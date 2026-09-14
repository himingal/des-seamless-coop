using System.Net.Http.Json;
using System.Text.Json;
using DesCoop.Net;
using DesCoop.Server;

namespace DesCoop.Tests;

public class RelayTests
{
    [Fact]
    public void Invite_is_sealed_with_the_password()
    {
        var inv = new PartyInvite("mingalDES", ["26.1.2.3"], "bore.pub", new() { [18000] = 40001 }, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var sealedText = Rendezvous.Seal(inv, "MingalDES", "4821");
        Assert.DoesNotContain("26.1.2.3", sealedText);
        var back = Rendezvous.Open(sealedText, "mingaldes", "4821");
        Assert.NotNull(back);
        Assert.Equal(40001, back!.RelayPorts![18000]);
        Assert.Null(Rendezvous.Open(sealedText, "mingaldes", "0000"));
        Assert.Equal(Rendezvous.Topic("MingalDES", "4821"), Rendezvous.Topic(" mingaldes ", "4821"));
        Assert.NotEqual(Rendezvous.Topic("mingaldes", "4821"), Rendezvous.Topic("mingaldes", "4822"));
    }

    /// <summary>Real bore.pub + ntfy.sh round trip: server on custom ports, reached only through the relay.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Friend_reaches_host_through_public_relay_and_directory()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESCOOP_IT_DIR"))) return;
        var dir = Path.Combine(Path.GetTempPath(), "descoop-relay-" + Guid.NewGuid().ToString("N"));
        var o = new DesServerOptions { DataDir = dir, ServerName = "it-" + Guid.NewGuid().ToString("N")[..8], BootstrapPort = 29000, PortUS = 29666, PortEU = 29667, PortJP = 29668 };
        using var server = new DesServer(o);
        server.Start();

        var tunnels = new List<BoreTunnel>();
        try
        {
            foreach (var p in new[] { 29000, 29666 }) tunnels.Add(await BoreTunnel.OpenAsync(p));
            var ports = tunnels.ToDictionary(t => t.LocalPort, t => t.RemotePort);

            string pass = Rendezvous.NewPassword();
            await Rendezvous.PublishAsync(new PartyInvite(o.ServerName, [], BoreTunnel.DefaultServer, ports, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), o.ServerName, pass);
            PartyInvite? found = null;
            for (int i = 0; i < 5 && found == null; i++) { found = await Rendezvous.FindAsync(o.ServerName, pass); if (found == null) await Task.Delay(1000); }
            Assert.NotNull(found);

            // Friend side forwards local 39000 -> relay port of 29000.
            using var fwd = new RelayForwarder(found!.Relay!, new() { [39000] = found.RelayPorts![29000], [39666] = found.RelayPorts[29666] }, "FriendX");
            fwd.Start();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var hello = await http.GetStringAsync("http://127.0.0.1:39000/descoop/hello?via=127.0.0.1&name=FriendX");
            Assert.Contains("\"app\":\"descoop\"", hello);

            // A game request through the relay is attributed to the relayed friend, not to loopback.
            var body = Protocol.Encrypt("ver=100&characterID=FriendX&index=0&"u8.ToArray());
            using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:39666/cgi-bin/initializeCharacter.spd") { Content = new ByteArrayContent(body) };
            var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            var payload = Convert.FromBase64String(text.TrimEnd('\n'));
            Assert.Equal(0x17, payload[0]);
            Assert.Contains(server.GetStatus().Players, p => p.Name == "FriendX");
        }
        finally
        {
            foreach (var t in tunnels) await t.DisposeAsync();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
