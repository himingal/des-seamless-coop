using SoulsFormats;
using static SoulsFormats.PARAMDEF;

namespace DesCoop.Game;

public sealed class PatchOptions
{
    /// <summary>Blue Eye Stone usable in body form. OFF by default: the game only shows a body-form sign to
    /// its owner, so a sign placed while human never reaches the host. Co-op needs the helper in soul form.</summary>
    public bool BlueEyeStoneInBodyForm { get; set; } = false;
    /// <summary>Stone of Ephemeral Eyes is never consumed, so you can turn human any time to host.</summary>
    public bool InfiniteEphemeralEyes { get; set; } = true;
    /// <summary>Soul form keeps 100% max HP instead of 50%: dying costs nothing, it plays like body form.</summary>
    public bool FullHpSoulForm { get; set; } = true;
    /// <summary>No automatic revival after bosses: you stay in soul form (Stone of Ephemeral Eyes revives on demand).</summary>
    public bool StayInSoulForm { get; set; } = true;
    /// <summary>Every starting class carries a Blue Eye Stone and a Stone of Ephemeral Eyes from the first second.</summary>
    public bool StartWithBlueEyeStone { get; set; } = true;
    /// <summary>Ten new starting classes (names, stats and kits) replace the vanilla ones.</summary>
    public bool RevampedClasses { get; set; } = true;
    /// <summary>Brand the title screen: the "PRESS START BUTTON" text (menu FMG entry 30000) becomes
    /// "SEAMLESS EDITION / PRESS START BUTTON", shown in the game's own font on the start screen.</summary>
    public bool SeamlessEditionTitle { get; set; } = true;
    /// <summary>Everything sold by NPCs costs half.</summary>
    public bool CheaperShops { get; set; } = true;
    /// <summary>Pure Bladestone drops from the Shrine of Storms skeletons 15% of the time instead of 0.5%.</summary>
    public bool EasierPureBladestone { get; set; } = true;
    /// <summary>Weapons, armor, rings and items weigh 1/3 less: +50% equip load and item burden.</summary>
    public bool HeavierLoads { get; set; } = true;
    /// <summary>Upgrade stones (hardstone, sharpstone, bladestone…) drop far more often from enemies.</summary>
    public bool EasierUpgradeMaterials { get; set; } = true;
    /// <summary>Every Crystal Lizard has 1 HP: one hit and it drops its stones.</summary>
    public bool OneHitCrystalLizards { get; set; } = true;
    /// <summary>The Red and Blue dragons (and their kin) have half the HP.</summary>
    public bool WeakerDragons { get; set; } = true;
    /// <summary>Enemies give 25% more souls.</summary>
    public bool MoreSouls { get; set; } = true;
    /// <summary>Passive MP regeneration while any chest armor is worn.</summary>
    public bool ManaRegen { get; set; } = true;
    /// <summary>Seconds between each +1 MP tick of the passive regen (1 = 1 MP/s; 2 = 1 MP every 2 s).</summary>
    public int ManaRegenIntervalSeconds { get; set; } = 2;
    /// <summary>Every world pickup, chest and enemy drop gives two of the item instead of one. OFF: loot is shared
    /// natively instead (see <see cref="SharedLoot"/>), so nothing has to be handed over manually.</summary>
    public bool DoubleLoot { get; set; } = false;
    /// <summary>
    /// Map treasure for both players. 115 world treasures keep their item in ItemLotParam's hostOnlyItem slot
    /// ("only the single player / multiplay host can obtain it") while the shared draw is a guaranteed nothing.
    /// The item is moved into the shared draw at 100%, so whoever opens the treasure — host or helper — gets it.
    /// </summary>
    public bool SharedLoot { get; set; } = true;
    /// <summary>
    /// NPCs for the helper too: NpcParam.isChangeWanderGhost makes 28 friendly NPCs turn into non-interactive
    /// wandering ghosts when the player is a guest (client) in someone else's world. Cleared, they stay real.
    /// </summary>
    public bool NpcsForHelper { get; set; } = true;
    /// <summary>Every starting class carries a "Cyanide Pill" that kills you instantly, so the helper turns
    /// into a soul-form ghost on demand instead of having to farm a death to place a summon sign.</summary>
    public bool CyanidePill { get; set; } = true;
    /// <summary>The two co-op items get their own identity and icon: the Stone of Ephemeral Eyes becomes the
    /// "Host Sigil" (restore your body = be the host) and the Blue Eye Stone the "Join Sigil" (in soul form,
    /// your sign is carried to your host's side). Same items, same mechanics — new names, text and icons.</summary>
    public bool SeamlessItems { get; set; } = true;
    /// <summary>EXPERIMENTAL shared boss progression: a boss killed together also counts in the helper's world
    /// (the phantom's boss-clear flag rollback is switched to the keep-progress mode). Pairs with DoubleLoot,
    /// which gives the host two Demon's Souls to share. Back up saves before testing.</summary>
    public bool SharedBossProgress { get; set; } = true;

