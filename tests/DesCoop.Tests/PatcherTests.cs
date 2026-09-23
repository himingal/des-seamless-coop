using System.Buffers.Binary;
using System.Text;
using DesCoop.Game;
using SoulsFormats;
using static SoulsFormats.PARAMDEF;

namespace DesCoop.Tests;

/// <summary>Builds a fake Demon's Souls dump with real FromSoftware formats and runs the patcher on it.</summary>
public class PatcherTests : IDisposable
{
    const int BlueId = 2105, EphId = 1019, OtherId = 5;
    readonly string _root = Path.Combine(Path.GetTempPath(), "descoop-game-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void BrandTitle_prepends_seamless_edition_to_the_copyright()
    {
        const string copyright = "©2009 Sony Computer Entertainment Inc.\nLicensed to and published by Atlus U.S.A., Inc.";
        var misc = new FMG(FMG.FMGVersion.DemonsSouls) { Entries = [new(MsgPatcher.CopyrightEntryId, copyright)] };
        // Same id in another FMG but without the copyright marker must be left alone.
        var help = new FMG(FMG.FMGVersion.DemonsSouls) { Entries = [new(MsgPatcher.CopyrightEntryId, "Please select an item.")] };
        var bnd = new BND3 { Version = "07D7R6", Format = Binder.Format.IDs | Binder.Format.Names1 | Binder.Format.Names2, BigEndian = true };
        bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, 0, "menu_misc.fmg", misc.Write()));
        bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, 1, "help.fmg", help.Write()));

        Assert.True(MsgPatcher.BrandTitle(bnd));

        var t = FMG.Read(bnd.Files[0].Bytes).Entries.First(e => e.ID == MsgPatcher.CopyrightEntryId).Text;
        var h = FMG.Read(bnd.Files[1].Bytes).Entries.First(e => e.ID == MsgPatcher.CopyrightEntryId).Text;
        Assert.StartsWith("SEAMLESS EDITION", t);
        Assert.Contains("Sony Computer Entertainment", t); // original copyright preserved
        Assert.Equal("Please select an item.", h);         // untouched (no copyright marker)

        // Idempotent: a second pass does not stack the brand.
        Assert.False(MsgPatcher.BrandTitle(bnd));
    }

    [Fact]
    public void Enhanced_coop_defaults()
    {
        var o = new PatchOptions();
        Assert.True(o.DoubleLoot, "loot for both: DoubleLoot must be ON so the host can drop the dupe to the phantom");
        Assert.True(o.CyanidePill, "fast regroup needs the Cyanide Pill");
        Assert.True(o.BonusMerchant, "Boldwin must sell the rarities so a run never misses them");
        Assert.False(o.PersistentCoop, "persistent co-op desyncs in-game (engine-level); stays OFF");
    }

    /// <summary>EQUIP_PARAM_GOODS_ST as documented for Demon's Souls (64 bytes per row).</summary>
    internal static PARAMDEF GoodsDef()
    {
        var def = new PARAMDEF { ParamType = "EQUIP_PARAM_GOODS_ST", BigEndian = true, DataVersion = 1 };
        void F(DefType t, string n, int arr = 1)
        {
            var f = new Field(def, t, n);
            if (t == DefType.dummy8) f.ArrayLength = arr;
            def.Fields.Add(f);
        }
        F(DefType.u16, "iconId"); F(DefType.u8, "goodsType"); F(DefType.dummy8, "pad_0", 1);
        F(DefType.f32, "weight"); F(DefType.u16, "modelId"); F(DefType.dummy8, "pad_1", 2);
        F(DefType.s32, "basicPrice"); F(DefType.u8, "goodsCategory"); F(DefType.dummy8, "pad_2", 2);
        F(DefType.u8, "goodsUseAnim"); F(DefType.s32, "behaviorId");
        F(DefType.u8, "enable_live"); F(DefType.u8, "enable_gray"); F(DefType.u8, "enable_white");
        F(DefType.u8, "enable_black"); F(DefType.u8, "enable_multi"); F(DefType.u8, "disable_offline");
        F(DefType.dummy8, "pad_3", 2); F(DefType.u8, "isEquip"); F(DefType.u8, "isConsume");
        F(DefType.u8, "isAutoEquip"); F(DefType.u8, "isEstablishment"); F(DefType.s16, "shopLv"); F(DefType.dummy8, "pad_5", 2);
        F(DefType.u8, "isDrop"); F(DefType.u8, "isDisableHand"); F(DefType.dummy8, "pad_6", 2);
        F(DefType.s32, "sortId"); F(DefType.s16, "trophySGradeId"); F(DefType.dummy8, "pad_7", 2);
        F(DefType.s32, "qwcId"); F(DefType.u8, "isEnhance"); F(DefType.u8, "opmeMenuType"); F(DefType.dummy8, "pad_8", 2);
        F(DefType.s32, "yesNoDialogMessageId");
        return def;
    }

    static byte[] BuildGoodsParam(PARAMDEF def)
    {
        var param = new PARAM { ParamType = def.ParamType, BigEndian = true, Rows = [] };
        param.ApplyParamdef(def);
        foreach (var (id, live, gray, consume) in new[] { (OtherId, 1, 1, 1), (EphId, 0, 1, 1), (BlueId, 0, 1, 0), (9000, 1, 0, 1) })
        {
            var row = new PARAM.Row(id, "", def);
            row["enable_live"].Value = (byte)live;
            row["enable_gray"].Value = (byte)gray;
            row["isConsume"].Value = (byte)consume;
            row["sortId"].Value = id * 10;
            param.Rows.Add(row);
        }
        return param.Write();
    }

    static void WriteBnd(string path, params (string name, byte[] data)[] files)
    {
        var bnd = new BND3 { Version = "07D7R6", Format = Binder.Format.IDs | Binder.Format.Names1 | Binder.Format.Names2, BigEndian = true };
        int id = 0;
        foreach (var (name, data) in files) bnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, id++, name, data));
        bnd.Compression = new DCX.DcxEdgeCompressionInfo();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bnd.Write(path);
    }

    internal static byte[] BuildSfo(params (string key, string value)[] entries)
    {
        var keys = new MemoryStream();
        var data = new MemoryStream();
        var index = new List<(int keyOff, int len, int max, int dataOff)>();
        foreach (var (k, v) in entries)
        {
            int ko = (int)keys.Length;
            keys.Write(Encoding.ASCII.GetBytes(k)); keys.WriteByte(0);
            var bytes = Encoding.UTF8.GetBytes(v + "\0");
            int max = (bytes.Length + 3) / 4 * 4;
            index.Add((ko, bytes.Length, max, (int)data.Length));
            data.Write(bytes); data.Write(new byte[max - bytes.Length]);
        }
        while (keys.Length % 4 != 0) keys.WriteByte(0);
        int keyStart = 20 + entries.Length * 16;
        int dataStart = keyStart + (int)keys.Length;
        var o = new byte[dataStart + data.Length];
        "\0PSF"u8.CopyTo(o);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(4), 0x0101);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(8), keyStart);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(12), dataStart);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(16), entries.Length);
        for (int i = 0; i < index.Count; i++)
        {
            int e = 20 + i * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(e), (ushort)index[i].keyOff);
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(e + 2), 0x0204);
            BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(e + 4), index[i].len);
            BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(e + 8), index[i].max);
            BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(e + 12), index[i].dataOff);
        }
        keys.ToArray().CopyTo(o, keyStart);
        data.ToArray().CopyTo(o, dataStart);
        return o;
    }

    GameInfo MakeGame()
    {
        var ps3 = Path.Combine(_root, "PS3_GAME");
        var usr = Path.Combine(ps3, "USRDIR");
        Directory.CreateDirectory(usr);
        File.WriteAllBytes(Path.Combine(ps3, "PARAM.SFO"), BuildSfo(("APP_VER", "01.00"), ("TITLE", "Demon's Souls"), ("TITLE_ID", "BLUS30443")));
        File.WriteAllBytes(Path.Combine(usr, "EBOOT.BIN"), [0x53, 0x43, 0x45, 0]);

        var names = new FMG(FMG.FMGVersion.DemonsSouls) { Entries = [new(BlueId, "Blue Eye Stone"), new(EphId, "Stone of Ephemeral Eyes"), new(OtherId, "Grass")] };
        var descs = new FMG(FMG.FMGVersion.DemonsSouls) { Entries = [new(OtherId, "Use the Blue Eye Stone to help others.")] };
        WriteBnd(Path.Combine(usr, "msg", "naenglish", "item.msgbnd.dcx"),
            (@"N:\DemonsSoul\data\Msg\na_english\goods_name.fmg", names.Write()),
            (@"N:\DemonsSoul\data\Msg\na_english\goods_info.fmg", descs.Write()));

        var goods = BuildGoodsParam(GoodsDef());
        foreach (var n in new[] { "gameparam.parambnd.dcx", "gameparamna.parambnd.dcx" })
            WriteBnd(Path.Combine(usr, "param", "gameparam", n),
                (@"N:\DemonsSoul\data\Param\GameParam\NpcParam.param", new byte[] { 1, 2, 3, 4 }),
                (@"N:\DemonsSoul\data\Param\GameParam\EquipParamGoods.param", goods));

        var g = GameLocator.Resolve(_root);
        Assert.NotNull(g);
        return g!;
    }

    static PARAM ReadGoods(string bndPath)
    {
        var bnd = BND3.Read(bndPath);
        Assert.Equal(DCX.Type.DCX_EDGE, bnd.Compression.Type);
        var p = PARAM.Read(bnd.Files.Single(f => f.Name.EndsWith("EquipParamGoods.param")).Bytes);
        p.ApplyParamdef(GoodsDef());
        return p;
    }

    static byte Cell(PARAM p, int id, string field) => (byte)p[id]![field].Value;

    [Fact]
    public void Locator_reads_sfo_and_accepts_any_subpath()
    {
        var g = MakeGame();
        Assert.Equal("BLUS30443", g.Serial);
        Assert.Equal("01.00", g.Version);
        Assert.True(g.IsDisc);
        Assert.True(GameLocator.IsDemonsSouls(g));
        Assert.Equal(g.Root, GameLocator.Resolve(g.Eboot)!.Root);
        Assert.Equal(g.Root, GameLocator.Resolve(g.UsrDir)!.Root);
    }

    [Fact]
    public void Patch_apply_is_idempotent_reversible_and_precise()
    {
        var g = MakeGame();
        var (blue, eph) = GamePatcher.FindItemIds(g.UsrDir);
        Assert.Equal([BlueId], blue);
        Assert.Equal([EphId], eph);

        // These two co-op-item tweaks are off by default, so turn them on for this check.
        var full = new PatchOptions { BlueEyeStoneInBodyForm = true, InfiniteEphemeralEyes = true };
        var report = GamePatcher.Apply(g, full);
        Assert.True(report.Changed, string.Join("\n", report.Lines));
        Assert.True(GamePatcher.IsPatched(g));
        foreach (var bnd in GamePatcher.FindParamBnds(g.UsrDir))
        {
            Assert.True(File.Exists(bnd + GamePatcher.BackupSuffix));
            var p = ReadGoods(bnd);
            Assert.Equal(1, Cell(p, BlueId, "enable_live"));
            Assert.Equal(1, Cell(p, BlueId, "enable_gray"));
            Assert.Equal(0, Cell(p, BlueId, "isConsume"));
            Assert.Equal(0, Cell(p, EphId, "isConsume"));
            Assert.Equal(0, Cell(p, EphId, "enable_live"));
            Assert.Equal(1, Cell(p, OtherId, "isConsume"));
            Assert.Equal(90000, (int)p[9000]!["sortId"].Value);
        }

        Assert.False(GamePatcher.Apply(g, full).Changed);

        GamePatcher.Apply(g, new PatchOptions { BlueEyeStoneInBodyForm = false, InfiniteEphemeralEyes = true });
        var p2 = ReadGoods(GamePatcher.FindParamBnds(g.UsrDir).First());
        Assert.Equal(0, Cell(p2, BlueId, "enable_live"));
        Assert.Equal(0, Cell(p2, EphId, "isConsume"));

        GamePatcher.Restore(g);
        Assert.False(GamePatcher.IsPatched(g));
        var p3 = ReadGoods(GamePatcher.FindParamBnds(g.UsrDir).First());
        Assert.Equal(1, Cell(p3, EphId, "isConsume"));
    }
}
