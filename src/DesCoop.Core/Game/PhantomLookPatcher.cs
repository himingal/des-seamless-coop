using System.Buffers.Binary;
using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// No blue phantom: a summoned helper is drawn with the "white ghost" material parameters
/// (mtd Ps_Ghost_Param_White for the player's parts, Cs_Ghost_Param_White for the body) — a blue fresnel edge
/// (g_GhostEdgeColor) plus a scrolling blue texture overlay (g_GhostTexColor) on the DS_Ghost_Param shader.
/// Both colors are cleared and the overlay alpha is set to 1, so the helper looks like a normal, solid character
/// in the host's world (verified in RPCS3 on the same shader: no tint, no see-through). Soul form (grey) and
/// invaders (black) keep their vanilla look. The game reads mtd/mtd.mtdbnd(.dcx) and the loose copies next to
/// it; all are patched in place (float values only — MTD round-trips are not byte-identical), from backups.
/// </summary>
public static class PhantomLookPatcher
{
    static readonly string[] Materials = ["ps_ghost_param_white", "cs_ghost_param_white"];
    static readonly string[] Binders = ["mtd.mtdbnd", "mtd.mtdbnd.dcx"];

    static bool IsTarget(string name) =>
        Materials.Contains(Path.GetFileNameWithoutExtension(name.Replace('\\', '/')).ToLowerInvariant());

    static IEnumerable<string> Files(string usrDir)
    {
        var dir = Path.Combine(usrDir, "mtd");
        if (!Directory.Exists(dir)) return [];
        var loose = Directory.EnumerateFiles(dir, "*.mtd", SearchOption.AllDirectories).Where(IsTarget);
        return Binders.Select(b => Path.Combine(dir, b)).Where(File.Exists).Concat(loose);
    }

    /// <summary>Clears the ghost edge/overlay colors of one MTD (overlay alpha 1 = solid); returns null when the
    /// parameters are not where they should be.</summary>
    internal static byte[]? Neutralize(byte[] mtd)
    {
        var m = MTD.Read(mtd);
        var o = (byte[])mtd.Clone();
        int n = 0;
        foreach (var p in m.Params.Where(p => p.Name is "g_GhostEdgeColor" or "g_GhostTexColor"))
        {
            if (p.Value is not float[] v || v.Length != 4) return null;
            var pattern = new byte[16];
            for (int i = 0; i < 4; i++) BinaryPrimitives.WriteSingleLittleEndian(pattern.AsSpan(i * 4), v[i]);
            int at = o.AsSpan().IndexOf(pattern);
            if (at < 0) return null;
            o.AsSpan(at, 16).Clear();
            if (p.Name == "g_GhostTexColor") BinaryPrimitives.WriteSingleLittleEndian(o.AsSpan(at + 12), 1f);
            n++;
        }
        return n == 2 ? o : null;
    }

    public static bool Apply(string usrDir, bool enabled, List<string> log)
    {
        bool changed = false;
        foreach (var path in Files(usrDir))
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
                    var original = File.ReadAllBytes(backup);
                    int n = 0;
                    if (name.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
                    {
                        var patched = Neutralize(original);
                        fresh = patched ?? original;
                        if (patched != null) n++;
                    }
                    else
                    {
                        var bnd = BND3.Read(original);
                        foreach (var f in bnd.Files.Where(f => IsTarget(f.Name ?? "")))
                            if (Neutralize(f.Bytes) is { } patched) { f.Bytes = patched; n++; }
                        fresh = n > 0 ? bnd.Write() : original;
                    }
                    if (n > 0) log.Add($"{name}: helper drawn without the blue phantom tint ({n} material(s))");
                    else log.Add($"{name}: phantom material not found, left alone");
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex) { log.Add($"{name}: phantom look not changed ({ex.Message})"); }
        }
        return changed;
    }

    public static bool IsPatched(string usrDir) =>
        Files(usrDir).Any(p => File.Exists(p + GamePatcher.BackupSuffix)
            && !File.ReadAllBytes(p).AsSpan().SequenceEqual(File.ReadAllBytes(p + GamePatcher.BackupSuffix)));

    public static void Restore(string usrDir)
    {
        foreach (var p in Files(usrDir))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }
}
