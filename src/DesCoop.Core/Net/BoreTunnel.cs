using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DesCoop.Net;

/// <summary>
/// Minimal client for bore (github.com/ekzhang/bore), a free open TCP relay: exposes a local port
/// as bore.pub:&lt;random port&gt; so a friend can reach the host even behind NAT/CGNAT without
/// UPnP, port forwarding or a VPN. Protocol: null-delimited JSON on TCP 7835.
/// </summary>
public sealed class BoreTunnel : IAsyncDisposable
{
    public const string DefaultServer = "bore.pub";
    const int ControlPort = 7835;

    readonly TcpClient _control;
    readonly NetworkStream _stream;
    readonly CancellationTokenSource _cts = new();
    public string Server { get; }
    public int LocalPort { get; }
    public int RemotePort { get; }
    public bool Alive { get; private set; } = true;
    public event Action<BoreTunnel>? Closed;

    BoreTunnel(TcpClient control, string server, int localPort, int remotePort)
    {
        _control = control;
        _stream = control.GetStream();
        Server = server;
        LocalPort = localPort;
        RemotePort = remotePort;
    }

    public static async Task<BoreTunnel> OpenAsync(int localPort, string server = DefaultServer, CancellationToken ct = default)
    {
        var tcp = new TcpClient { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            await tcp.ConnectAsync(server, ControlPort, timeout.Token);
            var s = tcp.GetStream();
            await SendAsync(s, "{\"Hello\":0}", timeout.Token);
            using var reply = await ReceiveAsync(s, timeout.Token) ?? throw new IOException("relay closed the connection");
            var root = reply.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Hello", out var port))
            {
                var t = new BoreTunnel(tcp, server, localPort, port.GetInt32());
                _ = t.ControlLoop();
                return t;
            }
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Error", out var err)) throw new IOException("relay: " + err.GetString());
            throw new IOException("relay: unexpected reply (authentication required?)");
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    static async Task SendAsync(Stream s, string json, CancellationToken ct)
    {
        await s.WriteAsync(Encoding.UTF8.GetBytes(json + "\0"), ct);
        await s.FlushAsync(ct);
    }

    static async Task<JsonDocument?> ReceiveAsync(Stream s, CancellationToken ct)
    {
        var buf = new List<byte>(64);
        var one = new byte[1];
        while (true)
        {
            if (await s.ReadAsync(one, ct) == 0) return null;
            if (one[0] == 0) break;
            buf.Add(one[0]);
            if (buf.Count > 256) throw new IOException("relay frame too long");
        }
        return JsonDocument.Parse(buf.ToArray());
    }

    async Task ControlLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                using var msg = await ReceiveAsync(_stream, _cts.Token);
                if (msg == null) break;
                var root = msg.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Connection", out var idProp))
                {
                    // Read the id now: the JSON document is disposed at the end of this iteration.
                    string id = idProp.GetString()!;
                    _ = Task.Run(() => AcceptAsync(id));
                }
                // "Heartbeat" and anything else: ignore
            }
        }
        catch { }
        Alive = false;
        if (!_cts.IsCancellationRequested) Closed?.Invoke(this);
    }

    async Task AcceptAsync(string id)
    {
        using var remote = new TcpClient { NoDelay = true };
        using var local = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await remote.ConnectAsync(Server, ControlPort, timeout.Token);
            var rs = remote.GetStream();
            await SendAsync(rs, $"{{\"Accept\":\"{id}\"}}", timeout.Token);
            await local.ConnectAsync("127.0.0.1", LocalPort, timeout.Token);
            var ls = local.GetStream();
            var up = rs.CopyToAsync(ls, _cts.Token).Observe();
            var down = ls.CopyToAsync(rs, _cts.Token).Observe();
            await Task.WhenAny(up, down);
            await Task.WhenAny(Task.WhenAll(up, down), Task.Delay(3000));
        }
        catch { }
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _control.Dispose();
        return ValueTask.CompletedTask;
    }
}
