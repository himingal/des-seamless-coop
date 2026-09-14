using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DesCoop.Net;

/// <summary>How to reach a host: direct addresses (LAN/VPN/public) plus a relay fallback.</summary>
public sealed record PartyInvite(string Name, string[] Direct, string? Relay, Dictionary<int, int>? RelayPorts, long Ts)
{
    public bool IsFresh => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Ts < 1800;
}

/// <summary>
/// Lets a friend find a party by just its name and password. The host publishes an encrypted
/// invite to a public pub/sub topic (ntfy.sh) derived from name+password; nothing readable leaves the PC.
/// </summary>
public static class Rendezvous
{
    const string Server = "https://ntfy.sh";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static Rendezvous() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("DesCoop/1.2");

    static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public static string Topic(string name, string password)
    {
        var h = SHA256.HashData(Encoding.UTF8.GetBytes("descoop|" + Normalize(name) + "|" + password.Trim()));
        return "descoop-" + Convert.ToHexString(h)[..32].ToLowerInvariant();
    }

    static byte[] Key(string name, string password) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password.Trim()), Encoding.UTF8.GetBytes("descoop-key|" + Normalize(name)), 50_000, HashAlgorithmName.SHA256, 32);

    internal static string Seal(PartyInvite invite, string name, string password)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(invite, Json);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(Key(name, password), 16);
        gcm.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    internal static PartyInvite? Open(string sealedText, string name, string password)
    {
        try
        {
            var all = Convert.FromBase64String(sealedText.Trim());
            if (all.Length < 29) return null;
            var plain = new byte[all.Length - 28];
            using var gcm = new AesGcm(Key(name, password), 16);
            gcm.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain);
            return JsonSerializer.Deserialize<PartyInvite>(plain, Json);
        }
        catch { return null; }
    }

    public static async Task PublishAsync(PartyInvite invite, string name, string password, CancellationToken ct = default)
    {
        using var content = new StringContent(Seal(invite, name, password), Encoding.ASCII, "text/plain");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Server}/{Topic(name, password)}") { Content = content };
        req.Headers.TryAddWithoutValidation("Cache", "yes");
        using var resp = await Http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>Newest valid invite published in the last 30 minutes, or null.</summary>
    public static async Task<PartyInvite?> FindAsync(string name, string password, CancellationToken ct = default)
    {
        var text = await Http.GetStringAsync($"{Server}/{Topic(name, password)}/json?poll=1&since=30m", ct);
        PartyInvite? best = null;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.GetProperty("event").GetString() != "message") continue;
                var inv = Open(doc.RootElement.GetProperty("message").GetString() ?? "", name, password);
                if (inv != null && inv.IsFresh && (best == null || inv.Ts > best.Ts)) best = inv;
            }
            catch { }
        }
        return best;
    }

    /// <summary>Short friendly password: 4 digits.</summary>
    public static string NewPassword() => RandomNumberGenerator.GetInt32(1000, 10000).ToString();
}