    /// <summary>
    /// Blacksmith Boldwin (Nexus) also sells the world-tendency-locked rarities (so a co-op run never misses
    /// them): Talisman of Beasts, Phosphorescent Pole, Dragon Bone Smasher, Magic Sword "Makoto", Istarelle,
    /// Blind, Large Sword of Moonlight, Blueblood Sword, Monk's Head Wrappings, Colorless Demon's Soul (10k
    /// each) and every Pure upgrade stone. Added as extra ShopLineupParam rows in Boldwin's buy menu (ids
    /// 9009+, inside the [9000,9099] range his shop displays), with no tendency/flag gate — pure param,
    /// pristine backup, reversible, no ESD.
    /// </summary>
    public bool BonusMerchant { get; set; } = true;

    /// <summary>
    /// Keep the summoned blue phantom in the host's world through a boss clear and through the host's death,
    /// instead of being sent home. Implemented by commenting out the single <c>proxy:WarpNextStageKick();</c>
    /// call in the co-op teardown functions of the game's own Lua (<c>BlockClear2_3</c> = boss/area clear,
    /// <c>HostDead_1</c> = host death) in every m*.luabnd. Plain-Lua edit, pristine backup kept, fully
    /// reversible — no EBOOT memory patching. OFF by default: keeping the phantom past a boss or the host's
    /// death desyncs the session in-game (glitched phantom), so the game's normal "send the phantom home" is
    /// left in place and re-summoning is instead made instant (sign next to the host + the Cyanide Pill).
    /// </summary>
    public bool PersistentCoop { get; set; } = false;
}

public sealed record PatchReport(bool Changed, List<string> Lines);

/// <summary>
/// Edits gameparam*.parambnd(.dcx) of the user's own dump. Every change is a single field written in
/// place (byte offsets from the game's own paramdefs), always starting from the pristine backup, so
/// unticking an option really reverts it. Items are located by their English in-game names.
/// </summary>
public static class GamePatcher
{
    public const string BackupSuffix = ".descoop-backup";
    public const int ShopPricePercent = 50;
    public const double LoadMultiplier = 1.5;
    public const int PureBladestoneChance = 150; // out of 1000
    public const int UpgradeMaterialChance = 250; // out of 1000 (25%)
    public const int SoulBonusPercent = 125;
    /// <summary>SpEffectParam row "[System] parameter change while a ghost" (soul form and blue phantoms): maxHpRate 0.5.</summary>
    public const int SoulFormEffect = 8;
    /// <summary>Goods ids of every upgrade stone (shard/chunk/pure of each material), incl. large shards.</summary>
    public static readonly int[] UpgradeStones = [.. Enumerable.Range(2000, 58)];
    /// <summary>NpcParam ids of the Crystal Lizards (one per area) and of the bridge dragons + kin.</summary>
    public static readonly int[] CrystalLizards = [.. Enumerable.Range(311000, 20)];
    public static readonly int[] Dragons = [512000, 513000, 513001];
    /// <summary>MP-regen SpEffect used by equipment (changeMpPoint -1) and the Behavior that applies it.</summary>
    public const int MpRegenEffect = 6030, MpRegenBehavior = 4200;
    /// <summary>Chest-armor "stamina recovery down" effects (levels 1-4). They have a free changeMpPoint slot,
    /// so MP regen is added there for medium/heavy armor without removing the stamina penalty.</summary>
    public static readonly int[] BodyStaminaEffects = [6210, 6211, 6212, 6213];

