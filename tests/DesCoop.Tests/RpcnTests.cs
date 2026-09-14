using DesCoop.Emu;
using DesCoop.Net;

namespace DesCoop.Tests;

public class RpcnTests
{
    [Fact]
    public void Password_derivation_matches_rpcs3()
    {
        // Reference computed with Python: hashlib.pbkdf2_hmac('sha3_256', b'hunter2', SALT, 200000).hex().upper()
        Assert.Equal("16ED8CE157D7D666E5BA6CE7B5038B9227DB846A92F35050B72C73BC1691363C", RpcnClient.DerivePassword("hunter2"));
    }

    [Fact]
    public void Validation_rules()
    {
        Assert.True(RpcnClient.IsValidUsername("Mingal_01"));
        Assert.False(RpcnClient.IsValidUsername("ab"));
        Assert.False(RpcnClient.IsValidUsername("has space"));
        Assert.True(RpcnClient.IsValidToken("ABCDEF0123456789"));
        Assert.False(RpcnClient.IsValidToken("abcdef0123456789"));
    }

    [Fact]
    public void Account_is_written_like_rpcs3_does()
    {
        var root = Path.Combine(Path.GetTempPath(), "descoop-rpcn-" + Guid.NewGuid().ToString("N"));
        try
        {
            var m = new Rpcs3Manager(root);
            m.SaveRpcnAccount("Tester", RpcnClient.DerivePassword("x"), "ABCDEF0123456789");
            Assert.Equal("Tester", m.RpcnUser());
            var text = File.ReadAllText(Path.Combine(root, "config", "rpcn.yml"));
            Assert.Contains("Version: 2", text);
            Assert.Contains("Token: ABCDEF0123456789", text);
            Assert.Contains("Host: np.rpcs3.net", text);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Official_server_handshake()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESCOOP_IT_DIR"))) return;
        await using var c = await RpcnClient.ConnectAsync();
        Assert.Equal(RpcnClient.ProtocolVersion, c.ServerVersion);
        Assert.Equal(RpcnClient.Error.LoginInvalidUsername,
            await c.LoginAsync("zz_nobody_" + Random.Shared.Next(100000), RpcnClient.DerivePassword("x"), ""));
    }
}
