using SoulsFormats;
using static SoulsFormats.PARAMDEF;

namespace DesCoop.Game;

public sealed class PatchOptions
{
    /// <summary>Blue Eye Stone usable in body form: re-place your sign right after a boss without dying.</summary>
    public bool BlueEyeStoneInBodyForm { get; set; } = true;
    /// <summary>Stone of Ephemeral Eyes is not consumed: the host can always get body form back to summon.</summary>
    public bool InfiniteEphemeralEyes { get; set; } = true;
    /// <summary>Soul form keeps 100% max HP instead of 50%: dying costs nothing, it plays like body form.</summary>
    public bool FullHpSoulForm { get; set; } = true;
    /// <summary>Every starting class carries a Blue Eye Stone and a Stone of Ephemeral Eyes from the first second.</summary>
    public bool StartWithBlueEyeStone { get; set; } = true;
    /// <summary>Ten new starting classes (names, stats and kits) replace the vanilla ones.</summary>
    public bool RevampedClasses { get; set; } = true;
    /// <summary>Everything sold by NPCs costs half.</summary>
    public bool CheaperShops { get; set; } = true;
    /// <summary>Pure Bladestone drops from the Shrine of Storms skeletons 15% of the time instead of 0.5%.</summary>
    public bool EasierPureBladestone { get; set; } = true;
    /// <summary>Weapons, armor, rings and items weigh 1/3 less: +50% equip load and item burden.</summary>
    public bool HeavierLoads { get; set; } = true;
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
    /// <summary>SpEffectParam row "[System] parameter change while a ghost" (soul form and blue phantoms): maxHpRate 0.5.</summary>
    public const int SoulFormEffect = 8;

    static readonly string[] BlueEyeNames = ["Blue Eye Stone"];
    static readonly string[] EphemeralNames = ["Stone of Ephemeral Eyes"];
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
            var fresh = bnd.Write();
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh))
            {
                File.WriteAllBytes(path, fresh);
                changed = true;
            }
            log.Add($"{Path.GetFileName(path)}: {n} change(s)");
        }
        if (MsgPatcher.Apply(game.UsrDir, opt.RevampedClasses ? ClassRevamp.Renames : null, log)) changed = true;
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

        public void Set(RawParam p, int id, string field, double v)
        {
            if (p.Set(id, field, v)) Changes++;
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
        if (opt.EasierPureBladestone) BoostDrop(c, ids.PureBladestone.Count > 0 ? ids.PureBladestone : [2023]);

        var chara = c.Param("CharaInitParam");
        if (chara != null)
        {
            if (opt.RevampedClasses) ClassRevamp.Apply(chara, c.Set, Exists(c), log, label);
            if (opt.StartWithBlueEyeStone && goods != null)
            {
                foreach (var blue in ids.Blue.Where(goods.Has).Take(1)) GiveToClasses(c, chara, blue, "Blue Eye Stone");
                foreach (var eph in ids.Ephemeral.Where(goods.Has).Take(1)) GiveToClasses(c, chara, eph, "Stone of Ephemeral Eyes");
            }
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

    /// <summary>Raises the chance of an item in every lot that can drop it, taking the weight from the most common entry.</summary>
    static void BoostDrop(Ctx c, HashSet<int> itemIds)
    {
        var lots = c.Param("ItemLotParam");
        if (lots == null) return;
        foreach (var lot in lots.RowIds)
        {
            for (int i = 1; i <= 8; i++)
            {
                if (lots.GetInt(lot, $"lotItemCategory{i:00}") != GoodsCategory || !itemIds.Contains(lots.GetInt(lot, $"lotItemId{i:00}"))) continue;
                int pts = lots.GetInt(lot, $"lotItemBasePoint{i:00}");
                if (pts >= PureBladestoneChance) continue;
                int need = PureBladestoneChance - pts;
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
                c.Set(lots, lot, $"lotItemBasePoint{i:00}", PureBladestoneChance - need);
                c.Log.Add($"{c.Label}: drop lot {lot}: Pure Bladestone {pts / 10.0:0.#}% -> {(PureBladestoneChance - need) / 10.0:0.#}%");
            }
        }
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
        || MsgPatcher.IsPatched(game.UsrDir);

    public static void Restore(GameInfo game)
    {
        foreach (var p in FindParamBnds(game.UsrDir))
        {
            var b = p + BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
        MsgPatcher.Restore(game.UsrDir);
    }

    static bool FilesEqual(string a, string b) => File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
}
