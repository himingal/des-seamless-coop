using System.Buffers.Binary;
using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// Renames the starting classes in the game's menu text (msg/*/menu.msgbnd[.dcx]). The class list shows
/// &lt;?archetype@2010xx?&gt; tags, so only those tag entries are touched (blood-message words stay).
/// Always rebuilt from the pristine backup; with renaming off the original bytes are put back.
/// </summary>
public static class MsgPatcher
{
    public const int FirstClassTag = 201001, LastClassTag = 201010;

    public static IEnumerable<string> FindMenuBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "menu.msgbnd*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(GamePatcher.BackupSuffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Apply(string usrDir, IReadOnlyDictionary<string, string>? renames, List<string> log)
    {
        bool changed = false;
        foreach (var path in FindMenuBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetRelativePath(Path.Combine(usrDir, "msg"), path);
            try
            {
                byte[] fresh;
                if (renames == null || renames.Count == 0)
                {
                    if (!File.Exists(backup)) continue; // never touched
                    fresh = File.ReadAllBytes(backup);
                }
                else
                {
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    var bnd = BND3.Read(backup);
                    int n = RenameIn(bnd, renames);
                    if (n == 0) { fresh = File.ReadAllBytes(backup); }
                    else { fresh = bnd.Write(); log.Add($"{name}: {n} class name(s) replaced"); }
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                log.Add($"{name}: class names not changed ({ex.Message})");
            }
        }
        return changed;
    }

    /// <summary>Renames matching class tags in every FMG of the binder; returns the number of entries changed.</summary>
    public static int RenameIn(BND3 bnd, IReadOnlyDictionary<string, string> renames)
    {
        int total = 0;
        foreach (var f in bnd.Files)
        {
            FMG fmg;
            try { fmg = FMG.Read(f.Bytes); } catch { continue; }
            int n = 0;
            foreach (var e in fmg.Entries)
            {
                if (e.ID < FirstClassTag || e.ID > LastClassTag || e.Text == null) continue;
                if (renames.TryGetValue(e.Text.Trim(), out var to)) { e.Text = to; n++; }
            }
            if (n == 0) continue;
            f.Bytes = WriteLikeOriginal(fmg, f.Bytes.Length);
            total += n;
        }
        return total;
    }

    /// <summary>SoulsFormats drops the retail files' trailing 4-byte alignment; put it back so an unmodified FMG round-trips exactly.</summary>
    public static byte[] WriteLikeOriginal(FMG fmg, int originalLength)
    {
        var b = fmg.Write();
        if (originalLength % 4 != 0 || b.Length % 4 == 0) return b;
        var padded = new byte[(b.Length + 3) & ~3];
        b.CopyTo(padded, 0);
        if (fmg.BigEndian) BinaryPrimitives.WriteInt32BigEndian(padded.AsSpan(4), padded.Length);
        else BinaryPrimitives.WriteInt32LittleEndian(padded.AsSpan(4), padded.Length);
        return padded;
    }

    public static bool IsPatched(string usrDir) =>
        FindMenuBnds(usrDir).Any(p => File.Exists(p + GamePatcher.BackupSuffix)
            && !File.ReadAllBytes(p).AsSpan().SequenceEqual(File.ReadAllBytes(p + GamePatcher.BackupSuffix)));

    public static void Restore(string usrDir)
    {
        foreach (var p in FindMenuBnds(usrDir))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }
}
