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
        string[] touched = ["EquipParamGoods", "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "ShopLineupParam", "ItemLotParam", "CharaInitParam", "SpEffectParam", "NpcParam"];
        foreach (var f in bnd.Files)
        {
            var o = original.Files.First(x => x.Name == f.Name);
            Assert.Equal(o.Bytes.Length, f.Bytes.Length);
            if (!touched.Any(t => f.Name.EndsWith(t + ".param", StringComparison.OrdinalIgnoreCase)))
                Assert.True(o.Bytes.AsSpan().SequenceEqual(f.Bytes), f.Name + " should be untouched");
        }

        var goods = Open(bnd, "EquipParamGoods");
        var goods0 = Open(original, "EquipParamGoods");
        // Body-form Blue Eye Stone and infinite Ephemeral Eyes are OFF by default now, so those fields are untouched.
        Assert.Equal(goods0.Get(9997, "enable_live"), goods.Get(9997, "enable_live"));
        Assert.Equal(goods0.Get(1021, "isConsume"), goods.Get(1021, "isConsume"));
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
        // Pure Bladestone is also an upgrade stone, so the materials pass raises it the rest of the way.
        Assert.Equal(GamePatcher.UpgradeMaterialChance, lots.GetInt(320120, $"lotItemBasePoint{pure:00}"));
        var lots0 = Open(original, "ItemLotParam");
        int Sum(RawParam p) => Enumerable.Range(1, 8).Sum(i => p.GetInt(320120, $"lotItemBasePoint{i:00}"));
        Assert.Equal(Sum(lots0), Sum(lots));

        var sp = Open(bnd, "SpEffectParam");
        Assert.Equal(1.0, sp.Get(GamePatcher.SoulFormEffect, "maxHpRate"), 3);
        Assert.Equal(0.5, sp.Get(9, "maxHpRate"), 3); // black phantoms (invaders) keep the penalty
        Assert.Equal(1, sp.GetInt(GamePatcher.MpRegenEffect, "motionInterval")); // light-armor MP regen ticks every second
        var sp0 = Open(original, "SpEffectParam");
        foreach (var e in GamePatcher.BodyStaminaEffects) // heavy chests: MP regen added, stamina penalty kept
        {
            Assert.Equal(-1, sp.GetInt(e, "changeMpPoint"));
            Assert.Equal(1, sp.GetInt(e, "motionInterval"));
            Assert.Equal(sp0.Get(e, "staminaRecoverChangeSpeed"), sp.Get(e, "staminaRecoverChangeSpeed"), 3);
        }

        var npc = Open(bnd, "NpcParam");
        var npc0 = Open(original, "NpcParam");
        foreach (var liz in GamePatcher.CrystalLizards) Assert.Equal(1, npc.GetInt(liz, "hp"));
        foreach (var d in GamePatcher.Dragons) Assert.Equal(npc0.GetInt(d, "hp") / 2, npc.GetInt(d, "hp"));
        Assert.Equal((int)Math.Round(npc0.GetInt(512000, "getSoul") * 1.25), npc.GetInt(512000, "getSoul")); // +25% souls
        Assert.True(npc.GetInt(311000, "getSoul") > npc0.GetInt(311000, "getSoul")); // lizards give more souls too

        var prot = Open(bnd, "EquipParamProtector");
        var prot0 = Open(original, "EquipParamProtector");
        Assert.Equal(GamePatcher.MpRegenBehavior, prot.GetInt(200000, "residentSpEffectBehaviorId")); // Shaman's Clothes regens MP
        Assert.Equal(prot0.GetInt(200400, "residentSpEffectBehaviorId"), prot.GetInt(200400, "residentSpEffectBehaviorId")); // Chain Mail's stamina effect left alone
        int matSlot = Enumerable.Range(1, 8).FirstOrDefault(i => lots.GetInt(10278, $"lotItemId{i:00}") == 2050 && lots.GetInt(10278, $"lotItemCategory{i:00}") == unchecked((int)0x40000000));
        // A boosted enemy stone drop reaches at least 25%.
        if (matSlot > 0) Assert.True(lots.GetInt(10278, $"lotItemBasePoint{matSlot:00}") >= GamePatcher.UpgradeMaterialChance);

        var chara = Open(bnd, "CharaInitParam");
        var chara0 = Open(original, "CharaInitParam");
        string[] stats = ["baseVit", "baseWil", "baseEnd", "baseStr", "baseDex", "baseMag", "baseFai", "baseLuc"];
        foreach (var k in ClassRevamp.Kits)
        {
            Assert.Equal(k.SoulLevel, stats.Sum(s => chara.GetInt(k.Id, s)) - 80);
            Assert.InRange(k.SoulLevel, 1, 9);
            Assert.Equal(k.Right, chara.GetInt(k.Id, "equip_Wep_Right"));
            Assert.Contains(Enumerable.Range(1, 10), i => chara.GetInt(k.Id, $"item_{i:00}") == 9997);
            Assert.Contains(Enumerable.Range(1, 10), i => chara.GetInt(k.Id, $"item_{i:00}") == 1021);
            Assert.Contains(Enumerable.Range(1, 10), i => chara.GetInt(k.Id, $"item_{i:00}") == 99); // Augite lantern kept
        }
        Assert.DoesNotContain(log, l => l.Contains("not in this game"));
        Assert.Equal(chara0.GetInt(9999, "baseVit"), chara.GetInt(9999, "baseVit")); // debug/NPC rows untouched
    }

    [Fact]
    public void Every_new_class_can_use_its_whole_kit()
    {
        var usr = Usr();
        if (usr == null) return;
        var defs = GamePatcher.LoadParamdefs(usr)!;
        var bnd = BND3.Read(Pristine(usr));
        RawParam Open(string name)
        {
            var f = bnd.Files.First(x => x.Name.EndsWith(name + ".param", StringComparison.OrdinalIgnoreCase));
            return new RawParam(f.Bytes, defs[PARAM.Read(f.Bytes).ParamType]);
        }
        var wep = Open("EquipParamWeapon");
        var armor = Open("EquipParamProtector");
        var rings = Open("EquipParamAccessory");
        var goods = Open("EquipParamGoods");
        var magic = Open("Magic");
        var names = new HashSet<string>();

        // Each row's Original must be its real vanilla class, or the menu name won't match the kit.
        var vanilla = new Dictionary<int, string> {
            [1000] = "Soldier", [1001] = "Knight", [1002] = "Hunter", [1003] = "Priest", [1004] = "Magician",
            [1005] = "Wanderer", [1006] = "Barbarian", [1007] = "Thief", [1008] = "Temple Knight", [1009] = "Royalty" };
        var bodies = new HashSet<int>();
        var focusCount = new Dictionary<string, int>();
        foreach (var k in ClassRevamp.Kits)
        {
            Assert.Equal(vanilla[k.Id], k.Original);
            Assert.True(bodies.Add(k.Armor), "duplicate body armor " + k.Armor + " on " + k.Name);
            focusCount[k.Focus] = focusCount.GetValueOrDefault(k.Focus) + 1;
            Assert.True(names.Add(k.Name), "duplicate class name " + k.Name);
            // Every weapon one-handed with the class's own stats (no two-handing needed).
            foreach (var w in new[] { k.Right, k.Right2, k.Left, k.Left2 }.Where(w => w > 0))
            {
                Assert.True(wep.Has(w), $"{k.Name}: weapon {w} missing");
                string why = $"{k.Name}: weapon {w} needs {wep.GetInt(w, "properStrength")}/{wep.GetInt(w, "properAgility")}/{wep.GetInt(w, "properMagic")}/{wep.GetInt(w, "properFaith")}";
                Assert.True(wep.GetInt(w, "properStrength") <= k.Str, why);
                Assert.True(wep.GetInt(w, "properAgility") <= k.Dex, why);
                Assert.True(wep.GetInt(w, "properMagic") <= k.Mag, why);
                Assert.True(wep.GetInt(w, "properFaith") <= k.Fai, why);
            }
            var hands = new[] { k.Right, k.Right2, k.Left, k.Left2 }.Where(w => w > 0).ToList();
            // Spells need a catalyst, miracles a talisman; at most two one-slot spells (like the vanilla casters).
            Assert.True(k.Spells.Length <= 2);
            foreach (var s in k.Spells)
            {
                Assert.True(magic.Has(s), $"{k.Name}: spell {s} missing");
                Assert.Equal(1, magic.GetInt(s, "slotLength"));
                string flag = magic.GetInt(s, "ezStateBehaviorType") == 1 ? "enableMiracle" : "enableMagic";
                Assert.Contains(hands, w => wep.GetInt(w, flag) == 1);
            }
            if (k.Spells.Length > 0) Assert.True(k.Int >= 8 && Math.Max(k.Mag, k.Fai) >= 13, k.Name);
            if (k.Arrow > 0) Assert.Contains(hands, w => wep.GetInt(w, "weaponCategory") == 10);
            if (k.Bolt > 0) Assert.Contains(hands, w => wep.GetInt(w, "weaponCategory") == 11);
            foreach (var a in new[] { k.Helm, k.Armor, k.Gloves, k.Legs }) Assert.True(armor.Has(a), $"{k.Name}: armor {a} missing");
            if (k.Ring1 > 0) Assert.True(rings.Has(k.Ring1));
            foreach (var (id, _) in k.Items) Assert.True(goods.Has(id), $"{k.Name}: item {id} missing");
            // Total starting weight stays near the vanilla Knight's (36.4).
            double weight = hands.Sum(w => wep.Get(w, "weight")) + new[] { k.Helm, k.Armor, k.Gloves, k.Legs }.Sum(a => armor.Get(a, "weight"));
            Assert.True(weight <= 40, $"{k.Name} carries {weight:0.0}");
            output.WriteLine($"{k.Name,-13} {k.Focus,-19} SL{k.SoulLevel,2}  weight {weight,4:0.0}");
        }
        // Two classes per focus, and INT/FAI classes actually cast.
        Assert.Equal(2, focusCount["Strength"]);
        Assert.Equal(2, focusCount["Dexterity"]);
        Assert.Equal(2, focusCount["Strength/Dexterity"]);
        Assert.Equal(2, focusCount["Faith"]);
        Assert.Equal(2, focusCount["Intelligence"]);
        foreach (var k in ClassRevamp.Kits.Where(k => k.Focus is "Intelligence" or "Faith"))
            Assert.NotEmpty(k.Spells);
    }

    [Fact]
    public void Stay_in_soul_form_only_disables_automatic_revivals()
    {
        var usr = Usr();
        if (usr == null) return;
        var path = Path.Combine(usr, "script", "m01.luabnd.dcx");
        var pristine = File.Exists(path + GamePatcher.BackupSuffix) ? path + GamePatcher.BackupSuffix : path;
        var bnd = BND3.Read(pristine);
        var before = bnd.Files.ToDictionary(f => f.Name, f => f.Bytes.ToArray());

        int n = ScriptPatcher.PatchBinder(bnd);
        Assert.Equal(12, n); // 5 RevivePlayer (one inside an old block comment), 3 RevivePlayerNext, 2 SetAliveMotion, 2 "Revival" texts
        var again = BND3.Read(bnd.Write());
        Assert.Equal(DCX.Type.DCX_EDGE, again.Compression.Type);
        var ge = System.Text.Encoding.Latin1.GetString(again.Files.First(f => f.Name.EndsWith("global_event.lua")).Bytes);
        var live = ge.Split('\n').Select(l => l.Trim()).Where(l => !l.StartsWith("--")).ToList();
        // The only automatic-looking revive left is the manual Demon's Soul one.
        Assert.Single(live, l => l.StartsWith("proxy:RevivePlayer();"));
        Assert.DoesNotContain(live, l => l.StartsWith("proxy:RevivePlayerNext();"));
        Assert.Contains("function OnDemonsSoulRevive", ge);
        output.WriteLine($"{n} lines disabled");
        foreach (var f in again.Files.Where(f => !f.Name.EndsWith("global_event.lua")))
            Assert.True(before[f.Name].AsSpan().SequenceEqual(f.Bytes), f.Name);
        // Idempotent: patching the patched script changes nothing more.
        Assert.Equal(0, ScriptPatcher.PatchGlobalEvent(again.Files.First(f => f.Name.EndsWith("global_event.lua")).Bytes).changed);
    }

    [Fact]
    public void Class_names_are_replaced_in_the_menu_text_only()
    {
        var usr = Usr();
        if (usr == null) return;
        var path = Path.Combine(usr, "msg", "na_english", "menu.msgbnd.dcx");
        var pristine = File.Exists(path + GamePatcher.BackupSuffix) ? path + GamePatcher.BackupSuffix : path;
        var bnd = BND3.Read(pristine);
        var before = bnd.Files.Select(f => f.Bytes.ToArray()).ToList();

        // An untouched FMG round-trips byte for byte.
        var tag = bnd.Files.First(f => f.Name.EndsWith("テキスト表示用タグ一覧.fmg"));
        Assert.True(MsgPatcher.WriteLikeOriginal(FMG.Read(tag.Bytes), tag.Bytes.Length).AsSpan().SequenceEqual(tag.Bytes));

        Assert.Equal(10, MsgPatcher.RenameIn(bnd, ClassRevamp.Renames));
        var again = BND3.Read(bnd.Write());
        var fmg = FMG.Read(again.Files.First(f => f.Name.EndsWith("テキスト表示用タグ一覧.fmg")).Bytes);
        var classNames = fmg.Entries.Where(e => e.ID is >= MsgPatcher.FirstClassTag and <= MsgPatcher.LastClassTag).Select(e => e.Text).ToHashSet();
        Assert.True(classNames.SetEquals(ClassRevamp.Kits.Select(k => k.Name)));
        Assert.Equal("The Nexus", fmg[200101]);
        // Every other file in the binder is untouched (blood-message words keep "Soldier", "Knight"...).
        for (int i = 0; i < before.Count; i++)
            if (!again.Files[i].Name.EndsWith("テキスト表示用タグ一覧.fmg"))
                Assert.True(before[i].AsSpan().SequenceEqual(again.Files[i].Bytes), again.Files[i].Name);
    }
}
