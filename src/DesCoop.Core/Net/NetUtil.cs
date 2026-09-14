using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace DesCoop.Net;

public sealed record LocalAddress(string Address, string Adapter, string Kind);

public static class NetUtil
{
    public const int BootstrapPort = 18000;
    public static readonly int[] AllPorts = [18000, 18666, 18667, 18668];

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    static NetUtil() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("DesCoop/1.0");

    /// <summary>IPv4 addresses of this PC, VPN adapters (Radmin, Hamachi, ZeroTier, Tailscale) first.</summary>
    public static List<LocalAddress> GetLocalAddresses()
    {
        var list = new List<LocalAddress>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = ua.Address.GetAddressBytes();
                if (b[0] == 169 && b[1] == 254) continue;
                string desc = (ni.Name + " " + ni.Description).ToLowerInvariant();
                string kind =
                    desc.Contains("radmin") || b[0] == 26 ? "Radmin VPN" :
                    desc.Contains("hamachi") || b[0] == 25 ? "Hamachi" :
                    desc.Contains("zerotier") ? "ZeroTier" :
                    desc.Contains("tailscale") || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ? "Tailscale" :
                    "LAN";
                list.Add(new LocalAddress(ua.Address.ToString(), ni.Name, kind));
            }
        }
        return [.. list.OrderBy(a => a.Kind == "LAN" ? 1 : 0)];
    }

    public static async Task<string?> GetPublicIpAsync(CancellationToken ct = default)
    {
        foreach (var url in new[] { "https://api.ipify.org", "https://ifconfig.me/ip", "https://icanhazip.com" })
        {
            try
            {
                var s = (await Http.GetStringAsync(url, ct)).Trim();
                if (IPAddress.TryParse(s, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork) return s;
            }
            catch { }
        }
        return null;
    }

    public sealed record PingResult(string Address, string Name, string Version, string? YourIp);

    /// <summary>Checks that a DeS Seamless Co-op server answers at <paramref name="address"/> and tells it how we reach it.</summary>
    public static async Task<PingResult?> HelloAsync(string address, string? playerName, CancellationToken ct = default)
    {
        try
        {
            var url = $"http://{address}:{BootstrapPort}/descoop/hello?via={Uri.EscapeDataString(address)}&name={Uri.EscapeDataString(playerName ?? "")}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
            var r = doc.RootElement;
            if (r.GetProperty("app").GetString() != "descoop") return null;
            return new PingResult(address, r.GetProperty("name").GetString() ?? "", r.GetProperty("version").GetString() ?? "",
                r.TryGetProperty("yourIp", out var y) ? y.GetString() : null);
        }
        catch { return null; }
    }

    /// <summary>Tries every address in parallel and returns the first one that answers.</summary>
    public static async Task<PingResult?> FindReachableAsync(IEnumerable<string> addresses, string? playerName, CancellationToken ct = default)
    {
        var tasks = addresses.Distinct().Select(a => HelloAsync(a, playerName, ct)).ToList();
        while (tasks.Count > 0)
        {
            var done = await Task.WhenAny(tasks);
            tasks.Remove(done);
            if (await done is { } ok) return ok;
        }
        return null;
    }

    public static async Task<Server.ServerStatus?> GetStatusAsync(string address, CancellationToken ct = default)
    {
        try
        {
            var json = await Http.GetStringAsync($"http://{address}:{BootstrapPort}/descoop/status", ct);
            return JsonSerializer.Deserialize<Server.ServerStatus>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    /// <summary>Local IPv4 used to reach <paramref name="remote"/> (no packets are sent).</summary>
    public static IPAddress? LocalAddressFor(IPAddress remote)
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect(remote, 1900);
            return ((IPEndPoint)s.LocalEndPoint!).Address;
        }
        catch { return null; }
    }
}
