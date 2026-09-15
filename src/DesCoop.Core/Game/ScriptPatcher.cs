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

    /// <summary>Comments out the automatic revive statements; returns the new text and how many lines changed.</summary>
    public static (byte[] bytes, int changed) PatchGlobalEvent(byte[] source)
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
            if (fn != null && KeepInFunctions.Contains(fn)) continue;
            foreach (var stmt in Disabled)
            {
                if (!t.StartsWith(stmt)) continue;
                var rest = t[stmt.Length..].Trim();
                if (rest.Length > 0 && !rest.StartsWith("--")) break; // something else on the line: leave it
                int indent = text.Length - text.TrimStart().Length;
                lines[i] = Encoding.Latin1.GetBytes(text[..indent] + "--[DeS Co-op] " + text[indent..]);
                changed++;
                break;
            }
        }
        return (Join(lines), changed);
    }

    /// <summary>Patches global_event.lua in a script binder; returns the number of disabled statements.</summary>
    public static int PatchBinder(BND3 bnd)
    {
        int n = 0;
        foreach (var f in bnd.Files.Where(f => Path.GetFileName(f.Name ?? "").Equals(ScriptName, StringComparison.OrdinalIgnoreCase)))
        {
            var (bytes, changed) = PatchGlobalEvent(f.Bytes);
            f.Bytes = bytes;
            n += changed;
        }
        return n;
    }

    public static bool Apply(string usrDir, bool stayInSoulForm, List<string> log)
    {
        bool changed = false;
        foreach (var path in FindScriptBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetFileName(path);
            try
            {
                byte[] fresh;
                if (!stayInSoulForm)
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
                    int n = PatchBinder(bnd);
                    if (n == 0) { log.Add($"{name}: revive calls not found, left alone"); fresh = File.ReadAllBytes(backup); }
                    else { fresh = bnd.Write(); log.Add($"{name}: {n} automatic revive line(s) disabled"); }
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
