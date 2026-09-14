using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DesCoop.Net;

/// <summary>
/// Friend side of the relay: the game talks to 127.0.0.1:18000/18666-18668 and every connection is
/// piped to the host through the public relay. A header identifies this player, since every relayed
/// connection reaches the host's server from the same loopback address.
/// </summary>
public sealed class RelayForwarder : IDisposable
{
    public const string ClientHeader = "X-DesCoop-Client";
    readonly string _relayHost;
    readonly Dictionary<int, int> _ports;
    readonly string _clientId;
    readonly CancellationTokenSource _cts = new();
    readonly List<TcpListener> _listeners = [];

    public event Action<string>? Log;

    public RelayForwarder(string relayHost, Dictionary<int, int> ports, string clientId)
    {
        _relayHost = relayHost;
        _ports = ports;
        _clientId = new string(clientId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (_clientId.Length == 0) _clientId = Guid.NewGuid().ToString("N")[..12];
    }

    public void Start()
    {
        foreach (var (local, remote) in _ports)
        {
            var l = new TcpListener(IPAddress.Loopback, local);
            l.Start(64);
            _listeners.Add(l);
            _ = AcceptLoop(l, remote);
        }
    }

    async Task AcceptLoop(TcpListener l, int remotePort)
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient c;
            try { c = await l.AcceptTcpClientAsync(_cts.Token); } catch { return; }
            _ = Task.Run(() => HandleAsync(c, remotePort));
        }
    }

    async Task HandleAsync(TcpClient local, int remotePort)
    {
        using var _ = local;
        using var remote = new TcpClient();
        try
        {
            await remote.ConnectAsync(_relayHost, remotePort, _cts.Token);
            var ls = local.GetStream();
            var rs = remote.GetStream();

            // Copy the request line, then inject our identity header.
            var first = new List<byte>();
            var one = new byte[1];
            while (first.Count < 4096 && await ls.ReadAsync(one, _cts.Token) == 1)
            {
                first.Add(one[0]);
                if (first.Count >= 2 && first[^2] == '\r' && first[^1] == '\n') break;
            }
            await rs.WriteAsync(first.ToArray(), _cts.Token);
            await rs.WriteAsync(Encoding.ASCII.GetBytes($"{ClientHeader}: {_clientId}\r\n"), _cts.Token);

            var up = ls.CopyToAsync(rs, _cts.Token).Observe();
            var down = rs.CopyToAsync(ls, _cts.Token).Observe();
            await Task.WhenAny(up, down);
            await Task.WhenAny(down, Task.Delay(3000));
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            Log?.Invoke("Relay connection failed: " + ex.Message);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        foreach (var l in _listeners) { try { l.Stop(); } catch { } }
    }
}
