using System.Buffers.Binary;

namespace DesCoop.Game;

/// <summary>
/// Co-op in the Nexus. The online "block" a player reports (blockID of getSosData/getWanderingGhost…) is the
/// MapNameID of the collision under their feet (MSB), next to a sub-block index: areas use positive ids
/// (1-1 = 20070..20073, index 0) and the Nexus is -10079 with index -1 — a negative id is how the game marks a
/// place with no multiplayer, so there it never placed or looked for summon signs. Every Nexus collision is
/// switched to block 10079 / index 0 (both big-endian shorts, patched in place), so the helper's Join Sigil works
/// there and the host sees the sign (verified in RPCS3: addSosData/getSosData with block 10079). The area name
/// shown on screen is unchanged.
/// </summary>
public static class NexusPatcher
{
    const string MapFile = "m01_00_00_00.msb";
    const short NexusBlock = 10079;

    static IEnumerable<string> Files(string usrDir)
    {
        var dir = Path.Combine(usrDir, "map", "mapstudio");
        return new[] { MapFile, MapFile + ".dcx" }.Select(f => Path.Combine(dir, f)).Where(File.Exists);
    }

    /// <summary>Replaces every (index -1, block -10079) pair with (0, 10079); returns how many were changed.</summary>
    internal static int OpenBlocks(byte[] msb)
    {
        Span<byte> from = stackalloc byte[4], to = stackalloc byte[4];
        BinaryPrimitives.WriteInt16BigEndian(from, -1);
        BinaryPrimitives.WriteInt16BigEndian(from[2..], (short)-NexusBlock);
        BinaryPrimitives.WriteInt16BigEndian(to, 0);
        BinaryPrimitives.WriteInt16BigEndian(to[2..], NexusBlock);
        int n = 0;
        for (int i = msb.AsSpan().IndexOf(from); i >= 0; )
        {
            to.CopyTo(msb.AsSpan(i));
            n++;
            int next = msb.AsSpan(i + 4).IndexOf(from);
            i = next < 0 ? -1 : i + 4 + next;
        }
        return n;
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
                else if (path.EndsWith(".dcx", StringComparison.OrdinalIgnoreCase))
                {
                    log.Add($"{name}: compressed map not handled, left alone");
                    continue;
                }
                else
                {
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    fresh = File.ReadAllBytes(backup);
                    int n = OpenBlocks(fresh);
                    log.Add(n > 0 ? $"{name}: Nexus opened for co-op ({n} collisions -> online block {NexusBlock})" : $"{name}: Nexus block not found, left alone");
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
                {
                    File.WriteAllBytes(path, fresh);
                    changed = true;
                }
            }
            catch (Exception ex) { log.Add($"{name}: Nexus not changed ({ex.Message})"); }
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
