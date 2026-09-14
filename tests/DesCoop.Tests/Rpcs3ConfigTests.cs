using DesCoop.Emu;
using DesCoop.Game;

namespace DesCoop.Tests;

public class Rpcs3ConfigTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "descoop-rpcs3-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public async Task Network_patch_and_game_config_are_written_without_losing_user_settings()
    {
        var cfgDir = Path.Combine(_root, "config");
        Directory.CreateDirectory(cfgDir);
        File.WriteAllText(Path.Combine(cfgDir, "config.yml"),
            "Core:\n  PPU Decoder: Recompiler (LLVM)\n  SPU Block Size: Safe\nVideo:\n  Renderer: Vulkan\nNet:\n  Internet enabled: Disconnected\n  DNS address: 8.8.8.8\n  PSN status: Disconnected\n");
        File.WriteAllText(Path.Combine(cfgDir, "rpcn.yml"), "Version: 2\nHost: np.rpcs3.net\nNPID: Miguel\nPassword: abc\nToken: \"\"\n");
        Directory.CreateDirectory(Path.Combine(_root, "patches"));
        File.WriteAllText(Path.Combine(_root, "patches", "patch.yml"), "Version: 1.2\n");

        var m = new Rpcs3Manager(_root);
        m.ConfigureNetwork("26.10.20.30");
        m.RegisterGame(new GameInfo(@"D:\Games\Demon's Souls", @"D:\Games\Demon's Souls\PS3_GAME\USRDIR", "x", "BLUS30443", "Demon's Souls", "01.00", true));
        await m.EnableQualityPatchesAsync();
        m.PrepareGuiSettings();

        var text = File.ReadAllText(Path.Combine(cfgDir, "config.yml"));
        Assert.Contains("PPU Decoder: Recompiler (LLVM)", text);
        Assert.Contains("Renderer: Vulkan", text);
        Assert.Contains("DNS address: 8.8.8.8", text);
        Assert.Contains("Internet enabled: Connected", text);
        Assert.Contains("PSN status: RPCN", text);
        Assert.Equal("26.10.20.30", m.CurrentSwapTarget());
        var cfg = new YamlFile(Path.Combine(cfgDir, "config.yml"));
        var swap = YamlFile.Get(YamlFile.Map(cfg.Root, "Net"), "IP swap list")!;
        Assert.Equal(6, swap.Split("&&").Length);
        Assert.Contains("ds-eu-c.scej-online.jp=26.10.20.30", swap);

        Assert.Equal("Miguel", m.RpcnUser());
        Assert.Contains("BLUS30443: D:/Games/Demon's Souls/", File.ReadAllText(Path.Combine(cfgDir, "games.yml")));

        var pc = new YamlFile(Path.Combine(cfgDir, "patch_config.yml"));
        var enabled = YamlFile.Get(YamlFile.Map(YamlFile.Map(YamlFile.Map(YamlFile.Map(YamlFile.Map(pc.Root,
            "PPU-83681f6110d33442329073b72b8dc88a2f677172"), "Skip Intro Videos"), "Demon's Souls"), "BLUS30443"), "01.00"), "Enabled");
        Assert.Equal("true", enabled);

        var ini = File.ReadAllText(Path.Combine(_root, "GuiConfigs", "CurrentSettings.ini"));
        Assert.Contains("infoBoxEnabledWelcome=false", ini);
        Assert.Contains("[Meta]", ini);

        // Idempotent: running again does not duplicate keys.
        m.ConfigureNetwork("127.0.0.1");
        m.PrepareGuiSettings();
        Assert.Equal("127.0.0.1", m.CurrentSwapTarget());
        Assert.Single(File.ReadAllLines(Path.Combine(_root, "GuiConfigs", "CurrentSettings.ini")), l => l.StartsWith("infoBoxEnabledWelcome"));
    }
}
