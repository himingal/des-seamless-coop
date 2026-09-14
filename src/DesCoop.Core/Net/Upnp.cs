using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace DesCoop.Net;

/// <summary>Minimal UPnP IGD client: discovers the router and opens/closes TCP port mappings.</summary>
public sealed class Upnp
{
    static readonly string[] ServiceTypes =
    [
        "urn:schemas-upnp-org:service:WANIPConnection:2",
        "urn:schemas-upnp-org:service:WANIPConnection:1",
        "urn:schemas-upnp-org:service:WANPPPConnection:1",
    ];

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public string ControlUrl { get; }
    public string ServiceType { get; }
    public IPAddress LocalAddress { get; }

    Upnp(string controlUrl, string serviceType, IPAddress local)
    {
        ControlUrl = controlUrl;
        ServiceType = serviceType;
        LocalAddress = local;
    }

    public static async Task<Upnp?> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        var group = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
        foreach (var st in new[] { "urn:schemas-upnp-org:device:InternetGatewayDevice:1" }.Concat(ServiceTypes))
        {
            var msg = Encoding.ASCII.GetBytes($"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nST: {st}\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\n\r\n");
            await udp.SendAsync(msg, group, ct);
        }

        var seen = new HashSet<string>();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(deadline - DateTime.UtcNow);
            UdpReceiveResult res;
            try { res = await udp.ReceiveAsync(cts.Token); }
            catch (OperationCanceledException) { break; }

            var text = Encoding.ASCII.GetString(res.Buffer);
            var loc = text.Split("\r\n").FirstOrDefault(l => l.StartsWith("location:", StringComparison.OrdinalIgnoreCase));
            if (loc == null) continue;
            var url = loc[(loc.IndexOf(':') + 1)..].Trim();
            if (!seen.Add(url)) continue;
            var dev = await TryDeviceAsync(url, ct);
            if (dev != null) return dev;
        }
        return null;
    }

    static async Task<Upnp?> TryDeviceAsync(string location, CancellationToken ct)
    {
        try
        {
            var xml = XDocument.Parse(await Http.GetStringAsync(location, ct));
            XNamespace ns = xml.Root!.Name.Namespace;
            var baseUri = new Uri(location);
            foreach (var svc in xml.Descendants(ns + "service"))
            {
                var type = svc.Element(ns + "serviceType")?.Value.Trim();
                if (type == null || !ServiceTypes.Contains(type)) continue;
                var ctrl = svc.Element(ns + "controlURL")?.Value.Trim();
                if (string.IsNullOrEmpty(ctrl)) continue;
                var local = NetUtil.LocalAddressFor(IPAddress.Parse(baseUri.Host));
                if (local == null) continue;
                return new Upnp(new Uri(baseUri, ctrl).ToString(), type, local);
            }
        }
        catch { }
        return null;
    }

    async Task<XDocument> SoapAsync(string action, params (string name, string value)[] args)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>");
        sb.Append($"<u:{action} xmlns:u=\"{ServiceType}\">");
        foreach (var (n, v) in args) sb.Append($"<{n}>{SecurityElement.Escape(v)}</{n}>");
        sb.Append($"</u:{action}></s:Body></s:Envelope>");
        using var req = new HttpRequestMessage(HttpMethod.Post, ControlUrl) { Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/xml") };
        req.Headers.TryAddWithoutValidation("SOAPAction", $"\"{ServiceType}#{action}\"");
        using var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"UPnP {action} falhou ({(int)resp.StatusCode})");
        return XDocument.Parse(body);
    }

    public async Task<string?> GetExternalIpAsync()
    {
        try
        {
            var doc = await SoapAsync("GetExternalIPAddress");
            var ip = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "NewExternalIPAddress")?.Value.Trim();
            return IPAddress.TryParse(ip, out _) ? ip : null;
        }
        catch { return null; }
    }

    public Task AddTcpMappingAsync(int port, string description) => SoapAsync("AddPortMapping",
        ("NewRemoteHost", ""), ("NewExternalPort", port.ToString()), ("NewProtocol", "TCP"),
        ("NewInternalPort", port.ToString()), ("NewInternalClient", LocalAddress.ToString()),
        ("NewEnabled", "1"), ("NewPortMappingDescription", description), ("NewLeaseDuration", "0"));

    public async Task RemoveTcpMappingAsync(int port)
    {
        try { await SoapAsync("DeletePortMapping", ("NewRemoteHost", ""), ("NewExternalPort", port.ToString()), ("NewProtocol", "TCP")); }
        catch { }
    }
}
