using DesCoop.Game;
using SoulsFormats;
using Xunit.Abstractions;

namespace DesCoop.Tests;

/// <summary>
/// Runs every tweak against a copy of the retail parambnd (in memory; the user's files are not touched).
/// Skipped automatically when no Demon's Souls dump is present. Set DESCOOP_GAME to point elsewhere.
/// </summary>
public class RealGameTests(ITestOutputHelper output)
{
    static string? Usr()
    {
        var root = Environment.GetEnvironmentVariable("DESCOOP_GAME") ?? @"C:\ROM RPCS3\Demons Souls (USA)";
        var usr = Path.Combine(root, "PS3_GAME", "USRDIR");
        return Directory.Exists(Path.Combine(usr, "param", "gameparam")) ? usr : null;
    }

    static byte[] Pristine(string usr)
    {
        var p = Path.Combine(usr, "param", "gameparam", "gameparamna.parambnd.dcx");
        return File.ReadAllBytes(File.Exists(p + GamePatcher.BackupSuffix) ? p + GamePatcher.BackupSuffix : p);
    }

    [Fact]
    public void All_tweaks_hit_the_right_fields_of_the_retail_params()
    {
        var usr = Usr();
        if (usr == null) return;
        var defs = GamePatcher.LoadParamdefs(usr);
        Assert.NotNull(defs);
        var ids = GamePatcher.FindIds(usr);
        Assert.Contains(9997, ids.Blue);
        Assert.Contains(1021, ids.Ephemeral);
        Assert.Contains(2023, ids.PureBladestone);

        var original = BND3.Read(Pristine(usr));
        var bnd = BND3.Read(Pristine(usr));
        var log = new List<string>();
        int n = GamePatcher.PatchBinder(bnd, new PatchOptions(), ids, defs, log, "na");
        foreach (var l in log) output.WriteLine(l);
        Assert.True(n > 100);

        RawParam Open(BND3 b, string name)
        {
            var f = b.Files.First(x => x.Name.EndsWith(name + ".param", StringComparison.OrdinalIgnoreCase));
            return new RawParam(f.Bytes, defs![PARAM.Read(f.Bytes).ParamType]);
        }

        // Only the touched params differ, and each has the same size as before.
        string[] touched = ["EquipParamGoods", "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "ShopLineupParam", "ItemLotParam", "CharaInitParam"];
        foreach (var f in bnd.Files)
        {
            var o = original.Files.First(x => x.Name == f.Name);
            Assert.Equal(o.Bytes.Length, f.Bytes.Length);
            if (!touched.Any(t => f.Name.EndsWith(t + ".param", StringComparison.OrdinalIgnoreCase)))
                Assert.True(o.Bytes.AsSpan().SequenceEqual(f.Bytes), f.Name + " should be untouched");
        }

        var goods = Open(bnd, "EquipParamGoods");
        var goods0 = Open(original, "EquipParamGoods");
        Assert.Equal(1, goods.Get(9997, "enable_live"));
        Assert.Equal(0, goods.Get(1021, "isConsume"));
        Assert.Equal(goods0.Get(1000, "weight") / 1.5, goods.Get(1000, "weight"), 4);

        var weapons = Open(bnd, "EquipParamWeapon");
        var weapons0 = Open(original, "EquipParamWeapon");
        Assert.Equal(weapons0.Get(20500, "weight") / 1.5, weapons.Get(20500, "weight"), 4); // Claymore
        var armor = Open(bnd, "EquipParamProtector");
        var armor0 = Open(original, "EquipParamProtector");
        Assert.Equal(armor0.Get(200600, "weight") / 1.5, armor.Get(200600, "weight"), 4);

        var shop = Open(bnd, "ShopLineupParam");
        Assert.Equal(50, shop.GetInt(1000, "value"));      // Crescent Moon Grass 100 -> 50
        Assert.Equal(5000, shop.GetInt(4050, "value"));    // Dark Moon Grass 10000 -> 5000
        Assert.Equal(0, shop.GetInt(7057, "value"));       // paid with a soul: untouched

        var lots = Open(bnd, "ItemLotParam");
        int pure = Enumerable.Range(1, 8).First(i => lots.GetInt(320120, $"lotItemId{i:00}") == 2023);
        Assert.Equal(GamePatcher.PureBladestoneChance, lots.GetInt(320120, $"lotItemBasePoint{pure:00}"));
        var lots0 = Open(original, "ItemLotParam");
        int Sum(RawParam p) => Enumerable.Range(1, 8).Sum(i => p.GetInt(320120, $"lotItemBasePoint{i:00}"));
        Assert.Equal(Sum(lots0), Sum(lots));

        var chara = Open(bnd, "CharaInitParam");
        var chara0 = Open(original, "CharaInitParam");
        string[] stats = ["baseVit", "baseWil", "baseEnd", "baseStr", "baseDex", "baseMag", "baseFai", "baseLuc"];
        foreach (var k in ClassRevamp.Kits)
        {
            Assert.Equal(stats.Sum(s => chara0.GetInt(k.Id, s)), stats.Sum(s => chara.GetInt(k.Id, s))); // same Soul Level
            Assert.Equal(k.Right, chara.GetInt(k.Id, "equip_Wep_Right"));
            Assert.Contains(Enumerable.Range(1, 10), i => chara.GetInt(k.Id, $"item_{i:00}") == 9997);
            Assert.Contains(Enumerable.Range(1, 10), i => chara.GetInt(k.Id, $"item_{i:00}") == 99); // Augite lantern kept
        }
        Assert.DoesNotContain(log, l => l.Contains("not in this game"));
        Assert.Equal(chara0.GetInt(9999, "baseVit"), chara.GetInt(9999, "baseVit")); // debug/NPC rows untouched
    }
}
