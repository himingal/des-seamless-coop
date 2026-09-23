using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DesCoop.Net;
using DesCoop.Server;

namespace DesCoop.Tests;

public class ServerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "descoop-test-" + Guid.NewGuid().ToString("N"));
    readonly DesServer _server;

    public ServerTests()
    {
        _server = new DesServer(new DesServerOptions { DataDir = _dir });
    }

    public void Dispose()
    {
        _server.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
    }

    static Dictionary<string, string> P(params (string k, string v)[] kv) => kv.ToDictionary(x => x.k, x => x.v);

    static Dictionary<string, string> Sign(string who, int block, float x, int isBlack = 2) => P(
        ("characterID", who), ("blockID", ((uint)block).ToString()), ("posx", x.ToString()), ("posy", "1.5"), ("posz", "3"),
        ("angx", "0"), ("angy", "0.5"), ("angz", "0"), ("messageID", "0"), ("mainMsgID", "0"), ("addMsgCateID", "0"),
        ("playerInfo", "info"), ("qwcwb", "0"), ("qwclr", "0"), ("isBlack", isBlack.ToString()), ("playerLevel", "30"));

    internal static byte[] MakeReplay(params float[][] positions)
    {
        var raw = new MemoryStream();
        void BE32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); raw.Write(b); }
        void BEF(float v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteSingleBigEndian(b, v); raw.Write(b); }
        BE32((uint)positions.Length); BE32(0); BE32(0);
        foreach (var p in positions) { foreach (var f in p) BEF(f); BE32(0); BE32(0); }
        for (int i = 0; i < 20; i++) BE32(0);
        var name = Encoding.BigEndianUnicode.GetBytes("Tester");
        raw.Write(name);
        raw.Write(new byte[34 - name.Length]);
        var outMs = new MemoryStream();
        using (var z = new ZLibStream(outMs, CompressionLevel.Optimal, true)) z.Write(raw.ToArray());
        return outMs.ToArray();
    }

    /// <summary>Parses a getSosData payload into (knownIds, fresh signs as (id, name, x, y, z)).</summary>
    static (List<uint> known, List<(uint id, string who, float x, float y, float z)> fresh) ParseSos(byte[] d)
    {
        int o = 0;
        uint U() { var v = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o)); o += 4; return v; }
        float Fl() { var v = BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(o)); o += 4; return v; }
        string S() { int s = o; while (d[o] != 0) o++; var r = Encoding.Latin1.GetString(d, s, o - s); o++; return r; }
        var known = new List<uint>();
        uint nk = U();
        for (int i = 0; i < nk; i++) known.Add(U());
        var fresh = new List<(uint, string, float, float, float)>();
        uint nf = U();
        for (int i = 0; i < nf; i++)
        {
            uint id = U(); string who = S();
            float x = Fl(), y = Fl(), z = Fl(); Fl(); Fl(); Fl();
            U(); U(); U(); U();
            for (int r = 0; r < 5; r++) U();
            U(); U(); S(); U(); U(); o += 1;
            fresh.Add((id, who, x, y, z));
        }
        Assert.Equal(d.Length, o);
        return (known, fresh);
    }

    [Fact]
    public void Aes_roundtrip_and_params()
    {
        var body = Protocol.Encrypt(Encoding.Latin1.GetBytes("ver=100&characterID=abc&index=1&"));
        var p = Protocol.ParseParams(Protocol.Decrypt(body));
        Assert.Equal("abc", p["characterID"]);
        Assert.Equal("1", p["index"]);
    }

    [Fact]
    public void Party_sign_follows_the_host_to_any_area()
    {
        // Friend places a blue sign in the Nexus.
        _server.Dispatch("initializeCharacter.spd", P(("characterID", "Friend"), ("index", "0")), "10.0.0.2", 18667);
        Assert.Equal((byte)0x0a, _server.Dispatch("addSosData.spd", Sign("Friend0", -10079, 1), "10.0.0.2", 18667)!.Value.cmd);

        // Host (US disc) walks around Boletarian Palace; ghost upload carries his position.
        _server.Dispatch("initializeCharacter.spd", P(("characterID", "Host"), ("index", "0")), "10.0.0.1", 18666);
        var replay = Protocol.EncodeGameBase64(MakeReplay([0, 0, 0, 0, 0, 0], [100f, 20f, -50f, 0f, 0f, 0f]));
        _server.Dispatch("setWanderingGhost.spd", P(("characterID", "Host0"), ("ghostBlockID", "20070"), ("replayData", replay)), "10.0.0.1", 18666);

        var res = _server.Dispatch("getSosData.spd", P(("characterID", "Host0"), ("blockID", "20070"), ("sosNum", "10"), ("sosList", "")), "10.0.0.1", 18666)!.Value;
        Assert.Equal((byte)0x0f, res.cmd);
        var (known, fresh) = ParseSos(res.data);
        Assert.Empty(known);
        var s = Assert.Single(fresh);
        Assert.Equal("Friend0", s.who);
        Assert.InRange(Math.Sqrt(Math.Pow(s.x - 100, 2) + Math.Pow(s.z + 50, 2)), 0.5, 2.0);
        Assert.Equal(20f, s.y);

        // Second poll: sign is reported as already known.
        var again = ParseSos(_server.Dispatch("getSosData.spd", P(("characterID", "Host0"), ("blockID", "20070"), ("sosNum", "10"), ("sosList", $"{s.id}")), "10.0.0.1", 18666)!.Value.data);
        Assert.Equal([s.id], again.known);

        // Host touches the sign -> friend's poll returns the NP room.
        Assert.Equal([1], _server.Dispatch("summonOtherCharacter.spd", P(("ghostID", s.id.ToString()), ("NPRoomID", "ROOM42")), "10.0.0.1", 18666)!.Value.data);
        var chk = _server.Dispatch("checkSosData.spd", P(("characterID", "Friend0")), "10.0.0.2", 18667)!.Value;
        Assert.Equal("ROOM42", Encoding.Latin1.GetString(chk.data));
        Assert.Equal([0], _server.Dispatch("checkSosData.spd", P(("characterID", "Friend0")), "10.0.0.2", 18667)!.Value.data);

        var st = _server.GetStatus();
        Assert.Contains(st.Players, p => p.Name == "Friend" && p.HasSign);
        Assert.Contains(st.Players, p => p.Name == "Host" && p.Area.Contains("Boletarian"));
    }

    [Fact]
    public void Login_before_character_is_not_listed_as_a_player()
    {
        // Real game order (seen on RPCS3): login.spd arrives before any characterID.
        var motd = _server.Dispatch("login.spd", P(("ver", "100")), "10.0.0.9", 18666)!.Value;
        Assert.DoesNotContain("[10.0.0.9]", Encoding.Latin1.GetString(motd.data));
        Assert.Empty(_server.GetStatus().Players);
        _server.Dispatch("initializeCharacter.spd", P(("characterID", "Real"), ("index", "0")), "10.0.0.9", 18666);
        Assert.Equal("Real", Assert.Single(_server.GetStatus().Players).Name);
    }

    [Fact]
    public void Login_motd_teaches_loot_sharing_and_fast_regroup()
    {
        var motd = Encoding.Latin1.GetString(_server.Dispatch("login.spd", P(("ver", "100")), "10.0.0.9", 18666)!.Value.data);
        Assert.Contains("LOOT", motd);
        Assert.Contains("Cyanide", motd);   // fast regroup via the pill
        Assert.Contains("Blue Eye Stone", motd);
    }

    [Fact]
    public void Red_signs_and_own_signs_are_not_relocated()
    {
        _server.Dispatch("addSosData.spd", Sign("Red0", -10079, 1, isBlack: 1), "10.0.0.3", 18667);
        _server.Dispatch("addSosData.spd", Sign("Host0", -10079, 1), "10.0.0.1", 18667);
        var replay = Protocol.EncodeGameBase64(MakeReplay([5, 5, 5, 0, 0, 0]));
        _server.Dispatch("setWanderingGhost.spd", P(("characterID", "Host0"), ("ghostBlockID", "20070"), ("replayData", replay)), "10.0.0.1", 18667);
        var (_, fresh) = ParseSos(_server.Dispatch("getSosData.spd", P(("characterID", "Host0"), ("blockID", "20070"), ("sosNum", "10"), ("sosList", "")), "10.0.0.1", 18667)!.Value.data);
        Assert.Empty(fresh);
    }

    [Fact]
    public void Same_block_sign_is_shown_across_merged_regions()
    {
        // A JP sign is visible to a US reader in the same block (regions are merged). The co-op sign is
        // moved next to the reader (here the only known position is the sign's own), so it stays reachable.
        _server.Dispatch("addSosData.spd", Sign("Jp0", 20070, 42), "10.0.0.4", 18668);
        var (_, fresh) = ParseSos(_server.Dispatch("getSosData.spd", P(("characterID", "Us0"), ("blockID", "20070"), ("sosNum", "10"), ("sosList", "")), "10.0.0.5", 18666)!.Value.data);
        Assert.Equal(42f, Assert.Single(fresh).x, 3f); // near the anchor
    }

    [Fact]
    public void Messages_and_bloodstains_roundtrip()
    {
        var msg = Sign("A0", 20070, 7);
        _server.Dispatch("addBloodMessage.spd", msg, "10.0.0.1", 18666);
        var r = _server.Dispatch("getBloodMessage.spd", P(("characterID", "B0"), ("blockID", "20070"), ("replayNum", "10")), "10.0.0.2", 18666)!.Value;
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(r.data));

        var rp = new Dictionary<string, string>(Sign("A0", 20070, 7)) { ["replayBinary"] = Protocol.EncodeGameBase64(MakeReplay([1, 2, 3, 0, 0, 0])) };
        _server.Dispatch("addReplayData.spd", rp, "10.0.0.1", 18666);
        var list = _server.Dispatch("getReplayList.spd", P(("blockID", "20070"), ("replayNum", "10")), "10.0.0.2", 18666)!.Value;
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(list.data));
        int ghostId = BinaryPrimitives.ReadInt32LittleEndian(list.data.AsSpan(4));
        var data = _server.Dispatch("getReplayData.spd", P(("ghostID", ghostId.ToString())), "10.0.0.2", 18666)!.Value;
        Assert.Equal(ghostId, BinaryPrimitives.ReadInt32LittleEndian(data.data));
        Assert.True(BinaryPrimitives.ReadInt32LittleEndian(data.data.AsSpan(4)) > 10);
    }

    [Fact]
    public void World_state_persists_across_restarts()
    {
        _server.Dispatch("addBloodMessage.spd", Sign("A0", 20070, 7), "10.0.0.1", 18666);
        _server.Dispose();
        var json = File.ReadAllText(Path.Combine(_dir, "world.json"));
        Assert.Contains("\"Messages\"", json);
        Assert.DoesNotContain("LockProvider", json);

        using var again = new DesServer(new DesServerOptions { DataDir = _dir });
        var r = again.Dispatch("getBloodMessage.spd", P(("characterID", "B0"), ("blockID", "20070"), ("replayNum", "10")), "10.0.0.2", 18666)!.Value;
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(r.data));
        var known = again.Dispatch("getSosData.spd", P(("characterID", "B0"), ("blockID", "20070"), ("sosNum", "10"), ("sosList", "")), "10.0.0.2", 18666);
        Assert.NotNull(known);
    }

    [Fact]
    public void Advertised_address_logic()
    {
        Assert.Equal("127.0.0.1", _server.AdvertisedAddress(IPAddress.Loopback, IPAddress.Loopback));
        Assert.Equal("26.1.2.3", _server.AdvertisedAddress(IPAddress.Parse("26.9.9.9"), IPAddress.Parse("26.1.2.3")));
        Assert.Equal("ds-eu-g.scej-online.jp", _server.AdvertisedAddress(IPAddress.Parse("200.1.1.1"), IPAddress.Parse("192.168.0.5")));
        _server.Options.PublicAddress = "177.1.2.3";
        Assert.Equal("177.1.2.3", _server.AdvertisedAddress(IPAddress.Parse("200.1.1.1"), IPAddress.Parse("192.168.0.5")));
        Assert.Contains("<gameurl1>http://1.2.3.4:18666/cgi-bin/</gameurl1>", _server.BuildInfoSs("1.2.3.4"));
    }

    [Fact]
    public void Party_code_roundtrip()
    {
        var code = PartyCode.Encode("Party do Miguel", ["26.1.2.3", "177.10.20.30", "192.168.0.10"]);
        Assert.True(PartyCode.TryDecode(code, out var name, out var addrs));
        Assert.Equal("Party do Miguel", name);
        Assert.Equal(["26.1.2.3", "177.10.20.30", "192.168.0.10"], addrs);
        Assert.True(PartyCode.TryDecode(" 177.10.20.30 ", out _, out var one));
        Assert.Equal(["177.10.20.30"], one);
        Assert.False(PartyCode.TryDecode("lixo", out _, out _));
    }
}

