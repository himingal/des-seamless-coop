using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DesCoop.Server;

/// <summary>
/// Wire format of the Demon's Souls online server (reverse engineered by ymgve's DeSSE).
/// Requests: HTTP POST whose body is AES-256-CBC (IV prefixed) of a form string.
/// Responses: base64 of [cmd:u8][len:u32 LE][data], followed by a newline.
/// All strings are treated as raw bytes (Latin-1) so nothing gets mangled.
/// </summary>
public static class Protocol
{
    static readonly byte[] Key = Encoding.ASCII.GetBytes("11111111222222223333333344444444");
    public static readonly Encoding Raw = Encoding.Latin1;

    public static byte[] Decrypt(byte[] body)
    {
        if (body.Length < 32) return [];
        var iv = body.AsSpan(0, 16).ToArray();
        int len = (body.Length - 16) / 16 * 16;
        using var aes = Aes.Create();
        aes.Key = Key;
        var pt = aes.DecryptCbc(body.AsSpan(16, len), iv, PaddingMode.None);
        int pad = pt.Length > 0 ? pt[^1] : 0;
        if (pad is >= 1 and <= 16 && pad <= pt.Length) pt = pt[..^pad];
        return pt;
    }

    public static byte[] Encrypt(byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        var iv = RandomNumberGenerator.GetBytes(16);
        var ct = aes.EncryptCbc(plain, iv, PaddingMode.PKCS7);
        return [.. iv, .. ct];
    }

    public static Dictionary<string, string> ParseParams(byte[] plain)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in Raw.GetString(plain).Split('&'))
        {
            if (part.Length == 0 || part == "\0") continue;
            int eq = part.IndexOf('=');
            if (eq < 0) continue;
            result[part[..eq]] = part[(eq + 1)..];
        }
        return result;
    }

    public static byte[] BuildPayload(byte cmd, byte[] data)
    {
        var buf = new byte[5 + data.Length];
        buf[0] = cmd;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1), (uint)(data.Length + 5));
        data.CopyTo(buf, 5);
        return buf;
    }

    // The trailing newline is required for normal responses and must be absent for the bootstrap one.
    public static byte[] BuildResponse(byte cmd, byte[] data) =>
        HttpOk(Raw.GetBytes(Convert.ToBase64String(BuildPayload(cmd, data)) + "\n"), "text/html; charset=UTF-8");

    public static byte[] BuildBootstrap(string infoSs) =>
        HttpOk(Raw.GetBytes(Convert.ToBase64String(Raw.GetBytes(infoSs))), "text/html; charset=UTF-8");

    public static byte[] HttpOk(byte[] body, string contentType, int status = 200)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n");
        sb.Append("Date: ").Append(DateTime.UtcNow.ToString("r")).Append("\r\n");
        sb.Append("Server: Apache\r\n");
        sb.Append("Content-Length: ").Append(body.Length).Append("\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n\r\n");
        return [.. Raw.GetBytes(sb.ToString()), .. body];
    }

    /// <summary>The game base64-encodes binary params but sends '+' as ' ' and may truncate padding.</summary>
    public static byte[] DecodeBrokenBase64(string data)
    {
        var sb = new StringBuilder(data.Length + 3);
        foreach (char c in data)
        {
            if (char.IsAsciiLetterOrDigit(c) || c == '/' || c == '+') sb.Append(c);
            else if (c == ' ') sb.Append('+');
            else break;
        }
        switch (sb.Length % 4)
        {
            case 3: sb.Append('='); break;
            case 2: sb.Append("=="); break;
            case 1: sb.Append("A=="); break;
        }
        return Convert.FromBase64String(sb.ToString());
    }

    public static string EncodeGameBase64(byte[] data) => Convert.ToBase64String(data).Replace('+', ' ');

    public static int ToSigned(string s)
    {
        long n = long.Parse(s.Trim('\0'), System.Globalization.CultureInfo.InvariantCulture);
        return unchecked((int)(uint)(n & 0xFFFFFFFF));
    }

    public static float F(string s) => float.Parse(s.Trim('\0'), System.Globalization.CultureInfo.InvariantCulture);
    public static int I(string s) => int.Parse(s.Trim('\0'), System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Little-endian binary builder for response payloads.</summary>
public sealed class Payload
{
    readonly MemoryStream _ms = new();
    public Payload U8(byte v) { _ms.WriteByte(v); return this; }
    public Payload I32(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); _ms.Write(b); return this; }
    public Payload U32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); _ms.Write(b); return this; }
    public Payload F32(float v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, v); _ms.Write(b); return this; }
    public Payload CStr(string s) { _ms.Write(Protocol.Raw.GetBytes(s)); _ms.WriteByte(0); return this; }
    public Payload Str(string s) { _ms.Write(Protocol.Raw.GetBytes(s)); return this; }
    public Payload Bytes(byte[] b) { _ms.Write(b); return this; }
    public byte[] ToArray() => _ms.ToArray();
}
