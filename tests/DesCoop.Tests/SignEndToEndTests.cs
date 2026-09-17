using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using DesCoop.Net;
using DesCoop.Server;
using Xunit.Abstractions;

namespace DesCoop.Tests;

/// <summary>
/// Full path over real TCP + HTTP + AES, with two clients on loopback: the host connects directly and the
/// friend arrives with the relay's X-DesCoop-Client header (exactly how the two players reach a hosted game).
/// Proves that a blue sign the friend places is returned to the host by getSosData.
/// </summary>
public class SignEndToEndTests(ITestOutputHelper output) : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "descoop-e2e-" + Guid.NewGuid().ToString("N"));
    DesServer? _server;
    int _port;

    public void Dispose() { _server?.Dispose(); try { Directory.Delete(_dir, true); } catch { } }

    void Start()
    {
        // High ports so a running app on 18666 doesn't clash.
        int b = 29000 + new Random().Next(0, 400);
        _server = new DesServer(new DesServerOptions { DataDir = _dir, BootstrapPort = b, PortUS = b + 1, PortEU = b + 2, PortJP = b + 3 });
        _server.Log += output.WriteLine;
        _server.Start();
        _port = b + 1; // US
    }

    /// <summary>One encrypted DeS request over TCP; returns the decoded (cmd, data). relayId != null tags it like the friend's forwarder.</summary>
    (byte cmd, byte[] data) Call(string spd, string form, string? relayId)
    {
        using var c = new TcpClient();
        c.Connect(System.Net.IPAddress.Loopback, _port);
        var body = Protocol.Encrypt(Protocol.Raw.GetBytes(form));
        var head = new StringBuilder();
        head.Append($"POST /cgi-bin/{spd} HTTP/1.1\r\nHost: localhost\r\n");
        if (relayId != null) head.Append($"{RelayForwarder.ClientHeader}: {relayId}\r\n");
        head.Append($"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        var s = c.GetStream();
        s.Write(Protocol.Raw.GetBytes(head.ToString()));
        s.Write(body);
        s.Flush();
        var ms = new MemoryStream();
        var buf = new byte[4096];
        int n;
        while ((n = s.Read(buf)) > 0) ms.Write(buf, 0, n);
        var all = Protocol.Raw.GetString(ms.ToArray());
        int hdr = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var payload = Convert.FromBase64String(all[(hdr + 4)..].Trim());
        byte cmd = payload[0];
        return (cmd, payload[5..]);
    }

    static string Sign(string who, int block, float x) =>
        $"characterID={who}&blockID={(uint)block}&posx={x}&posy=1.5&posz=3&angx=0&angy=0.5&angz=0" +
        "&messageID=0&mainMsgID=0&addMsgCateID=0&playerInfo=info&qwcwb=0&qwclr=0&isBlack=2&playerLevel=30";

    [Fact]
    public void Host_sees_the_friends_blue_sign_over_the_real_wire()
    {
        Start();
        // Friend (via relay) announces itself and places a blue sign in 1-1 (block 10010), a few steps from the host.
        Call("initializeCharacter.spd", "characterID=Friend&index=0", "friend-relay");
        Call("addSosData.spd", Sign("Friend0", 10010, 50f), "friend-relay");

        // Host (direct loopback) is in the same area; a ghost upload gives the server the host's position (the anchor).
        Call("initializeCharacter.spd", "characterID=Host&index=0", null);
        var replay = Protocol.EncodeGameBase64(ServerTests.MakeReplay([10f, 1.5f, 20f, 0f, 0.5f, 0f]));
        Call("setWanderingGhost.spd", $"characterID=Host0&ghostBlockID=10010&replayData={replay}", null);

        // Host reads signs for 1-1. The real game asks for sosNum=0, which must still return the sign.
        var (cmd, data) = Call("getSosData.spd", "characterID=Host0&blockID=10010&sosNum=0&sosList=", null);
        Assert.Equal(0x0f, cmd);

        int o = 0;
        uint U() { var v = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o)); o += 4; return v; }
        float Fl() { var v = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(o)); o += 4; return v; }
        string S() { int st = o; while (data[o] != 0) o++; var r = Encoding.Latin1.GetString(data, st, o - st); o++; return r; }
        uint known = U();
        for (int i = 0; i < known; i++) U();
        uint fresh = U();
        Assert.True(fresh >= 1, "host should receive the friend's sign");
        uint sosId = U();
        string who = S();
        float x = Fl(), y = Fl(), z = Fl();
        output.WriteLine($"host received sign #{sosId} from {who} at ({x:0.0},{y:0.0},{z:0.0})");
        Assert.Equal("Friend0", who);
        // The sign was moved right next to the host's anchor (10,1.5,20), not left at x=50.
        Assert.True(Math.Abs(x - 10) < 3, $"sign should be next to the host, got x={x}");
    }
}
