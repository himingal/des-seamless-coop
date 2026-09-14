using DesCoop.Net;
using DesCoop.Server;

namespace DesCoop.Party;

/// <summary>
/// Host side: local DeS server + every way in we can offer (router UPnP, LAN/VPN addresses and a
/// public relay), published under the party name + password so the friend only types those two.
/// </summary>
public sealed class PartyHost : IAsyncDisposable
{
    public DesServer Server { get; }
    public string Name => Server.Options.ServerName;
    public string Password { get; }
    /// <summary>Legacy long code with direct addresses only.</summary>
    public string Code { get; private set; } = "";
    public string? PublicIp { get; private set; }
    public bool UpnpOk { get; private set; }
    public bool RelayOk => _tunnels.Count == NetUtil.AllPorts.Length && _tunnels.All(t => t.Alive);
    public bool Published { get; private set; }
    public List<LocalAddress> LocalAddresses { get; private set; } = [];

    readonly List<BoreTunnel> _tunnels = [];
    readonly List<string> _direct = [];
    readonly CancellationTokenSource _cts = new();
    Upnp? _upnp;
    Timer? _republish;

    public event Action<string>? Log;
    public event Action? StateChanged;

    public PartyHost(DesServerOptions options, string password)
    {
        Server = new DesServer(options);
        Server.Log += m => Log?.Invoke(m);
        Password = password;
    }

    public async Task StartAsync(bool useUpnp, bool useRelay = true, CancellationToken ct = default)
    {
        Server.Start();
        LocalAddresses = NetUtil.GetLocalAddresses();

        if (useUpnp)
        {
            Log?.Invoke("Looking for the router (UPnP) to open the ports...");
            _upnp = await Upnp.DiscoverAsync(TimeSpan.FromSeconds(3), ct);
            if (_upnp != null)
            {
                try
                {
                    foreach (var p in NetUtil.AllPorts) await _upnp.AddTcpMappingAsync(p, "DeS Seamless Coop");
                    UpnpOk = true;
                    PublicIp = await _upnp.GetExternalIpAsync();
                    Log?.Invoke($"Router ports opened via UPnP ({_upnp.LocalAddress}).");
                }
                catch (Exception ex) { Log?.Invoke("UPnP refused to open the ports: " + ex.Message); }
            }
            else Log?.Invoke("Router has no UPnP: using the relay instead.");
        }

        var web = await NetUtil.GetPublicIpAsync(ct);
        PublicIp = web ?? PublicIp;
        if (PublicIp != null) Server.Options.PublicAddress = PublicIp;

        _direct.AddRange(LocalAddresses.Where(a => a.Kind != "LAN").Select(a => a.Address));
        if (PublicIp != null && UpnpOk) _direct.Add(PublicIp);
        _direct.AddRange(LocalAddresses.Where(a => a.Kind == "LAN").Select(a => a.Address));
        var codeAddrs = new List<string>(_direct);
        if (PublicIp != null && !UpnpOk) codeAddrs.Add(PublicIp);
        Code = PartyCode.Encode(Name, codeAddrs);

        if (useRelay) await OpenRelayAsync(ct);
        await PublishAsync();
        _republish = new Timer(_ => _ = PublishAsync(), null, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(4));
    }

    async Task OpenRelayAsync(CancellationToken ct)
    {
        try
        {
            foreach (var port in NetUtil.AllPorts)
            {
                var t = await BoreTunnel.OpenAsync(port, ct: ct);
                t.Closed += OnTunnelClosed;
                lock (_tunnels) _tunnels.Add(t);
            }
            Log?.Invoke($"Relay online ({BoreTunnel.DefaultServer}): anyone with the party name + password can join.");
        }
        catch (Exception ex)
        {
            Log?.Invoke("Relay unavailable right now (" + ex.Message + "). Only direct connections will work.");
        }
    }

    void OnTunnelClosed(BoreTunnel dead) => _ = Task.Run(async () =>
    {
        Log?.Invoke("Relay connection dropped, reconnecting...");
        StateChanged?.Invoke();
        for (int attempt = 0; !_cts.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 2 + attempt * 3)), _cts.Token);
                var t = await BoreTunnel.OpenAsync(dead.LocalPort, dead.Server, _cts.Token);
                t.Closed += OnTunnelClosed;
                lock (_tunnels) { _tunnels.Remove(dead); _tunnels.Add(t); }
                Log?.Invoke("Relay back online.");
                await PublishAsync();
                StateChanged?.Invoke();
                return;
            }
            catch when (!_cts.IsCancellationRequested) { }
            catch { return; }
        }
    });

    async Task PublishAsync()
    {
        Dictionary<int, int>? ports = null;
        lock (_tunnels)
            if (_tunnels.Count > 0 && _tunnels.All(t => t.Alive))
                ports = _tunnels.ToDictionary(t => t.LocalPort, t => t.RemotePort);
        var invite = new PartyInvite(Name, [.. _direct], ports != null ? BoreTunnel.DefaultServer : null, ports,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        try
        {
            await Rendezvous.PublishAsync(invite, Name, Password, _cts.Token);
            if (!Published) Log?.Invoke($"Party \"{Name}\" is listed. Password: {Password}");
            Published = true;
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            Log?.Invoke("Could not list the party online (" + ex.Message + "). The long code still works.");
        }
        catch { }
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _republish?.Dispose();
        List<BoreTunnel> tunnels;
        lock (_tunnels) { tunnels = [.. _tunnels]; _tunnels.Clear(); }
        foreach (var t in tunnels) await t.DisposeAsync();
        if (_upnp != null && UpnpOk)
            foreach (var p in NetUtil.AllPorts) await _upnp.RemoveTcpMappingAsync(p);
        Server.Dispose();
    }
}
