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