    // The seamless names are listed too: item text is rewritten in place, so a second PLAY reads the new names.
    static readonly string[] BlueEyeNames = ["Blue Eye Stone", "Join Sigil"];
    static readonly string[] EphemeralNames = ["Stone of Ephemeral Eyes", "Host Sigil"];
    const string PureBladestoneName = "Pure Bladestone";
    const int GoodsCategory = 0x40000000;

    public sealed record ItemIds(HashSet<int> Blue, HashSet<int> Ephemeral, HashSet<int> PureBladestone);

    public static IEnumerable<string> FindParamBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "param", "gameparam");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "gameparam*.parambnd*")
            .Where(f => !f.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Goods ids by English name, read from the game's own item text.</summary>
    public static ItemIds FindIds(string usrDir)
    {
        var ids = new ItemIds([], [], []);
        var msgDir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(msgDir)) return ids;
        foreach (var file in Directory.EnumerateFiles(msgDir, "*.msgbnd*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                foreach (var f in BND3.Read(file).Files)
                {
                    FMG fmg;
                    try { fmg = FMG.Read(f.Bytes); } catch { continue; }
                    foreach (var e in fmg.Entries)
                    {
                        var t = e.Text?.Trim();
                        if (string.IsNullOrEmpty(t)) continue;
                        if (BlueEyeNames.Any(n => n.Equals(t, StringComparison.OrdinalIgnoreCase))) ids.Blue.Add(e.ID);
                        if (EphemeralNames.Any(n => n.Equals(t, StringComparison.OrdinalIgnoreCase))) ids.Ephemeral.Add(e.ID);
                        if (t.Equals(PureBladestoneName, StringComparison.OrdinalIgnoreCase)) ids.PureBladestone.Add(e.ID);
                    }
                }
            }
            catch { }
        }
        return ids;
    }

    /// <summary>Kept for callers that only need the two co-op items.</summary>
    public static (HashSet<int> blue, HashSet<int> ephemeral) FindItemIds(string usrDir)
    {
        var ids = FindIds(usrDir);
        return (ids.Blue, ids.Ephemeral);
    }

    // ------------------------------------------------------------------ field layouts

    /// <summary>Field offsets from the game's paramdef.paramdefbnd, or null if the dump has none.</summary>
    public static Dictionary<string, Dictionary<string, (int, DefType)>>? LoadParamdefs(string usrDir)
    {
        var path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd.dcx");
        if (!File.Exists(path)) path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd");
        if (!File.Exists(path)) return null;
        try
        {
            var map = new Dictionary<string, Dictionary<string, (int, DefType)>>();
            foreach (var f in BND3.Read(path).Files)
            {
                try { var d = PARAMDEF.Read(f.Bytes); map[d.ParamType] = RawParam.Offsets(d); } catch { }
            }
            return map.Count > 0 ? map : null;
        }
        catch { return null; }
    }

    /// <summary>Built-in layout (Demon's Souls, verified against the retail paramdefs) for the fields we touch.</summary>
    internal static Dictionary<string, (int, DefType)>? BuiltinFields(string paramType)
    {
        switch (paramType)
        {
            case "EQUIP_PARAM_GOODS_ST":
                return new() { ["weight"] = (4, DefType.f32), ["basicPrice"] = (12, DefType.s32), ["enable_live"] = (24, DefType.u8), ["enable_gray"] = (25, DefType.u8), ["isConsume"] = (33, DefType.u8) };
            case "EQUIP_PARAM_WEAPON_ST": return new() { ["weight"] = (36, DefType.f32) };
            case "EQUIP_PARAM_PROTECTOR_ST": return new() { ["weight"] = (20, DefType.f32) };
            case "EQUIP_PARAM_ACCESSORY_ST": return new() { ["weight"] = (12, DefType.f32) };
            case "SP_EFFECT_PARAM_ST": return new() { ["maxHpRate"] = (32, DefType.f32) };
            case "SHOP_LINEUP_PARAM":
                return new() { ["shopType"] = (0, DefType.u8), ["equipType"] = (1, DefType.u8), ["equipId"] = (4, DefType.s32), ["value"] = (8, DefType.s32), ["mtrlId"] = (12, DefType.s32) };
            case "ITEMLOT_PARAM_ST":
            {
                var d = new Dictionary<string, (int, DefType)>();
                for (int i = 1; i <= 8; i++)
                {
                    int b = 4 + (i - 1) * 20;
                    d[$"lotItemCategory{i:00}"] = (b, DefType.s32);
                    d[$"lotItemId{i:00}"] = (b + 4, DefType.s32);
                    d[$"lotItemNum{i:00}"] = (b + 8, DefType.u16);
                    d[$"lotItemBasePoint{i:00}"] = (b + 10, DefType.u16);
                }
                return d;
            }
            case "CHARACTER_INIT_PARAM":
            {
                var d = new Dictionary<string, (int, DefType)>();
                string[] ints = ["baseVit", "baseWil", "baseEnd", "baseStr", "baseDex", "baseMag", "baseFai", "baseLuc", "baseHp", "baseMp", "baseRec_mp", "baseSp", "baseRec_sp", "red_Falldam", "soul",
                    "equip_Wep_Right", "equip_Subwep_Right", "equip_Wep_Left", "equip_Subwep_Left", "equip_Helm", "equip_Armer", "equip_Gaunt", "equip_Leg",
                    "equip_Arrow", "arrowNum", "equip_Bolt", "boltNum", "equip_Accessory01", "equip_Accessory02", "equip_Accessory03", "equip_Accessory04", "equip_Accessory05",
                    "equip_Skill_01", "equip_Skill_02", "equip_Skill_03", "equip_Spell_01", "equip_Spell_02", "equip_Spell_03", "equip_Spell_04", "equip_Spell_05", "equip_Spell_06", "equip_Spell_07",
                    "QWC_sb", "QWC_mw", "QWC_cd"];
                for (int i = 0; i < ints.Length; i++)
                    d[ints[i]] = (i * 4, ints[i] is "baseRec_mp" or "baseRec_sp" or "red_Falldam" ? DefType.f32 : DefType.s32);
                for (int i = 1; i <= 10; i++)
                {
                    d[$"item_{i:00}"] = (180 + (i - 1) * 8, DefType.s32);
                    d[$"itemNum_{i:00}"] = (184 + (i - 1) * 8, DefType.s32);
                }
                return d;
            }
            default: return null;
        }
    }

    // ------------------------------------------------------------------ apply

    public static PatchReport Apply(GameInfo game, PatchOptions opt)
    {
        var log = new List<string>();
        var ids = FindIds(game.UsrDir);
        var defs = LoadParamdefs(game.UsrDir);
        if (opt.BlueEyeStoneInBodyForm && ids.Blue.Count == 0) log.Add("Warning: 'Blue Eye Stone' not found in the game's text files.");
        if (opt.InfiniteEphemeralEyes && ids.Ephemeral.Count == 0) log.Add("Warning: 'Stone of Ephemeral Eyes' not found in the game's text files.");

        bool changed = false;
        var bnds = FindParamBnds(game.UsrDir).ToList();
        if (bnds.Count == 0) log.Add("Error: param/gameparam/gameparam*.parambnd not found.");
        foreach (var path in bnds)
        {
            var backup = path + BackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup);
            var bnd = BND3.Read(backup);
            int n = PatchBinder(bnd, opt, ids, defs, log, Path.GetFileName(path));
            if (opt.BonusMerchant) n += MerchantPatcher.AddBonusStock(bnd, game.UsrDir, log, Path.GetFileName(path));
            if (opt.CyanidePill) n += CyanidePillPatcher.AddParams(bnd, game.UsrDir, log, Path.GetFileName(path));
            var fresh = bnd.Write();
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
            {
                File.WriteAllBytes(path, fresh);
                changed = true;
            }
            log.Add($"{Path.GetFileName(path)}: {n} change(s)");
        }
        if (MsgPatcher.Apply(game.UsrDir, opt.RevampedClasses ? ClassRevamp.Renames : null, opt.SeamlessEditionTitle, log)) changed = true;
        if (ItemTextPatcher.Apply(game.UsrDir, opt.CyanidePill, opt.SeamlessItems, log)) changed = true;
        if (IconPatcher.Apply(game.UsrDir, opt.SeamlessItems, log)) changed = true;
        if (ScriptPatcher.Apply(game.UsrDir, opt.StayInSoulForm, opt.PersistentCoop, opt.SharedBossProgress, log)) changed = true;
        return new PatchReport(changed, log);
    }

    sealed class Ctx(BND3 bnd, Dictionary<string, Dictionary<string, (int, DefType)>>? defs, List<string> log, string label)
    {
        readonly Dictionary<string, (BinderFile file, RawParam param)> _open = [];
        public List<string> Log => log;
        public string Label => label;
        public int Changes;

        public RawParam? Param(string fileSuffix)
        {
            if (_open.TryGetValue(fileSuffix, out var o)) return o.param;
            var f = bnd.Files.FirstOrDefault(x => (x.Name ?? "").EndsWith(fileSuffix + ".param", StringComparison.OrdinalIgnoreCase));
            if (f == null) return null;
            try
            {
                string type = PARAM.Read(f.Bytes).ParamType;
                var fields = defs != null && defs.TryGetValue(type, out var d) ? d : BuiltinFields(type);
                if (fields == null) { log.Add($"{label}: no field layout for {type}"); return null; }
                var p = new RawParam(f.Bytes, fields);
                _open[fileSuffix] = (f, p);
                return p;
            }
            catch (Exception ex)
            {
                log.Add($"{label}: could not open {fileSuffix}: {ex.Message}");
                return null;
            }
        }

        public bool Set(RawParam p, int id, string field, double v)
        {
            bool changed = p.Set(id, field, v);
            if (changed) Changes++;
            return changed;
        }

        public void Commit()
        {
            foreach (var (file, param) in _open.Values) file.Bytes = param.Bytes;
        }
    }

    internal static int PatchBinder(BND3 bnd, PatchOptions opt, ItemIds ids,
        Dictionary<string, Dictionary<string, (int, DefType)>>? defs, List<string> log, string label)
    {
        var c = new Ctx(bnd, defs, log, label);
        var goods = c.Param("EquipParamGoods");
        if (goods == null) log.Add($"{label}: EquipParamGoods.param not found");

        if (goods != null && opt.BlueEyeStoneInBodyForm)
            foreach (var id in ids.Blue.Where(goods.Has)) { c.Set(goods, id, "enable_live", 1); log.Add($"{label}: item {id} -> Blue Eye Stone usable in body form"); }

        if (goods != null && opt.InfiniteEphemeralEyes)
            foreach (var id in ids.Ephemeral.Where(goods.Has)) { c.Set(goods, id, "isConsume", 0); log.Add($"{label}: item {id} -> Stone of Ephemeral Eyes is never consumed"); }

        if (opt.FullHpSoulForm && c.Param("SpEffectParam") is { } sp && sp.Has(SoulFormEffect) && sp.HasField("maxHpRate")
            && Math.Abs(sp.Get(SoulFormEffect, "maxHpRate") - 0.5) < 0.01)
        {
            c.Set(sp, SoulFormEffect, "maxHpRate", 1.0);
            log.Add($"{label}: soul form keeps full HP");
        }

        if (opt.HeavierLoads) ScaleWeights(c);
        if (opt.CheaperShops) CheaperShops(c);
        if (opt.EasierPureBladestone) BoostDrop(c, ids.PureBladestone.Count > 0 ? ids.PureBladestone : [2023], PureBladestoneChance, "Pure Bladestone");
        if (opt.EasierUpgradeMaterials) BoostDrop(c, [.. UpgradeStones], UpgradeMaterialChance, "upgrade stones");
        if (opt.DoubleLoot) DoubleLoot(c);
        if (opt.SharedLoot) ShareHostOnlyLoot(c);
        if (opt.NpcsForHelper) NpcsStayReal(c);
        // The Host Sigil works from any living form, so "use it to be the host" never fails in body form.
        if (goods != null && opt.SeamlessItems)
            foreach (var id in ids.Ephemeral.Where(goods.Has)) c.Set(goods, id, "enable_live", 1);
        if (opt.OneHitCrystalLizards || opt.WeakerDragons || opt.MoreSouls) TweakEnemies(c, opt);
        if (opt.ManaRegen) ManaRegen(c, opt.ManaRegenIntervalSeconds);

        var chara = c.Param("CharaInitParam");
        if (chara != null)
        {
            if (opt.RevampedClasses) ClassRevamp.Apply(chara, c.Set, Exists(c), log, label);
            if (opt.StartWithBlueEyeStone && goods != null)
            {
                foreach (var blue in ids.Blue.Where(goods.Has).Take(1)) GiveToClasses(c, chara, blue, "Blue Eye Stone");
                foreach (var eph in ids.Ephemeral.Where(goods.Has).Take(1)) GiveToClasses(c, chara, eph, "Stone of Ephemeral Eyes");
            }
            // The Cyanide Pill goods row is added after PatchBinder, so hand it out by id directly.
            if (opt.CyanidePill) GiveToClasses(c, chara, CyanidePillPatcher.GoodsId, "Cyanide Pill");
        }

        c.Commit();
        return c.Changes;
    }

    static Func<string, int, bool> Exists(Ctx c) => (kind, id) =>
    {
        var p = kind switch
        {
            "weapon" => c.Param("EquipParamWeapon"),
            "protector" => c.Param("EquipParamProtector"),
            "accessory" => c.Param("EquipParamAccessory"),
            "goods" => c.Param("EquipParamGoods"),
            "magic" => c.Param("Magic"),
            _ => null,
        };
        return p == null || p.Has(id); // without the param we cannot check; trust the table
    };

    static void ScaleWeights(Ctx c)
    {
        int n = 0;
        foreach (var name in new[] { "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "EquipParamGoods" })
        {
            var p = c.Param(name);
            if (p == null || !p.HasField("weight")) continue;
            foreach (var id in p.RowIds)
            {
                double w = p.Get(id, "weight");
                if (w > 0) { c.Set(p, id, "weight", w / LoadMultiplier); n++; }
            }
        }
        c.Log.Add($"{c.Label}: {n} weights reduced by 1/3 (+50% equip load and item burden)");
    }

    static void CheaperShops(Ctx c)
    {
        var shop = c.Param("ShopLineupParam");
        if (shop == null) return;
        int n = 0;
        foreach (var id in shop.RowIds)
        {
            int v = shop.GetInt(id, "value");
            if (v <= 0) continue; // 0 = paid with a boss soul / material
            c.Set(shop, id, "value", Math.Max(1, (int)Math.Round(v * ShopPricePercent / 100.0)));
            n++;
        }
        c.Log.Add($"{c.Label}: {n} shop prices cut to {ShopPricePercent}%");
    }

    /// <summary>Raises the chance of an item in every lot that can drop it, taking the weight from the other entries.</summary>
    static void BoostDrop(Ctx c, HashSet<int> itemIds, int chance, string what)
    {
        var lots = c.Param("ItemLotParam");
        if (lots == null) return;
        int n = 0;
        foreach (var lot in lots.RowIds)
        {
            for (int i = 1; i <= 8; i++)
            {
                if (lots.GetInt(lot, $"lotItemCategory{i:00}") != GoodsCategory || !itemIds.Contains(lots.GetInt(lot, $"lotItemId{i:00}"))) continue;
                int pts = lots.GetInt(lot, $"lotItemBasePoint{i:00}");
                if (pts >= chance) continue;
                int need = chance - pts;
                // Take the points from the other entries, biggest first, never below 1.
                var others = Enumerable.Range(1, 8).Where(j => j != i)
                    .Select(j => (j, pts: lots.GetInt(lot, $"lotItemBasePoint{j:00}"))).Where(x => x.pts > 1)
                    .OrderByDescending(x => x.pts).ToList();
                foreach (var (j, p) in others)
                {
                    if (need <= 0) break;
                    int take = Math.Min(need, p - 1);
                    c.Set(lots, lot, $"lotItemBasePoint{j:00}", p - take);
                    need -= take;
                }
                if (c.Set(lots, lot, $"lotItemBasePoint{i:00}", chance - need)) n++;
            }
        }
        c.Log.Add($"{c.Label}: {n} drop lot(s) boosted for {what}");
    }

    /// <summary>Doubles how many of each item a lot gives, so a host and helper can each grab one.</summary>
    static void DoubleLoot(Ctx c)
    {
        var lots = c.Param("ItemLotParam");
        if (lots == null) return;
        int n = 0;
        foreach (var lot in lots.RowIds)
            for (int i = 1; i <= 8; i++)
            {
                if (lots.GetInt(lot, $"lotItemCategory{i:00}") == 0 || lots.GetInt(lot, $"lotItemId{i:00}") <= 0) continue;
                int num = lots.GetInt(lot, $"lotItemNum{i:00}");
                if (num >= 1 && num < 99 && c.Set(lots, lot, $"lotItemNum{i:00}", Math.Min(99, num * 2))) n++;
            }
        c.Log.Add($"{c.Label}: {n} loot stack(s) doubled");
    }

    /// <summary>Moves each pure host-only treasure item into the shared draw (slot 1, 100%).</summary>
    static void ShareHostOnlyLoot(Ctx c)
    {
        var lots = c.Param("ItemLotParam");
        if (lots == null || !lots.HasField("hostOnlyItemId")) { c.Log.Add($"{c.Label}: ItemLotParam has no hostOnly layout"); return; }
        // What "no host-only item" looks like in this dump (most common category among lots without one).
        int noneCate = lots.RowIds.Where(id => lots.GetInt(id, "hostOnlyItemId") <= 0)
            .GroupBy(id => lots.GetInt(id, "hostOnlyItemCate")).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(-1);
        int n = 0;
        foreach (var lot in lots.RowIds)
        {
            int item = lots.GetInt(lot, "hostOnlyItemId");
            if (item <= 0) continue;
            // Only "pure" treasures: every shared slot is empty or the "nothing" outcome.
            bool sharedIsEmpty = Enumerable.Range(1, 8).All(k => lots.GetInt(lot, $"lotItemId{k:00}") <= 0);
            if (!sharedIsEmpty) continue;
            c.Set(lots, lot, "lotItemCategory01", lots.GetInt(lot, "hostOnlyItemCate"));
            c.Set(lots, lot, "lotItemId01", item);
            c.Set(lots, lot, "lotItemNum01", Math.Max(1, lots.GetInt(lot, "hostOnlyItemNum")));
            for (int k = 2; k <= 8; k++) c.Set(lots, lot, $"lotItemBasePoint{k:00}", 0);
            c.Set(lots, lot, "lotItemBasePoint01", 100);
            c.Set(lots, lot, "hostOnlyItemId", 0);
            c.Set(lots, lot, "hostOnlyItemNum", 0);
            c.Set(lots, lot, "hostOnlyItemCate", noneCate);
            n++;
        }
        c.Log.Add($"{c.Label}: {n} host-only treasure(s) shared with the helper");
    }

    static void NpcsStayReal(Ctx c)
    {
        var npc = c.Param("NpcParam");
        if (npc == null || !npc.HasField("isChangeWanderGhost")) { c.Log.Add($"{c.Label}: NpcParam has no isChangeWanderGhost"); return; }
        int n = 0;
        foreach (var id in npc.RowIds)
            if (npc.GetInt(id, "isChangeWanderGhost") != 0 && c.Set(npc, id, "isChangeWanderGhost", 0)) n++;
        c.Log.Add($"{c.Label}: {n} NPC(s) stay real (talkable) for the helper");
    }

    static void TweakEnemies(Ctx c, PatchOptions opt)
    {
        var npc = c.Param("NpcParam");
        if (npc == null || !npc.HasField("hp") || !npc.HasField("getSoul")) { c.Log.Add($"{c.Label}: NpcParam has no hp/getSoul layout"); return; }
        int lizards = 0, dragons = 0, souls = 0;
        foreach (var id in npc.RowIds)
        {
            if (opt.MoreSouls)
            {
                int s = npc.GetInt(id, "getSoul");
                if (s > 0 && c.Set(npc, id, "getSoul", (int)Math.Round(s * SoulBonusPercent / 100.0))) souls++;
            }
            if (opt.OneHitCrystalLizards && CrystalLizards.Contains(id) && c.Set(npc, id, "hp", 1)) lizards++;
            if (opt.WeakerDragons && Dragons.Contains(id))
            {
                int hp = npc.GetInt(id, "hp");
                if (hp > 1 && c.Set(npc, id, "hp", Math.Max(1, hp / 2))) dragons++;
            }
        }
        if (opt.MoreSouls) c.Log.Add($"{c.Label}: +{SoulBonusPercent - 100}% souls on {souls} enemies");
        if (opt.OneHitCrystalLizards) c.Log.Add($"{c.Label}: {lizards} Crystal Lizards set to 1 HP");
        if (opt.WeakerDragons) c.Log.Add($"{c.Label}: {dragons} dragon(s) at half HP");
    }

    /// <summary>
    /// ~1 MP/second while any chest armor is worn. Light chests have no resident effect, so the equipment
    /// MP-regen behavior is attached to them; medium/heavy chests already carry a stamina-down effect, so the
    /// MP regen is added into that same effect (its changeMpPoint is unused) without touching the stamina
    /// penalty. Either way every body armor is one — and only one — MP-regen source.
    /// </summary>
    static void ManaRegen(Ctx c, int intervalSeconds)
    {
        var sp = c.Param("SpEffectParam");
        if (sp != null && sp.HasField("motionInterval") && sp.HasField("changeMpPoint"))
        {
            if (sp.Has(MpRegenEffect)) c.Set(sp, MpRegenEffect, "motionInterval", intervalSeconds); // light-armor path (behavior 4200)
            foreach (var id in BodyStaminaEffects.Where(sp.Has)) // medium/heavy chest path
            {
                c.Set(sp, id, "changeMpPoint", -1); // negative = restore 1 MP
                c.Set(sp, id, "motionInterval", intervalSeconds);
            }
        }
        var prot = c.Param("EquipParamProtector");
        if (prot == null || !prot.HasField("residentSpEffectBehaviorId")) { c.Log.Add($"{c.Label}: no protector resident-effect layout, MP regen skipped"); return; }
        int n = 0;
        // Chest armor ("Armer" slot) ids are 200000-202999. Light bodies (no resident effect) get the regen
        // behavior; heavy ones keep their stamina effect (now also regenning), so nothing stacks.
        foreach (var id in prot.RowIds.Where(id => id is >= 200000 and < 203000))
            if (prot.GetInt(id, "residentSpEffectBehaviorId") <= 0 && c.Set(prot, id, "residentSpEffectBehaviorId", MpRegenBehavior)) n++;
        c.Log.Add($"{c.Label}: MP regen on every chest armor ({n} light + heavy via stamina effect)");
    }

    static void GiveToClasses(Ctx c, RawParam chara, int itemId, string itemName)
    {
        foreach (var cls in ClassRevamp.ClassIds.Where(chara.Has))
        {
            int free = -1;
            bool has = false;
            for (int i = 1; i <= 10; i++)
            {
                int it = chara.GetInt(cls, $"item_{i:00}");
                if (it == itemId) has = true;
                if (it <= 0 && free < 0) free = i;
            }
            if (has || free < 0) continue;
            c.Set(chara, cls, $"item_{free:00}", itemId);
            c.Set(chara, cls, $"itemNum_{free:00}", 1);
        }
        c.Log.Add($"{c.Label}: every starting class carries a {itemName}");
    }

    public static bool IsPatched(GameInfo game) =>
        FindParamBnds(game.UsrDir).Any(p => File.Exists(p + BackupSuffix) && !FilesEqual(p, p + BackupSuffix))
        || MsgPatcher.IsPatched(game.UsrDir) || ScriptPatcher.IsPatched(game.UsrDir)
        || ItemTextPatcher.IsPatched(game.UsrDir) || IconPatcher.IsPatched(game.UsrDir);

    public static void Restore(GameInfo game)
    {
        foreach (var p in FindParamBnds(game.UsrDir))
        {
            var b = p + BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
        MsgPatcher.Restore(game.UsrDir);
        ScriptPatcher.Restore(game.UsrDir);
        ItemTextPatcher.Restore(game.UsrDir);
        IconPatcher.Restore(game.UsrDir);
    }

    static bool FilesEqual(string a, string b) => File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
}
