using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace DesCoop.Net;

/// <summary>
/// Just enough of the RPCN protocol (RPCS3's PSN replacement) to create an account, resend the
/// validation token and verify a login, so nobody has to dig through RPCS3's menus.
/// Wire format mirrors rpcs3/Emu/NP/rpcn_client.cpp.
/// </summary>
public sealed class RpcnClient : IAsyncDisposable
{
    public const string DefaultHost = "np.rpcs3.net";
    public const int DefaultPort = 31313;
    public const uint ProtocolVersion = 32;
    const int HeaderSize = 15;
    const string Salt = "No matter where you go, everybody's connected.";
    const string AvatarUrl = "https://rpcs3.net/cdn/netplay/DefaultAvatar.png";

    enum Command : ushort { Login = 0, Create = 2, SendToken = 4 }

    public enum Error : byte
    {
        NoError, Malformed, Invalid, InvalidInput, TooSoon, LoginError, LoginAlreadyLoggedIn, LoginInvalidUsername,
        LoginInvalidPassword, LoginInvalidToken, CreationError, CreationExistingUsername, CreationBannedEmailProvider,
        CreationExistingEmail, Unknown = 255,
    }

    readonly TcpClient _tcp;
    readonly SslStream _ssl;
    ulong _nextId = 1;
    public uint ServerVersion { get; private set; }

    RpcnClient(TcpClient tcp, SslStream ssl) { _tcp = tcp; _ssl = ssl; }

    public static async Task<RpcnClient> ConnectAsync(string host = DefaultHost, int port = DefaultPort, CancellationToken ct = default)
    {
        var tcp = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await tcp.ConnectAsync(host, port, timeout.Token);
        // RPCS3 itself does not verify the RPCN certificate (SSL_VERIFY_NONE); match it.
        var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, timeout.Token);
        var c = new RpcnClient(tcp, ssl);
        var (type, _, data) = await c.ReadPacketAsync(timeout.Token);
        if (type != 3 || data.Length != 4) throw new IOException("RPCN did not send its server info.");
        c.ServerVersion = BinaryPrimitives.ReadUInt32LittleEndian(data);
        return c;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _ssl.DisposeAsync(); } catch { }
        _tcp.Dispose();
    }

    /// <summary>RPCS3 never stores or sends the plain password: PBKDF2-HMAC-SHA3-256, 200k rounds, uppercase hex.</summary>
    public static string DerivePassword(string password)
    {
        var gen = new Pkcs5S2ParametersGenerator(new Sha3Digest(256));
        gen.Init(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(Salt), 200_000);
        var key = ((KeyParameter)gen.GenerateDerivedMacParameters(256)).GetKey();
        return Convert.ToHexString(key);
    }

    public static bool IsValidUsername(string s) =>
        s.Length is >= 3 and <= 16 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    public static bool IsValidToken(string s) => s.Length == 16 && s.All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c));

    public Task<Error> CreateAccountAsync(string npid, string derivedPassword, string email, CancellationToken ct = default) =>
        RequestAsync(Command.Create, ct, npid, derivedPassword, npid, AvatarUrl, email);

    public Task<Error> ResendTokenAsync(string npid, string derivedPassword, CancellationToken ct = default) =>
        RequestAsync(Command.SendToken, ct, npid, derivedPassword);

    public Task<Error> LoginAsync(string npid, string derivedPassword, string token, CancellationToken ct = default) =>
        RequestAsync(Command.Login, ct, npid, derivedPassword, token);

    async Task<Error> RequestAsync(Command cmd, CancellationToken ct, params string[] fields)
    {
        var body = new List<byte>();
        foreach (var f in fields) { body.AddRange(Encoding.UTF8.GetBytes(f)); body.Add(0); }
        ulong id = _nextId++;
        var packet = new byte[HeaderSize + body.Count];
        packet[0] = 0; // request
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(1), (ushort)cmd);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(3), (uint)packet.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(7), id);
        body.CopyTo(packet, HeaderSize);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await _ssl.WriteAsync(packet, timeout.Token);
        await _ssl.FlushAsync(timeout.Token);
        while (true)
        {
            var (type, pid, data) = await ReadPacketAsync(timeout.Token);
            if (type == 1 && pid == id) return data.Length > 0 ? (Error)data[0] : Error.Unknown;
            // notifications / other replies are ignored
        }
    }

    async Task<(byte type, ulong id, byte[] data)> ReadPacketAsync(CancellationToken ct)
    {
        var header = new byte[HeaderSize];
        await _ssl.ReadExactlyAsync(header, ct);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(3));
        if (size < HeaderSize || size > 16 << 20) throw new IOException("Bad RPCN packet.");
        var data = new byte[size - HeaderSize];
        await _ssl.ReadExactlyAsync(data, ct);
        return (header[0], BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(7)), data);
    }

    public static string Describe(Error e) => e switch
    {
        Error.NoError => "OK",
        Error.CreationExistingUsername => "That username is already taken.",
        Error.CreationExistingEmail => "An account with that email already exists.",
        Error.CreationBannedEmailProvider => "That email provider is not accepted by RPCN. Try Gmail/Outlook.",
        Error.LoginInvalidUsername => "Unknown username.",
        Error.LoginInvalidPassword => "Wrong password.",
        Error.LoginInvalidToken => "Wrong or missing token. Check your email (and the spam folder).",
        Error.LoginAlreadyLoggedIn => "This account is already online somewhere else (close RPCS3 and retry).",
        Error.TooSoon => "Too soon, wait a minute and try again.",
        Error.InvalidInput => "Invalid input.",
        _ => $"RPCN error: {e}",
    };
}
