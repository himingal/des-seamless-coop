using DesCoop.Net;
using DesCoop.Server;

namespace DesCoop.Party;

/// <summary>Runs the host side: local DeS server + router port mapping + shareable code.</summary>
public sealed class PartyHost : IAsyncDisposable
{
    public DesServer Server { get; }
    public string Code { get; private set; } = "";
    public string? PublicIp { get; private set; }
    public bool UpnpOk { get; private set; }
    public List<LocalAddress> LocalAddresses { get; private set; } = [];
    Upnp? _upnp;

    public event Action<string>? Log;

    public PartyHost(DesServerOptions options)
    {
        Server = new DesServer(options);
        Server.Log += m => Log?.Invoke(m);
    }

    public async Task StartAsync(bool useUpnp, CancellationToken ct = default)
    {
        Server.Start();
        LocalAddresses = NetUtil.GetLocalAddresses();

        if (useUpnp)
        {
            Log?.Invoke("Procurando roteador (UPnP) para abrir as portas...");
            _upnp = await Upnp.DiscoverAsync(TimeSpan.FromSeconds(3), ct);
            if (_upnp != null)
            {
                try
                {
                    foreach (var p in NetUtil.AllPorts) await _upnp.AddTcpMappingAsync(p, "DeS Seamless Coop");
                    UpnpOk = true;
                    PublicIp = await _upnp.GetExternalIpAsync();
                    Log?.Invoke($"Portas abertas no roteador via UPnP ({_upnp.LocalAddress}).");
                }
                catch (Exception ex) { Log?.Invoke("UPnP recusou abrir as portas: " + ex.Message); }
            }
            else Log?.Invoke("Roteador sem UPnP. Se o amigo nao conectar, use Radmin VPN/ZeroTier ou abra as portas TCP 18000 e 18666-18668.");
        }

        var web = await NetUtil.GetPublicIpAsync(ct);
        if (PublicIp != null && web != null && PublicIp != web)
            Log?.Invoke($"Aviso: seu roteador tem IP {PublicIp} mas a internet te ve como {web} (CGNAT). Use Radmin VPN/ZeroTier/Tailscale.");
        PublicIp = web ?? PublicIp;
        if (PublicIp != null) Server.Options.PublicAddress = PublicIp;

        var addrs = new List<string>();
        addrs.AddRange(LocalAddresses.Where(a => a.Kind != "LAN").Select(a => a.Address));
        if (PublicIp != null) addrs.Add(PublicIp);
        addrs.AddRange(LocalAddresses.Where(a => a.Kind == "LAN").Select(a => a.Address));
        Code = PartyCode.Encode(Server.Options.ServerName, addrs);
    }

    public async ValueTask DisposeAsync()
    {
        if (_upnp != null && UpnpOk)
            foreach (var p in NetUtil.AllPorts) await _upnp.RemoveTcpMappingAsync(p);
        Server.Dispose();
    }
}