/// <summary>End-to-end over real sockets on non-default ports, speaking the game's HTTP dialect.</summary>
public class ServerSocketTests
{
    static async Task<string> RawRequest(int port, string path, byte[] body)
    {
        using var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, port);
        var s = c.GetStream();
        var head = Encoding.ASCII.GetBytes($"POST {path} HTTP/1.1\r\nHost: ds-eu-g.scej-online.jp\r\nContent-Length: {body.Length}\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n");
        await s.WriteAsync(head.Concat(body).ToArray());
        using var r = new StreamReader(s, Encoding.Latin1);
        return await r.ReadToEndAsync();
    }

    [Fact]
    public async Task Bootstrap_login_and_custom_endpoints()
    {
        var dir = Path.Combine(Path.GetTempPath(), "descoop-sock-" + Guid.NewGuid().ToString("N"));
        var o = new DesServerOptions { DataDir = dir, BootstrapPort = 28000, PortUS = 28666, PortEU = 28667, PortJP = 28668 };
        using var server = new DesServer(o);
        server.Start();

        var boot = await RawRequest(28000, "/~demons-souls/ss.info", Protocol.Encrypt("ver=100"u8.ToArray()));
        var b64 = boot.Split("\r\n\r\n", 2)[1];
        Assert.DoesNotContain("\n", b64);
        var info = Encoding.Latin1.GetString(Convert.FromBase64String(b64));
        Assert.Contains("<gameurl2>http://127.0.0.1:28667/cgi-bin/</gameurl2>", info);

        var login = await RawRequest(28667, "/cgi-bin/login.spd", Protocol.Encrypt("ver=100&characterID=Tester&"u8.ToArray()));
        var payload = Convert.FromBase64String(login.Split("\r\n\r\n", 2)[1].TrimEnd('\n'));
        Assert.Equal(0x02, payload[0]);
        Assert.Equal(payload.Length, BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1)));
        Assert.Contains("Seamless", Encoding.Latin1.GetString(payload));

        using var http = new HttpClient();
        var hello = await http.GetStringAsync("http://127.0.0.1:28000/descoop/hello?via=127.0.0.1&name=x");
        Assert.Contains("\"app\":\"descoop\"", hello);
        var status = await http.GetStringAsync("http://127.0.0.1:28000/descoop/status");
        Assert.Contains("players", status);
        try { Directory.Delete(dir, true); } catch { }
    }
}
