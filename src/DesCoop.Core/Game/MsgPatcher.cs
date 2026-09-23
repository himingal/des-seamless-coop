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
    /// <summary>Menu FMG entry rendered on the title/start screen. Id 30000 is reused by other FMGs
    /// (dialog, key guide, one-line help) for unrelated text, so the title is matched by its exact
    /// original string, never by id alone.</summary>
    public const int TitleEntryId = 30000;
    public const string TitleOriginal = "PRESS START BUTTON";
    public const string TitleBranded = "SEAMLESS EDITION\nPRESS START BUTTON";

    public static IEnumerable<string> FindMenuBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "menu.msgbnd*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(GamePatcher.BackupSuffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Apply(string usrDir, IReadOnlyDictionary<string, string>? renames, bool seamlessTitle, List<string> log)
    {
        bool changed = false;
        bool doPatch = (renames != null && renames.Count > 0) || seamlessTitle;
        foreach (var path in FindMenuBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetRelativePath(Path.Combine(usrDir, "msg"), path);
            try
            {
                byte[] fresh;
                if (!doPatch)
                {
                    if (!File.Exists(backup)) continue; // never touched
                    fresh = File.ReadAllBytes(backup);
                }
                else
                {
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    var bnd = BND3.Read(backup);
                    int n = 0;
                    if (renames != null && renames.Count > 0) n += RenameIn(bnd, renames);
                    if (seamlessTitle && BrandTitle(bnd)) { n++; log.Add($"{name}: title screen branded SEAMLESS EDITION"); }
                    if (n == 0) { fresh = File.ReadAllBytes(backup); }
                    else { fresh = bnd.Write(); }
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                log.Add($"{name}: menu text not changed ({ex.Message})");
            }
        }
        return changed;
    }

    /// <summary>Sets the title-screen entry to the branded text in whichever FMG holds it; true if changed.</summary>
    public static bool BrandTitle(BND3 bnd)
    {
        bool any = false;
        foreach (var f in bnd.Files)
        {
            FMG fmg;
            try { fmg = FMG.Read(f.Bytes); } catch { continue; }
            // Match by the exact original title text: id 30000 is shared by other, unrelated FMGs.
            var e = fmg.Entries.FirstOrDefault(x => x.ID == TitleEntryId && x.Text?.Trim() == TitleOriginal);
            if (e == null) continue;
            e.Text = TitleBranded;
            f.Bytes = WriteLikeOriginal(fmg, f.Bytes.Length);
            any = true;
        }
        return any;
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
