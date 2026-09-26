using System.Text;
using DesCoop.Game;

namespace DesCoop.Tests;

/// <summary>The seamless co-op Lua block appended to global_event.lua (stay together after a boss).</summary>
public class SeamlessCoopScriptTests
{
    const string Retail =
        "function HostDead_1(proxy, param)\n\tproxy:WarpNextStageKick();\nend\n" +
        "function BlockClear2_3(proxy,param)\n\tif proxy:IsWhiteGhost() == true then\n\t\tproxy:WarpNextStageKick();\n\tend\nend\n";

    [Fact]
    public void Block_is_appended_once_after_the_untouched_retail_teardown()
    {
        // The retail teardown stays (it still runs for hosts, invaders and anyone out of a party); the block comes
        // after it so its definitions win. Commenting single kicks (the old approach) left the helper hidden with a
        // frozen menu, so no original line is touched for this option.
        var src = Encoding.Latin1.GetBytes(Retail);
        Assert.Equal(0, ScriptPatcher.PatchGlobalEvent(src).changed);

        var (bytes, changed) = ScriptPatcher.PatchGlobalEvent(src, persistentCoop: true);
        var lua = Encoding.Latin1.GetString(bytes);
        Assert.Equal(1, changed);
        Assert.StartsWith(Retail, lua);
        Assert.Contains(ScriptPatcher.SeamlessMarker, lua);
        Assert.Equal(0, ScriptPatcher.PatchGlobalEvent(bytes, persistentCoop: true).changed); // idempotent
    }

    [Fact]
    public void Block_replaces_the_teardown_without_hiding_or_freezing_the_helper()
    {
        var block = Encoding.Latin1.GetString(ScriptPatcher.SeamlessBlock());
        Assert.All(block, ch => Assert.True(ch < 128, "the block must stay ASCII (the scripts are Shift-JIS)"));

        // Every function the retail boss-clear / return-home path goes through is redefined.
        foreach (var fn in new[] { "BlockClear2", "BlockClear2_2", "BlockClear2_3Leave", "BlockClear2_3",
                                   "HostDead_1", "OnLeave_Limit", "PartyGhostDeath_2", "InGameStart" })
            Assert.Contains($"function {fn}(proxy,param)", block);

        // None of what made the helper vanish, and nobody leaves the room after a boss.
        Assert.DoesNotContain("proxy:SetDrawEnable", block);
        Assert.DoesNotContain("proxy:SetLoadWait", block);
        Assert.DoesNotContain("proxy:LeaveSession", block);
        Assert.DoesNotContain("CustomLuaCallStart( 4063", block);
        Assert.Contains("proxy:SetSubMenuBrake( false );", block);
        Assert.Contains("ClearBoss = false;", block);

        // Going home keeps the shared progress for the helper only, and the sign comes back at home.
        Assert.Contains("proxy:SetFlagInitState(1);", block);
        Assert.Contains("proxy:SetFlagInitState(2);", block);
        Assert.Contains("proxy:SetEventSpecialEffect( 10000, 4 );", block);
    }
}

/// <summary>Co-op in the Nexus: its collisions become an online block.</summary>
public class NexusPatcherTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "descoop-nexus-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Nexus_collisions_become_online_block_10079_and_revert()
    {
        var root = Environment.GetEnvironmentVariable("DESCOOP_GAME") ?? @"C:\ROM RPCS3\Demons Souls (USA)";
        var src = Path.Combine(root, "PS3_GAME", "USRDIR", "map", "mapstudio", "m01_00_00_00.msb");
        if (!File.Exists(src)) return;
        var dir = Path.Combine(_dir, "map", "mapstudio");
        Directory.CreateDirectory(dir);
        var msb = Path.Combine(dir, "m01_00_00_00.msb");
        File.Copy(File.Exists(src + GamePatcher.BackupSuffix) ? src + GamePatcher.BackupSuffix : src, msb);

        var retail = SoulsFormats.MSBD.Read(msb).Parts.Collisions;
        Assert.Equal(40, retail.Count(c => c.MapNameID == -10079 && c.UnkT38 == -1));   // no multiplayer in retail

        var log = new List<string>();
        Assert.True(NexusPatcher.Apply(_dir, true, log));
        var open = SoulsFormats.MSBD.Read(msb).Parts.Collisions;
        Assert.Equal(40, open.Count(c => c.MapNameID == 10079 && c.UnkT38 == 0));
        Assert.DoesNotContain(open, c => c.MapNameID == -10079);
        Assert.Equal(retail.Count, open.Count);

        Assert.False(NexusPatcher.Apply(_dir, true, log));   // idempotent
        Assert.True(NexusPatcher.Apply(_dir, false, log));   // reversible
        Assert.Equal(File.ReadAllBytes(msb + GamePatcher.BackupSuffix), File.ReadAllBytes(msb));
    }
}
