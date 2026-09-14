using System.Buffers.Binary;
using System.Text;

namespace DesCoop.Game;

public sealed record GameInfo(string Root, string UsrDir, string Eboot, string Serial, string Title, string Version, bool IsDisc);

public static class GameLocator
{
    /// <summary>Every known Demon's Souls release (disc + best-of + trade demo).</summary>
    public static readonly HashSet<string> DemonsSoulsSerials =
        ["BLUS30443", "BLES00932", "BCJS30022", "BCAS20071", "BCJS70013", "BCKS10071", "BCAS20107", "BLUD80018", "NPUB30910", "NPEB01202"];

    /// <summary>Accepts the dump folder, its PS3_GAME folder, USRDIR, EBOOT.BIN or an HDD game folder.</summary>
    public static GameInfo? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = Path.GetFullPath(path.Trim().Trim('"'));
        if (File.Exists(path)) path = Path.GetDirectoryName(path)!;

        var candidates = new List<string>();
        var dir = new DirectoryInfo(path);
        for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent) candidates.Add(dir.FullName);

        foreach (var c in candidates)
        {
            // Disc layout: <root>/PS3_GAME/{PARAM.SFO,USRDIR/EBOOT.BIN}
            var ps3Game = Path.Combine(c, "PS3_GAME");
            if (TryBuild(c, ps3Game, true) is { } disc) return disc;
            if (Path.GetFileName(c).Equals("PS3_GAME", StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(c) is { } parent
                && TryBuild(parent, c, true) is { } disc2) return disc2;
            // HDD layout: dev_hdd0/game/<serial>/{PARAM.SFO,USRDIR/EBOOT.BIN}
            if (TryBuild(c, c, false) is { } hdd) return hdd;
        }

        // A folder holding several dumps: pick the Demon's Souls one.
        if (Directory.Exists(path))
            foreach (var sub in Directory.EnumerateDirectories(path))
                if (TryBuild(sub, Path.Combine(sub, "PS3_GAME"), true) is { } g && DemonsSoulsSerials.Contains(g.Serial)) return g;
        return null;
    }

    static GameInfo? TryBuild(string root, string gameDir, bool disc)
    {
        var sfoPath = Path.Combine(gameDir, "PARAM.SFO");
        var usr = Path.Combine(gameDir, "USRDIR");
        var eboot = Path.Combine(usr, "EBOOT.BIN");
        if (!File.Exists(sfoPath) || !File.Exists(eboot)) return null;
        var sfo = Sfo.Read(sfoPath);
        return new GameInfo(root, usr, eboot, sfo.GetValueOrDefault("TITLE_ID", ""), sfo.GetValueOrDefault("TITLE", ""),
            sfo.GetValueOrDefault("APP_VER", sfo.GetValueOrDefault("VERSION", "")), disc);
    }

    public static bool IsDemonsSouls(GameInfo g) =>
        DemonsSoulsSerials.Contains(g.Serial) || g.Title.Contains("Demon", StringComparison.OrdinalIgnoreCase);
}

public static class Sfo
{
    public static Dictionary<string, string> Read(string path)
    {
        var d = File.ReadAllBytes(path);
        var result = new Dictionary<string, string>();
        if (d.Length < 20 || d[0] != 0 || d[1] != 'P' || d[2] != 'S' || d[3] != 'F') return result;
        int keyTable = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(8));
        int dataTable = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(12));
        int count = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(16));
        for (int i = 0; i < count; i++)
        {
            int e = 20 + i * 16;
            if (e + 16 > d.Length) break;
            int keyOff = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(e));
            int fmt = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(e + 2));
            int len = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(e + 4));
            int dataOff = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(e + 12));
            int k = keyTable + keyOff, kEnd = k;
            while (kEnd < d.Length && d[kEnd] != 0) kEnd++;
            string key = Encoding.ASCII.GetString(d, k, kEnd - k);
            int v = dataTable + dataOff;
            if (v < 0 || v + len > d.Length) continue;
            result[key] = fmt == 0x0404
                ? BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(v)).ToString()
                : Encoding.UTF8.GetString(d, v, len).TrimEnd('\0');
        }
        return result;
    }
}
