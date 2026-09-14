using System.Net;
using System.Text;

namespace DesCoop.Net;

/// <summary>
/// Shareable party code: "DES-" + base64url("name|addr1,addr2,..."). The joiner probes every
/// address (public IP, VPN IPs, LAN) and keeps the first that answers.
/// A plain IP or hostname is accepted as a code too.
/// </summary>
public static class PartyCode
{
    const string Prefix = "DES-";

    public static string Encode(string name, IEnumerable<string> addresses)
    {
        var raw = Encoding.UTF8.GetBytes(name.Replace('|', ' ') + "|" + string.Join(",", addresses.Distinct()));
        return Prefix + Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? code, out string name, out List<string> addresses)
    {
        name = "";
        addresses = [];
        code = code?.Trim();
        if (string.IsNullOrEmpty(code)) return false;

        if (code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var b64 = code[Prefix.Length..].Trim().Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                var parts = text.Split('|', 2);
                if (parts.Length != 2) return false;
                name = parts[0];
                addresses = [.. parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(IsHost)];
                return addresses.Count > 0;
            }
            catch { return false; }
        }

        if (IsHost(code))
        {
            addresses = [code];
            return true;
        }
        return false;
    }

    static bool IsHost(string s) =>
        IPAddress.TryParse(s, out var ip) ? ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                          : Uri.CheckHostName(s) == UriHostNameType.Dns && s.Contains('.');
}
