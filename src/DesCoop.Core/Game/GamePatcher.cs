using System.Reflection;
using SoulsFormats;

namespace DesCoop.Game;

public sealed class PatchOptions
{
    /// <summary>Blue Eye Stone usable in body form: re-place your sign right after a boss without dying.</summary>
    public bool BlueEyeStoneInBodyForm { get; set; } = true;
    /// <summary>Stone of Ephemeral Eyes is not consumed: the host can always get body form back to summon.</summary>
    public bool InfiniteEphemeralEyes { get; set; } = true;
}

public sealed record PatchReport(bool Changed, List<string> Lines);

/// <summary>
/// Edits EquipParamGoods inside gameparam*.parambnd.dcx of the user's own dump.
/// Rows are located by their in-game English names (read from the game's own FMG text),
/// so no item ids are hardcoded. Originals are backed up next to the files.
/// </summary>
public static class GamePatcher
{
    public const string BackupSuffix = ".descoop-backup";

    // EQUIP_PARAM_GOODS_ST (Demon's Souls) byte offsets, from the community paramdef.
    internal const int OffEnableLive = 24;   // usable in body form
    internal const int OffEnableGray = 25;   // usable in soul form
    internal const int OffIsConsume = 33;    // consumed on use
    internal const int MinRowSize = 34;

    static readonly string[] BlueEyeNames = ["Blue Eye Stone"];
    static readonly string[] EphemeralNames = ["Stone of Ephemeral Eyes"];

    static readonly FieldInfo RowDataOffset =
        typeof(PARAM.Row).GetField("DataOffset", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingFieldException("PARAM.Row.DataOffset");

    public static IEnumerable<string> FindParamBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "param", "gameparam");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "gameparam*.parambnd*")
            .Where(f => !f.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Goods ids whose English name matches, read from every FMG in the game's msg folder.</summary>
    public static (HashSet<int> blue, HashSet<int> ephemeral) FindItemIds(string usrDir)
    {
        var blue = new HashSet<int>();
        var eph = new HashSet<int>();
        var msgDir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(msgDir)) return (blue, eph);

        foreach (var file in Directory.EnumerateFiles(msgDir, "*.msgbnd*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var bnd = BND3.Read(file);
                foreach (var f in bnd.Files)
                {
                    FMG fmg;
                    try { fmg = FMG.Read(f.Bytes); } catch { continue; }
                    foreach (var e in fmg.Entries)
                    {
                        var t = e.Text?.Trim();
                        if (string.IsNullOrEmpty(t)) continue;
                        if (BlueEyeNames.Any(n => n.Equals(t, StringComparison.OrdinalIgnoreCase))) blue.Add(e.ID);
                        if (EphemeralNames.Any(n => n.Equals(t, StringComparison.OrdinalIgnoreCase))) eph.Add(e.ID);
                    }
                }
            }
            catch { }
        }
        return (blue, eph);
    }

    public static PatchReport Apply(GameInfo game, PatchOptions opt)
    {
        var log = new List<string>();
        var (blue, eph) = FindItemIds(game.UsrDir);
        if (opt.BlueEyeStoneInBodyForm && blue.Count == 0) log.Add("Warning: 'Blue Eye Stone' not found in the game's text files.");
        if (opt.InfiniteEphemeralEyes && eph.Count == 0) log.Add("Warning: 'Stone of Ephemeral Eyes' not found in the game's text files.");

        var edits = new List<(int id, int offset, byte value, string what)>();
        if (opt.BlueEyeStoneInBodyForm) foreach (var id in blue) edits.Add((id, OffEnableLive, 1, "Blue Eye Stone usable in body form"));
        if (opt.InfiniteEphemeralEyes) foreach (var id in eph) edits.Add((id, OffIsConsume, 0, "Stone of Ephemeral Eyes is never consumed"));

        bool changed = false;
        var bnds = FindParamBnds(game.UsrDir).ToList();
        if (bnds.Count == 0) log.Add("Error: param/gameparam/gameparam*.parambnd not found.");
        foreach (var path in bnds)
        {
            // Always patch from the pristine file so toggling options off really reverts them.
            var backup = path + BackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup);
            var bnd = BND3.Read(backup);
            int n = PatchBinder(bnd, edits, log, Path.GetFileName(path));
            var fresh = bnd.Write();
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
            {
                File.WriteAllBytes(path, fresh);
                changed = true;
            }
            log.Add($"{Path.GetFileName(path)}: {n} change(s)");
        }
        return new PatchReport(changed, log);
    }

    internal static int PatchBinder(BND3 bnd, List<(int id, int offset, byte value, string what)> edits, List<string> log, string label)
    {
        var goodsFile = bnd.Files.FirstOrDefault(f => (f.Name ?? "").EndsWith("EquipParamGoods.param", StringComparison.OrdinalIgnoreCase));
        if (goodsFile == null)
        {
            log.Add($"{label}: EquipParamGoods.param not found");
            return 0;
        }
        var bytes = goodsFile.Bytes.ToArray();
        var param = PARAM.Read(bytes);
        var offsets = param.Rows.Select(r => (id: r.ID, off: (long)RowDataOffset.GetValue(r)!)).ToList();
        var sorted = offsets.Select(o => o.off).Where(o => o > 0).Distinct().OrderBy(o => o).ToList();
        int count = 0;
        foreach (var (id, offset, value, what) in edits)
        {
            var row = offsets.FirstOrDefault(o => o.id == id);
            if (row.off <= 0) continue;
            int idx = sorted.IndexOf(row.off);
            long next = idx + 1 < sorted.Count ? sorted[idx + 1] : bytes.Length;
            if (next - row.off < MinRowSize || row.off + offset >= bytes.Length) continue;
            if (bytes[row.off + offset] != value)
            {
                bytes[row.off + offset] = value;
                count++;
            }
            log.Add($"{label}: item {id} -> {what}");
        }
        goodsFile.Bytes = bytes;
        return count;
    }

    public static bool IsPatched(GameInfo game) =>
        FindParamBnds(game.UsrDir).Any(p => File.Exists(p + BackupSuffix) && !FilesEqual(p, p + BackupSuffix));

    public static void Restore(GameInfo game)
    {
        foreach (var p in FindParamBnds(game.UsrDir))
        {
            var b = p + BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }

    static bool FilesEqual(string a, string b) => File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
}
