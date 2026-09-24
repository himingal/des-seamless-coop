using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// Gives the two seamless items their own icons: the Stone of Ephemeral Eyes (icon 1056) becomes the
/// "Host Sigil" (a golden crowned flame) and the Blue Eye Stone (icon 1115) the "Join Sigil" (a blue gate).
/// Item icons are 64x96 cells in DXT5 atlases (menu/menu.tpf "Icon10", menu/icon.tpf "EquIcon_10"); each
/// cell is located through the 'Icon' dialog of the paired DRB (menu.drb / icon.drb) and overwritten in place
/// with pre-encoded BC3 blocks (embedded; sources in docs/assets, generator in tools/icongen). No other item
/// uses these two icons. Always rebuilt from the pristine backup; restored when the option is off.
/// </summary>
public static class IconPatcher
{
    public const int HostIconId = 1056, JoinIconId = 1115;
    internal const int CellW = 64, CellH = 96;
    const byte Dxt5 = 5;
    static readonly (string drb, string tpf)[] Sheets = [("menu.drb", "menu.tpf"), ("icon.drb", "icon.tpf")];

    internal static byte[] Asset(string name)
    {
        using var s = typeof(IconPatcher).Assembly.GetManifestResourceStream("DesCoop." + name)
            ?? throw new InvalidOperationException($"missing embedded icon {name}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    static IEnumerable<string> Variants(string menuDir, string tpf) =>
        new[] { tpf, tpf + ".dcx" }.Select(n => Path.Combine(menuDir, n)).Where(File.Exists);

    public static bool Apply(string usrDir, bool enabled, List<string> log)
    {
        var menuDir = Path.Combine(usrDir, "menu");
        if (!Directory.Exists(menuDir)) return false;
        var art = new Dictionary<int, byte[]> { [HostIconId] = Asset("host_sigil.bc3"), [JoinIconId] = Asset("join_sigil.bc3") };
        bool changed = false;

        foreach (var (drbName, tpfName) in Sheets)
        {
            // Where each icon lives: atlas texture name + block-aligned cell origin.
            var cells = new List<(int id, string tex, int x, int y)>();
            var drbPath = Path.Combine(menuDir, drbName);
            if (enabled && File.Exists(drbPath))
            {
                try
                {
                    var drb = DRB.Read(drbPath, DRB.DRBVersion.DarkSouls);
                    var dlg = drb.Dlgs.FirstOrDefault(d => d.Name == "Icon");
                    foreach (var id in art.Keys)
                        if (dlg?.Dlgos.FirstOrDefault(o => o.Name == $"EquIcon_{id:0000}")?.Shape is DRB.Shape.Sprite s)
                            cells.Add((id, drb.Textures[s.TextureIndex].Name, s.TexLeftEdge & ~3, s.TexTopEdge & ~3));
                }
                catch (Exception ex) { log.Add($"{drbName}: not read ({ex.Message})"); }
            }

            foreach (var path in Variants(menuDir, tpfName))
            {
                var backup = path + GamePatcher.BackupSuffix;
                var name = Path.GetFileName(path);
                try
                {
                    byte[] fresh;
                    if (!enabled)
                    {
                        if (!File.Exists(backup)) continue;
                        fresh = File.ReadAllBytes(backup);
                    }
                    else
                    {
                        if (!File.Exists(backup)) File.Copy(path, backup);
                        var tpf = TPF.Read(backup);
                        int n = 0;
                        foreach (var (id, tex, x, y) in cells)
                        {
                            var t = tpf.Textures.FirstOrDefault(tt => tt.Name == tex);
                            if (t?.Header == null || t.Format != Dxt5) continue;
                            if (WriteCell(t.Bytes, t.Header.Width, t.Header.Height, x, y, art[id])) n++;
                        }
                        if (n == 0) fresh = File.ReadAllBytes(backup);
                        else { fresh = tpf.Write(); log.Add($"{name}: {n} icon(s) redrawn (Host / Join Sigil)"); }
                    }
                    if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                    {
                        File.WriteAllBytes(path, fresh);
                        changed = true;
                    }
                }
                catch (Exception ex) { log.Add($"{name}: icons not changed ({ex.Message})"); }
            }
        }
        return changed;
    }

    /// <summary>Copies a 64x96 BC3 cell (row-major 4x4 blocks) into a DXT5 atlas at (x, y).</summary>
    internal static bool WriteCell(byte[] atlas, int atlasW, int atlasH, int x, int y, byte[] cell)
    {
        if (x < 0 || y < 0 || x + CellW > atlasW || y + CellH > atlasH || (x | y) % 4 != 0) return false;
        int atlasBlocksPerRow = atlasW / 4, cellBlocksPerRow = CellW / 4;
        if (cell.Length != cellBlocksPerRow * (CellH / 4) * 16) return false;
        for (int by = 0; by < CellH / 4; by++)
            Buffer.BlockCopy(cell, by * cellBlocksPerRow * 16, atlas, ((y / 4 + by) * atlasBlocksPerRow + x / 4) * 16, cellBlocksPerRow * 16);
        return true;
    }

    public static bool IsPatched(string usrDir) =>
        Sheets.SelectMany(s => Variants(Path.Combine(usrDir, "menu"), s.tpf))
            .Any(p => File.Exists(p + GamePatcher.BackupSuffix) && !File.ReadAllBytes(p).AsSpan().SequenceEqual(File.ReadAllBytes(p + GamePatcher.BackupSuffix)));

    public static void Restore(string usrDir)
    {
        foreach (var p in Sheets.SelectMany(s => Variants(Path.Combine(usrDir, "menu"), s.tpf)))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }
}
