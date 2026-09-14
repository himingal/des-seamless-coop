using System.Diagnostics;
using DesCoop.Emu;
using DesCoop.Net;

namespace DesCoop.Tests;

/// <summary>
/// Real downloads (RPCS3 + Sony firmware) and a real RPCS3 boot. Opt-in:
///   $env:DESCOOP_IT_DIR = "D:\some\empty\folder"; dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    static string? Dir => Environment.GetEnvironmentVariable("DESCOOP_IT_DIR");

    [Fact]
    public async Task Install_rpcs3_firmware_and_boot_with_our_config()
    {
        if (string.IsNullOrEmpty(Dir)) return;
        var m = new Rpcs3Manager(Path.Combine(Dir, "rpcs3"));
        var log = new Progress<DownloadProgress>(p => Debug.WriteLine(p.Stage));
        if (!m.IsInstalled) await m.InstallLatestAsync(log);
        Assert.True(m.IsInstalled);
        if (!m.FirmwareInstalled) await m.InstallFirmwareAsync(log);
        Assert.True(m.FirmwareInstalled);

        m.ConfigureNetwork("127.0.0.1");
        await m.EnableQualityPatchesAsync();
        Assert.True(File.Exists(Path.Combine(m.Root, "patches", "patch.yml")));

        // Boot RPCS3 (GUI, no game) and make sure it keeps our network settings when it rewrites config.yml.
        m.PrepareGuiSettings();
        using var p = m.OpenGui();
        await Task.Delay(12000);
        Assert.False(p.HasExited, "RPCS3 closed by itself");
        p.CloseMainWindow();
        if (!p.WaitForExit(10000)) p.Kill(true);

        var cfg = File.ReadAllText(Path.Combine(m.ConfigDir, "config.yml"));
        Assert.Contains("PSN status: RPCN", cfg);
        Assert.Contains("ds-eu-c.scej-online.jp=127.0.0.1", cfg);
        Assert.Equal("127.0.0.1", m.CurrentSwapTarget());
    }

    [Fact]
    public async Task Upnp_discovery_is_read_only_safe()
    {
        if (string.IsNullOrEmpty(Dir)) return;
        var u = await Upnp.DiscoverAsync(TimeSpan.FromSeconds(3));
        if (u == null) return; // routers without UPnP are a valid outcome
        Assert.StartsWith("http", u.ControlUrl);
        var ip = await u.GetExternalIpAsync();
        Debug.WriteLine($"UPnP {u.ServiceType} ext={ip}");
    }
}
