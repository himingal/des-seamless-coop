using System.Reflection;

namespace DesCoop.Server;

public record struct WorldPos(int BlockId, float X, float Y, float Z, float AngX, float AngY, float AngZ, DateTime At);

public sealed class SosSign
{
    public uint SosId;
    public int BlockId;
    public string CharacterId = "";
    public float PosX, PosY, PosZ, AngX, AngY, AngZ;
    public int MessageId, MainMsgId, AddMsgCateId;
    public string PlayerInfo = "";
    public int Qwcwb, Qwclr;
    public int IsBlack; // 1 = red sign, 2 = blue sign, 3 = invasion
    public int PlayerLevel;
    public int[] Ratings = [0, 0, 0, 0, 0];
    public int TotalSessions;
    public int Port;
    public DateTime UpdatedAt = DateTime.UtcNow;

    public bool IsCoopSign => IsBlack != 1 && IsBlack != 3;

    public static SosSign FromParams(Dictionary<string, string> p, uint id, int port) => new()
    {
        SosId = id,
        BlockId = Protocol.ToSigned(p["blockID"]),
        CharacterId = p["characterID"],
        PosX = Protocol.F(p["posx"]), PosY = Protocol.F(p["posy"]), PosZ = Protocol.F(p["posz"]),
        AngX = Protocol.F(p["angx"]), AngY = Protocol.F(p["angy"]), AngZ = Protocol.F(p["angz"]),
        MessageId = Protocol.I(p["messageID"]), MainMsgId = Protocol.I(p["mainMsgID"]), AddMsgCateId = Protocol.I(p["addMsgCateID"]),
        PlayerInfo = p["playerInfo"],
        Qwcwb = Protocol.I(p["qwcwb"]), Qwclr = Protocol.I(p["qwclr"]),
        IsBlack = Protocol.I(p["isBlack"]),
        PlayerLevel = Protocol.I(p["playerLevel"]),
        Port = port,
    };

    public byte[] Serialize(float x, float y, float z, float ax, float ay, float az, uint? id = null, int? type = null)
    {
        var w = new Payload().U32(id ?? SosId).CStr(CharacterId)
            .F32(x).F32(y).F32(z).F32(ax).F32(ay).F32(az)
            .I32(MessageId).I32(MainMsgId).I32(AddMsgCateId)
            .I32(0);
        foreach (var r in Ratings) w.I32(r);
        w.I32(0).I32(TotalSessions).CStr(PlayerInfo).I32(Qwcwb).I32(Qwclr).U8((byte)(type ?? IsBlack));
        return w.ToArray();
    }

    public byte[] Serialize() => Serialize(PosX, PosY, PosZ, AngX, AngY, AngZ);
}

public sealed class BloodMessage
{
    public int BmId { get; set; }
    public string CharacterId { get; set; } = "";
    public int BlockId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float AngX { get; set; }
    public float AngY { get; set; }
    public float AngZ { get; set; }
    public int MessageId { get; set; }
    public int MainMsgId { get; set; }
    public int AddMsgCateId { get; set; }
    public int Rating { get; set; }

    public byte[] Serialize() => new Payload().I32(BmId).CStr(CharacterId).I32(BlockId)
        .F32(PosX).F32(PosY).F32(PosZ).F32(AngX).F32(AngY).F32(AngZ)
        .I32(MessageId).I32(MainMsgId).I32(AddMsgCateId).I32(Rating).ToArray();
}

public sealed class ReplayEntry
{
    public int GhostId { get; set; }
    public string CharacterId { get; set; } = "";
    public int BlockId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float AngX { get; set; }
    public float AngY { get; set; }
    public float AngZ { get; set; }
    public int MessageId { get; set; }
    public int MainMsgId { get; set; }
    public int AddMsgCateId { get; set; }
    public string ReplayBinary { get; set; } = "";

    public byte[] SerializeHeader() => new Payload().I32(GhostId).CStr(CharacterId).I32(BlockId)
        .F32(PosX).F32(PosY).F32(PosZ).F32(AngX).F32(AngY).F32(AngZ)
        .I32(MessageId).I32(MainMsgId).I32(AddMsgCateId).ToArray();
}

public sealed class PlayerStats
{
    public int GradeS { get; set; }
    public int GradeA { get; set; }
    public int GradeB { get; set; }
    public int GradeC { get; set; }
    public int GradeD { get; set; }
    public int Sessions { get; set; }
    public int MessageRating { get; set; }
    public int[] Ratings() => [GradeS, GradeA, GradeB, GradeC, GradeD];
}

public sealed class Ghost
{
    public required string CharacterId;
    public required int BlockId;
    public required byte[] ReplayData;
    public DateTime At = DateTime.UtcNow;
}

/// <summary>Live information about a connected player (in memory only).</summary>
public sealed class PlayerLive
{
    public required string CharacterId;
    public string Ip = "";
    public DateTime LastSeen = DateTime.UtcNow;
    public int LastBlock = int.MinValue;
    public WorldPos? LastPos;
    public bool InSession;
    public int Port;

    /// <summary>characterID is the NPID plus one save-slot digit.</summary>
    public string DisplayName => CharacterId.Length > 1 && char.IsDigit(CharacterId[^1]) ? CharacterId[..^1] : CharacterId;
}

public static class BlockNames
{
    static readonly Dictionary<int, string> Names = Load();

    static Dictionary<int, string> Load()
    {
        var d = new Dictionary<int, string>();
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesCoop.blocknames.txt");
        if (s == null) return d;
        using var r = new StreamReader(s);
        while (r.ReadLine() is { } line)
        {
            var parts = line.Split('|', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out int id)) d[id] = parts[1].Trim();
        }
        return d;
    }

    public static string Get(int blockId) =>
        blockId == int.MinValue ? "loading…" : Names.TryGetValue(blockId, out var n) ? n : $"Area {blockId}";

    /// <summary>World number (1..5) from a block id, 0 for the Nexus/unknown.</summary>
    public static int World(int blockId)
    {
        int b = Math.Abs(blockId) % 1000000 / 10000;
        return b switch { 2 => 1, 6 => 2, 4 => 3, 3 => 4, 5 => 5, _ => 0 };
    }
}
