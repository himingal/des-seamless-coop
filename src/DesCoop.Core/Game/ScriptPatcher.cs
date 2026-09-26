using System.Text;
using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// "Stay in soul form": the game gives your body back after a boss (host in soul form, or a blue phantom
/// going home) from its Lua event scripts (script/global_event.lua inside every m*.luabnd). Those calls are
/// commented out, so nobody is revived automatically; the Stone of Ephemeral Eyes still works on demand.
/// The game reads script/m*.luabnd.dcx.sdat; RPCS3 accepts an unencrypted .sdat (no NPD header), so the
/// patched DCX is written there as well as to the .dcx and plain .luabnd copies. Originals are backed up.
/// </summary>
public static class ScriptPatcher
{
    const string ScriptName = "global_event.lua";
    /// <summary>Lines disabled in global_event.lua (only as whole statements).</summary>
    static readonly string[] Disabled =
    [
        "proxy:RevivePlayer();",
        "proxy:RevivePlayerNext();",
        "proxy:SetAliveMotion( true );",
        "proxy:SetTextEffect(TEXT_TYPE_Revival);",
    ];
    /// <summary>Manual revivals stay (Demon's Soul revive): only automatic ones are removed.</summary>
    static readonly string[] KeepInFunctions = ["OnDemonsSoulRevive"];

