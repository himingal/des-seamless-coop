using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// Item text in msg/*/item.msgbnd, written in one pass from the pristine backup (several features touch the
/// same binder, so separate passes would overwrite each other): the Cyanide Pill's name/description, and the
/// two seamless items — the Stone of Ephemeral Eyes becomes the "Host Sigil", the Blue Eye Stone the
/// "Join Sigil". Goods FMGs are picked by their binder id (10 name, 20 short description, 24 long description)
/// and must hold the Blue Eye Stone (#9997), so the spell FMGs — which reuse ids like 1021 — are never touched.
/// </summary>
public static class ItemTextPatcher
{
    public const int NameFmg = 10, InfoFmg = 20, CaptionFmg = 24;
    public const int EphemeralGoods = 1021, BlueEyeGoods = 9997;

    public sealed record ItemText(int Id, string Name, string Info, string Caption);

    public static readonly ItemText HostSigil = new(EphemeralGoods, "Host Sigil",
        "Restore body and open your world as host",
        "A sigil cut from ephemeral stone, its flame\ncrowned in gold. Restores the user's body.\n\n" +
        "In body form you are the host: your\ncompanions' signs are carried to your side,\nwherever you stand.\n\nNever consumed.");

    public static readonly ItemText JoinSigil = new(BlueEyeGoods, "Join Sigil",
        "In any form, join your host's world",
        "A cold blue sigil marked with a gate.\nUse it, human or soul, to offer your aid.\n\n" +
        "Your sign is carried to your host's side,\nwherever they stand. One touch and you are\ndrawn into their world.\n\n" +
        "Host and helper, bound by the same road.");

    public static readonly ItemText CyanidePill = new(CyanidePillPatcher.GoodsId, CyanidePillPatcher.PillName,
        "Die at once and enter soul form",
        "A bitter pill that stops the heart at once.\n\nUse it to die on the spot and enter soul\n" +
        "form, ready to join a host's world without\nwaiting for death to find you.");

    public static IEnumerable<string> FindItemBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "item.msgbnd*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(GamePatcher.BackupSuffix, StringComparison.OrdinalIgnoreCase)
                && (f.EndsWith(".msgbnd", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msgbnd.dcx", StringComparison.OrdinalIgnoreCase)));
    }

    public static bool Apply(string usrDir, bool cyanidePill, bool seamlessItems, List<string> log)
    {
        var texts = new List<ItemText>();
        if (cyanidePill) texts.Add(CyanidePill);
        if (seamlessItems) { texts.Add(HostSigil); texts.Add(JoinSigil); }

        bool changed = false;
        foreach (var path in FindItemBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetRelativePath(Path.Combine(usrDir, "msg"), path);
            try
            {
                byte[] fresh;
                if (texts.Count == 0)
                {
                    if (!File.Exists(backup)) continue;
                    fresh = File.ReadAllBytes(backup);
                }
                else
                {
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    var bnd = BND3.Read(backup);
                    int n = PatchBinder(bnd, texts);
                    fresh = n == 0 ? File.ReadAllBytes(backup) : bnd.Write();
                    if (n > 0) log.Add($"{name}: {n} item text(s) written");
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex) { log.Add($"{name}: item text not changed ({ex.Message})"); }
        }
        return changed;
    }

    /// <summary>Writes name/description entries into the goods FMGs of an item binder; returns entries written.</summary>
    public static int PatchBinder(BND3 bnd, IReadOnlyList<ItemText> texts)
    {
        int total = 0;
        foreach (var f in bnd.Files)
        {
            if (f.ID is not (NameFmg or InfoFmg or CaptionFmg)) continue;
            FMG fmg;
            try { fmg = FMG.Read(f.Bytes); } catch { continue; }
            if (!fmg.Entries.Any(e => e.ID == BlueEyeGoods)) continue; // goods FMGs only
            int n = 0;
            foreach (var t in texts)
            {
                string value = f.ID switch { NameFmg => t.Name, InfoFmg => t.Info, _ => t.Caption };
                var e = fmg.Entries.FirstOrDefault(x => x.ID == t.Id);
                if (e != null) e.Text = value;
                else fmg.Entries.Add(new FMG.Entry(t.Id, value));
                n++;
            }
            fmg.Entries.Sort((a, b) => a.ID.CompareTo(b.ID));
            f.Bytes = MsgPatcher.WriteLikeOriginal(fmg, f.Bytes.Length);
            total += n;
        }
        return total;
    }

    public static bool IsPatched(string usrDir) =>
        FindItemBnds(usrDir).Any(p => File.Exists(p + GamePatcher.BackupSuffix)
            && !File.ReadAllBytes(p).AsSpan().SequenceEqual(File.ReadAllBytes(p + GamePatcher.BackupSuffix)));

    public static void Restore(string usrDir)
    {
        foreach (var p in FindItemBnds(usrDir))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }
}
