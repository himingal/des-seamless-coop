using DesCoop.Game;
using SoulsFormats;
using Xunit.Abstractions;

namespace DesCoop.Tests;

/// <summary>Host/Join Sigils (text + icons) and the experimental shared boss progression.</summary>
public class SeamlessItemsTests(ITestOutputHelper output) : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "descoop-sigils-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    static BND3 NewBinder() => new() { Version = "07D7R6", Format = Binder.Format.IDs | Binder.Format.Names1 | Binder.Format.Names2, BigEndian = true };

    [Fact]
    public void Shared_boss_progress_only_changes_the_phantoms_boss_clear_rollback()
    {
        // Same rollback call in three places; only the phantom's boss clear (BlockClear2_3) may keep progress.
        const string lua =
            "function HostDead_1(proxy, param)\n\tproxy:SetFlagInitState(2);\nend\n" +
            "function BlockClear2_3(proxy,param)\n\tif proxy:IsWhiteGhost() == true then\n\t\tproxy:SetFlagInitState(2);\n\tend\nend\n" +
            "function OnIrregularLeaveSession_1(proxy,param)\n\tproxy:SetFlagInitState(2);\nend\n";
        var src = System.Text.Encoding.Latin1.GetBytes(lua);

        Assert.Equal(0, ScriptPatcher.PatchGlobalEvent(src).changed);
        var (bytes, changed) = ScriptPatcher.PatchGlobalEvent(src, sharedBossProgress: true);
        var outLua = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Equal(1, changed);
        Assert.Equal(2, outLua.Split('\n').Count(l => l.Trim() == "proxy:SetFlagInitState(2);")); // host death + disconnect keep rollback
        Assert.Single(outLua.Split('\n'), l => l.Trim().StartsWith("proxy:SetFlagInitState(1);"));
        Assert.Contains("\t\tproxy:SetFlagInitState(1);", outLua);                              // indentation kept
        Assert.Equal(0, ScriptPatcher.PatchGlobalEvent(bytes, sharedBossProgress: true).changed); // idempotent
    }

    [Fact]
    public void Open_session_unlocks_the_room_in_every_script_of_the_binder()
    {
        // Boss death lock (global_event.lua) and a fog-gate lock with a Shift-JIS comment (map script).
        var sjisComment = System.Text.Encoding.Latin1.GetString([0x83, 0x8B, 0x81, 0x5B, 0x83, 0x80]);
        var global = "function BlockClear2(proxy,param)\n\tproxy:LockSession();\nend\n";
        var map = $"function OnEvent_111_1(proxy,param)\n\t\telse\n\t\t\tproxy:LockSession();--{sjisComment}\n\t\tend\nend\n";
        var bnd = NewBinder();
        bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, 0, @"N:\script\global_event.lua", System.Text.Encoding.Latin1.GetBytes(global)));
        bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, 1, @"N:\script\m02_00_00_00.lua", System.Text.Encoding.Latin1.GetBytes(map)));

        Assert.Equal(0, ScriptPatcher.PatchBinder(bnd, openSession: false) - ScriptPatcher.PatchBinder(NewBinder()));
        int n = ScriptPatcher.PatchBinder(bnd, openSession: true);
        Assert.Equal(2, n);
        foreach (var f in bnd.Files)
        {
            var lua = System.Text.Encoding.Latin1.GetString(f.Bytes);
            Assert.DoesNotContain(lua.Split('\n'), l => l.Trim().StartsWith("proxy:LockSession();"));
            Assert.Contains("--[DeS Co-op] proxy:LockSession();", lua);
        }
        Assert.Equal(0, ScriptPatcher.PatchBinder(bnd, openSession: true)); // idempotent
    }

    [Fact]
    public void Item_text_goes_to_goods_fmgs_only()
    {
        var bnd = NewBinder();
        void Add(int id, params (int id, string text)[] entries) =>
            bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, id, $"{id}.fmg",
                new FMG(FMG.FMGVersion.DemonsSouls) { Entries = [.. entries.Select(e => new FMG.Entry(e.id, e.text))] }.Write()));
        Add(ItemTextPatcher.NameFmg, (1021, "Stone of Ephemeral Eyes"), (9997, "Blue Eye Stone"));
        Add(ItemTextPatcher.InfoFmg, (1021, "Resurrect user's body"), (9997, "Soul Sign"));
        Add(ItemTextPatcher.CaptionFmg, (1021, "An eye stone..."), (9997, "Proof you have been accepted..."));
        Add(14, (1021, "Firestorm"));   // spell names reuse id 1021 and must stay untouched

        int n = ItemTextPatcher.PatchBinder(bnd, [ItemTextPatcher.HostSigil, ItemTextPatcher.JoinSigil, ItemTextPatcher.CyanidePill]);
        Assert.Equal(9, n);

        string Get(int fmg, int id) => FMG.Read(bnd.Files.First(f => f.ID == fmg).Bytes).Entries.First(e => e.ID == id).Text;
        Assert.Equal("Host Sigil", Get(ItemTextPatcher.NameFmg, 1021));
        Assert.Equal("Join Sigil", Get(ItemTextPatcher.NameFmg, 9997));
        Assert.Equal("Cyanide Pill", Get(ItemTextPatcher.NameFmg, CyanidePillPatcher.GoodsId));
        Assert.Equal(ItemTextPatcher.JoinSigil.Info, Get(ItemTextPatcher.InfoFmg, 9997));
        Assert.Equal(ItemTextPatcher.CyanidePill.Caption, Get(ItemTextPatcher.CaptionFmg, CyanidePillPatcher.GoodsId));
        Assert.Equal("Firestorm", Get(14, 1021));
    }

    [Fact]
    public void Sigil_icons_are_embedded_and_land_in_the_right_atlas_cell()
    {
        var host = IconPatcher.Asset("host_sigil.bc3");
        var join = IconPatcher.Asset("join_sigil.bc3");
        Assert.Equal(6144, host.Length);   // 64x96 BC3 = 16x24 blocks x 16 bytes
        Assert.Equal(6144, join.Length);
        Assert.NotEqual(host, join);

        const int W = 256, H = 192;       // fake DXT5 atlas: 64x48 blocks
        var atlas = new byte[W * H];
        Assert.True(IconPatcher.WriteCell(atlas, W, H, 64, 96, host));
        // First block row of the cell starts at block (16, 24); last at (16, 47).
        Assert.Equal(host.AsSpan(0, 256).ToArray(), atlas.AsSpan((24 * 64 + 16) * 16, 256).ToArray());
        Assert.Equal(host.AsSpan(23 * 256, 256).ToArray(), atlas.AsSpan((47 * 64 + 16) * 16, 256).ToArray());
        Assert.False(IconPatcher.WriteCell(atlas, W, H, 200, 96, host)); // would overflow the atlas
        Assert.False(IconPatcher.WriteCell(atlas, W, H, 2, 0, host));   // not block aligned
    }

    [Fact]
    public void Treasure_and_npcs_are_opened_up_for_the_helper_in_the_real_params()
    {
        var root = Environment.GetEnvironmentVariable("DESCOOP_GAME") ?? @"C:\ROM RPCS3\Demons Souls (USA)";
        var usr = Path.Combine(root, "PS3_GAME", "USRDIR");
        var pb = Path.Combine(usr, "param", "gameparam", "gameparamna.parambnd.dcx");
        if (!File.Exists(pb)) return;
        var defs = GamePatcher.LoadParamdefs(usr)!;
        var bnd = BND3.Read(File.Exists(pb + GamePatcher.BackupSuffix) ? pb + GamePatcher.BackupSuffix : pb);
        RawParam Open(string n, string t) => new(bnd.Files.First(f => f.Name.EndsWith(n + ".param")).Bytes, defs[t]);

        var before = Open("ItemLotParam", "ITEMLOT_PARAM_ST");
        var lot50Item = before.GetInt(50, "hostOnlyItemId");           // a ring, host-only in the retail game
        Assert.True(lot50Item > 0);

        var log = new List<string>();
        GamePatcher.PatchBinder(bnd, new PatchOptions(), GamePatcher.FindIds(usr), defs, log, "na");
        foreach (var l in log.Where(l => l.Contains("host-only") || l.Contains("NPC"))) output.WriteLine(l);

        var lots = Open("ItemLotParam", "ITEMLOT_PARAM_ST");
        Assert.Equal(lot50Item, lots.GetInt(50, "lotItemId01"));        // now in the shared draw...
        Assert.Equal(100, lots.GetInt(50, "lotItemBasePoint01"));       // ...guaranteed
        Assert.Equal(0, lots.GetInt(50, "hostOnlyItemId"));             // and no longer host-only (no double for the host)
        int pureLeft = lots.RowIds.Count(id => lots.GetInt(id, "hostOnlyItemId") > 0
            && Enumerable.Range(1, 8).All(k => lots.GetInt(id, $"lotItemId{k:00}") <= 0));
        Assert.Equal(0, pureLeft);

        var npc = Open("NpcParam", "NPC_PARAM_ST");
        Assert.DoesNotContain(npc.RowIds, id => npc.GetInt(id, "isChangeWanderGhost") != 0);

        var goods = Open("EquipParamGoods", "EQUIP_PARAM_GOODS_ST");
        Assert.Equal(1, goods.GetInt(1021, "enable_live"));             // Host Sigil usable in body form too
        Assert.Equal(1, goods.GetInt(9997, "enable_live"));             // Join Sigil usable in body form...
        var sp = Open("SpEffectParam", "SP_EFFECT_PARAM_ST");
        Assert.Equal(1, sp.GetInt(4, "requestSOS"));
        Assert.Equal(1, sp.GetInt(4, "effectTargetLive"));              // ...and its sign effect applies there
        Assert.Contains(log, l => l.Contains("sign effect 4"));
    }

    [Fact]
    public void Join_sigil_sign_effect_is_closed_to_the_living_in_retail()
    {
        // Guards the finding behind BlueEyeStoneInBodyForm: goods 9997 -> behavior 7 -> SpEffect 4 (requestSOS)
        // ships with effectTargetLive = 0, so a human using the stone places no sign.
        var root = Environment.GetEnvironmentVariable("DESCOOP_GAME") ?? @"C:\ROM RPCS3\Demons Souls (USA)";
        var usr = Path.Combine(root, "PS3_GAME", "USRDIR");
        var pb = Path.Combine(usr, "param", "gameparam", "gameparamna.parambnd.dcx");
        if (!File.Exists(pb + GamePatcher.BackupSuffix)) return;
        var defs = GamePatcher.LoadParamdefs(usr)!;
        var bnd = BND3.Read(pb + GamePatcher.BackupSuffix);
        RawParam Open(string n, string t) => new(bnd.Files.First(f => f.Name.EndsWith(n + ".param")).Bytes, defs[t]);
        var beh = Open("BehaviorParam", "BEHAVIOR_PARAM_ST").GetInt(Open("EquipParamGoods", "EQUIP_PARAM_GOODS_ST").GetInt(9997, "behaviorId"), "spEffectId");
        var sp = Open("SpEffectParam", "SP_EFFECT_PARAM_ST");
        Assert.Equal(4, beh);
        Assert.Equal(1, sp.GetInt(beh, "requestSOS"));
        Assert.Equal(0, sp.GetInt(beh, "effectTargetLive"));
        Assert.Equal(1, sp.GetInt(beh, "effectTargetGhost"));
    }

    [Fact]
    public void Sigil_icons_are_written_into_the_real_menu_atlas()
    {
        var root = Environment.GetEnvironmentVariable("DESCOOP_GAME") ?? @"C:\ROM RPCS3\Demons Souls (USA)";
        var src = Path.Combine(root, "PS3_GAME", "USRDIR", "menu");
        if (!File.Exists(Path.Combine(src, "menu.drb"))) return;

        // Work on copies (pristine when a backup exists).
        var menu = Path.Combine(_dir, "menu");
        Directory.CreateDirectory(menu);
        foreach (var f in new[] { "menu.drb", "menu.tpf", "icon.drb", "icon.tpf" })
        {
            var p = Path.Combine(src, f);
            File.Copy(File.Exists(p + GamePatcher.BackupSuffix) ? p + GamePatcher.BackupSuffix : p, Path.Combine(menu, f));
        }
        var log = new List<string>();
        Assert.True(IconPatcher.Apply(_dir, true, log));
        foreach (var l in log) output.WriteLine(l);

        var host = IconPatcher.Asset("host_sigil.bc3");
        var join = IconPatcher.Asset("join_sigil.bc3");
        var icon10 = TPF.Read(Path.Combine(menu, "menu.tpf")).Textures.First(t => t.Name == "Icon10");
        byte[] Cell(byte[] atlas, int x, int y) =>
            [.. Enumerable.Range(0, 24).SelectMany(by => atlas.AsSpan(((y / 4 + by) * 256 + x / 4) * 16, 256).ToArray())];
        Assert.Equal(host, Cell(icon10.Bytes, 512, 288));   // Host Sigil replaces icon 1056
        Assert.Equal(join, Cell(icon10.Bytes, 192, 672));   // Join Sigil replaces icon 1115

        // Turning the option off restores the originals byte for byte.
        Assert.True(IconPatcher.Apply(_dir, false, log));
        Assert.Equal(File.ReadAllBytes(Path.Combine(menu, "menu.tpf") + GamePatcher.BackupSuffix), File.ReadAllBytes(Path.Combine(menu, "menu.tpf")));
    }
}