    public static IEnumerable<string> FindScriptBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "script");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "m*.luabnd*")
            .Where(f => !f.EndsWith(GamePatcher.BackupSuffix, StringComparison.OrdinalIgnoreCase)
                && (f.EndsWith(".luabnd", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".luabnd.dcx", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".luabnd.dcx.sdat", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Persistent co-op: a Lua block (embedded seamless_coop.lua) appended to global_event.lua. In Lua the last
    /// definition wins, so it replaces the boss-clear teardown for the summoned helper (no dissolve/hide/load-wait,
    /// no room teardown for anyone), keeps the shared progress when the helper does go home, and re-places the
    /// helper's sign once it is home. Commenting out only the final kick (the first attempt) left the helper
    /// hidden with a frozen menu, because the rest of the teardown still ran.
    /// </summary>
    internal const string SeamlessMarker = "--[DeS Co-op] Seamless co-op, appended by DeS Seamless Co-op.";

    internal static byte[] SeamlessBlock()
    {
        using var s = typeof(ScriptPatcher).Assembly.GetManifestResourceStream("DesCoop.seamless_coop.lua")
            ?? throw new InvalidOperationException("missing embedded seamless_coop.lua");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Shared boss progression (experimental). When a boss dies the summoned blue phantom goes home through
    /// <c>BlockClear2_3</c>, which calls <c>SetFlagInitState(2)</c> — the "roll back everything from this
    /// session" mode also used for disconnects and PvP deaths — so the kill never reaches the helper's world.
    /// Mode 1 is what an ordinary solo death uses (world progress is kept). Switching that single call to mode
    /// 1 makes the boss count for the helper too. Host death, disconnects and invasions keep their rollback.
    /// </summary>
    const string BossClearFunction = "BlockClear2_3";
    const string RollbackCall = "proxy:SetFlagInitState(2);";
    const string KeepProgressCall = "proxy:SetFlagInitState(1);";

    /// <summary>Comments out the automatic revive statements (with <paramref name="sharedBossProgress"/>, also the
    /// boss-clear flag rollback of the phantom) and, when <paramref name="persistentCoop"/> is set, appends the
    /// seamless co-op block; returns the new text and how many statements/blocks changed.</summary>
    public static (byte[] bytes, int changed) PatchGlobalEvent(byte[] source, bool persistentCoop = false, bool sharedBossProgress = false)
    {
        // Shift-JIS file: split on raw bytes and only touch pure-ASCII statements.
        var lines = Split(source);
        string? fn = null;
        int changed = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var text = Encoding.Latin1.GetString(lines[i]);
            var t = text.Trim();
            if (t.StartsWith("function ")) fn = t[9..].Split('(')[0].Trim();

            // Shared boss progression: keep the session's world progress when the phantom leaves after a boss.
            if (sharedBossProgress && fn == BossClearFunction && ReplaceStatement(lines, i, text, t, RollbackCall, KeepProgressCall))
            {
                changed++;
                continue;
            }

            if (fn != null && KeepInFunctions.Contains(fn)) continue;
            foreach (var stmt in Disabled)
                if (CommentStatement(lines, i, text, t, stmt)) { changed++; break; }
        }
        var result = Join(lines);
        if (persistentCoop && !Encoding.Latin1.GetString(result).Contains(SeamlessMarker))
        {
            var block = SeamlessBlock();
            byte[] sep = result.Length > 0 && result[^1] != (byte)'\n' ? [(byte)'\r', (byte)'\n'] : [];
            result = [.. result, .. sep, .. block];
            changed++;
        }
        return (result, changed);
    }

    /// <summary>Comments the line in place if it is exactly <paramref name="stmt"/> (nothing else after it).</summary>
    static bool CommentStatement(List<byte[]> lines, int i, string text, string t, string stmt)
    {
        if (!t.StartsWith(stmt)) return false;
        var rest = t[stmt.Length..].Trim();
        if (rest.Length > 0 && !rest.StartsWith("--")) return false; // something else on the line: leave it
        int indent = text.Length - text.TrimStart().Length;
        lines[i] = Encoding.Latin1.GetBytes(text[..indent] + "--[DeS Co-op] " + text[indent..]);
        return true;
    }

    /// <summary>Replaces the line in place if it is exactly <paramref name="stmt"/>, keeping the indentation.</summary>
    static bool ReplaceStatement(List<byte[]> lines, int i, string text, string t, string stmt, string with)
    {
        if (!t.StartsWith(stmt)) return false;
        var rest = t[stmt.Length..].Trim();
        if (rest.Length > 0 && !rest.StartsWith("--")) return false;
        int indent = text.Length - text.TrimStart().Length;
        string eol = text.EndsWith("\r\n") ? "\r\n" : text.EndsWith('\n') ? "\n" : "";
        lines[i] = Encoding.Latin1.GetBytes(text[..indent] + with + " --[DeS Co-op] shared boss progress (was " + stmt + ")" + eol);
        return true;
    }

    /// <summary>
    /// Open session: the game locks the co-op room (<c>proxy:LockSession();</c>) when a boss dies (BlockClear2,
    /// global_event.lua) and when the local player walks through a boss fog gate (OnEvent_*_1 in the map scripts),
    /// so nobody can join a cleared area or a boss fight. Every lock is commented out: the helper can (re)join
    /// at any time, including right after the boss is dead.
    /// </summary>
    const string LockCall = "proxy:LockSession();";

    public static (byte[] bytes, int changed) OpenSessionIn(byte[] source)
    {
        var lines = Split(source);
        int changed = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var text = Encoding.Latin1.GetString(lines[i]);
            if (CommentStatement(lines, i, text, text.Trim(), LockCall)) changed++;
        }
        return (Join(lines), changed);
    }

    /// <summary>Patches the scripts of a binder (global_event.lua, plus every map script for the open session);
    /// returns the number of changed statements.</summary>
    public static int PatchBinder(BND3 bnd, bool persistentCoop = false, bool sharedBossProgress = false, bool openSession = false)
    {
        int n = 0;
        foreach (var f in bnd.Files)
        {
            var file = Path.GetFileName(f.Name ?? "");
            if (file.Equals(ScriptName, StringComparison.OrdinalIgnoreCase))
            {
                var (bytes, changed) = PatchGlobalEvent(f.Bytes, persistentCoop, sharedBossProgress);
                f.Bytes = bytes;
                n += changed;
            }
            if (openSession && file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                var (bytes, changed) = OpenSessionIn(f.Bytes);
                f.Bytes = bytes;
                n += changed;
            }
        }
        return n;
    }

    public static bool Apply(string usrDir, bool stayInSoulForm, bool persistentCoop, List<string> log) =>
        Apply(usrDir, stayInSoulForm, persistentCoop, false, false, log);

    public static bool Apply(string usrDir, bool stayInSoulForm, bool persistentCoop, bool sharedBossProgress, bool openSession, List<string> log)
    {
        bool doPatch = stayInSoulForm || persistentCoop || sharedBossProgress || openSession;
        bool changed = false;
        foreach (var path in FindScriptBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetFileName(path);
            try
            {
                byte[] fresh;
                if (!doPatch)
                {
                    if (!File.Exists(backup)) continue;
                    fresh = File.ReadAllBytes(backup);
                }
                else
                {
                    bool sdat = path.EndsWith(".sdat", StringComparison.OrdinalIgnoreCase);
                    // An encrypted .sdat holds the same bytes as the .dcx next to it (verified on retail),
                    // so it is rebuilt from that file's pristine copy.
                    var source = sdat ? path[..^5] : path;
                    var sourceBackup = source + GamePatcher.BackupSuffix;
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    if (sdat && !File.Exists(sourceBackup))
                    {
                        if (!File.Exists(source)) { log.Add($"{name}: no .dcx next to it, left alone"); continue; }
                        File.Copy(source, sourceBackup);
                    }
                    var bnd = BND3.Read(sdat ? sourceBackup : backup);
                    int n = PatchBinder(bnd, persistentCoop, sharedBossProgress, openSession);
                    if (n == 0) { log.Add($"{name}: script calls not found, left alone"); fresh = File.ReadAllBytes(backup); }
                    else { fresh = bnd.Write(); log.Add($"{name}: {n} script line(s) changed{(persistentCoop ? " (incl. seamless co-op block)" : "")}{(sharedBossProgress ? " (incl. shared boss progress)" : "")}{(openSession ? " (session stays open)" : "")}"); }
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                log.Add($"{name}: not changed ({ex.Message})");
            }
        }
        return changed;
    }

    public static bool IsPatched(string usrDir) =>
        FindScriptBnds(usrDir).Any(p => File.Exists(p + GamePatcher.BackupSuffix)
            && !File.ReadAllBytes(p).AsSpan().SequenceEqual(File.ReadAllBytes(p + GamePatcher.BackupSuffix)));

    public static void Restore(string usrDir)
    {
        foreach (var p in FindScriptBnds(usrDir))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }

    static List<byte[]> Split(byte[] s)
    {
        var lines = new List<byte[]>();
        int start = 0;
        for (int i = 0; i < s.Length; i++)
            if (s[i] == (byte)'\n') { lines.Add(s[start..(i + 1)]); start = i + 1; }
        lines.Add(s[start..]);
        return lines;
    }

    static byte[] Join(List<byte[]> lines)
    {
        var o = new byte[lines.Sum(l => l.Length)];
        int p = 0;
        foreach (var l in lines) { l.CopyTo(o, p); p += l.Length; }
        return o;
    }
}
