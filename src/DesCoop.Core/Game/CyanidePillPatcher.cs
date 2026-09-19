using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// Adds a "Cyanide Pill": a consumable that instantly kills you, so the helper turns into a soul-form ghost
/// on demand (to place a summon sign) without having to farm a death. Built as three new param rows cloned
/// from a healing grass's own chain — Goods 9990 → BehaviorParam 9990 → SpEffectParam 9990 — with the
/// SpEffect's changeHpPoint flipped from healing (negative) to 9999 damage (positive) on self. Its name is
/// added to the item-name text. Pure new rows/entries, pristine backups kept, reversible, no EBOOT edits.
/// </summary>
public static class CyanidePillPatcher
{
    public const int GoodsId = 9990;               // referenced by the class kits and Blacksmith Ed's shop
    const int BehaviorId = 9990, SpEffectId = 9990;
    const int ProtoGoods = 1000, ProtoBehavior = 3000, ProtoSpEffect = 3000; // Crescent Moon Grass' own chain
    const int LethalDamage = 9999;                 // changeHpPoint > 0 = damage (grass heals with a negative)
    public const string PillName = "Cyanide Pill";

    static readonly Dictionary<string, Dictionary<string, PARAMDEF>> _defCache = [];

    static Dictionary<string, PARAMDEF>? Defs(string usrDir)
    {
        if (_defCache.TryGetValue(usrDir, out var cached)) return cached;
        try
        {
            var path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd.dcx");
            if (!File.Exists(path)) path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd");
            if (!File.Exists(path)) return null;
            var map = new Dictionary<string, PARAMDEF>();
            foreach (var f in BND3.Read(path).Files)
                try { var d = PARAMDEF.Read(f.Bytes); map[d.ParamType] = d; } catch { }
            _defCache[usrDir] = map;
            return map;
        }
        catch { return null; }
    }

    /// <summary>Adds the SpEffect, Behavior and Goods rows to the game param binder; returns rows added.</summary>
    public static int AddParams(BND3 bnd, string usrDir, List<string> log, string label)
    {
        var defs = Defs(usrDir);
        if (defs == null) { log.Add($"{label}: no paramdefs, cyanide pill skipped"); return 0; }

        int added = 0;
        added += AddClonedRow(bnd, defs, "SpEffectParam", "SP_EFFECT_PARAM_ST", ProtoSpEffect, SpEffectId,
            r => r["changeHpPoint"].Value = LethalDamage, log, label);
        added += AddClonedRow(bnd, defs, "BehaviorParam", "BEHAVIOR_PARAM_ST", ProtoBehavior, BehaviorId,
            r => r["spEffectId"].Value = SpEffectId, log, label);
        added += AddClonedRow(bnd, defs, "EquipParamGoods", "EQUIP_PARAM_GOODS_ST", ProtoGoods, GoodsId, r =>
        {
            r["behaviorId"].Value = BehaviorId;
            r["isConsume"].Value = (byte)1;
            if (r.Cells.Any(c => c.Def.InternalName == "sortId")) r["sortId"].Value = 9990;
        }, log, label);
        AddToShop(bnd, defs, log, label); // Blacksmith Ed also sells the pill
        if (added >= 3) log.Add($"{label}: Cyanide Pill added (goods {GoodsId})");
        return added;
    }

    /// <summary>Adds a Blacksmith Ed shop row selling the pill (goods 9990), in a free id of Ed's range.</summary>
    static void AddToShop(BND3 bnd, Dictionary<string, PARAMDEF> defs, List<string> log, string label)
    {
        if (!defs.TryGetValue("SHOP_LINEUP_PARAM", out var def)) return;
        var file = bnd.Files.FirstOrDefault(f => (f.Name ?? "").EndsWith("ShopLineupParam.param", StringComparison.OrdinalIgnoreCase));
        if (file == null) return;
        try
        {
            var param = PARAM.Read(file.Bytes);
            param.ApplyParamdef(def);
            var proto = param.Rows.FirstOrDefault(r => r.ID == 5000);
            if (proto == null) return;
            int id = 5029;
            while (param.Rows.Any(r => r.ID == id)) id++;
            if (id > 5049) return; // out of Ed's range
            var row = new PARAM.Row(id, null, def);
            foreach (var c in row.Cells)
                c.Value = proto.Cells.First(p => p.Def.InternalName == c.Def.InternalName).Value;
            row["equipType"].Value = (byte)3; // goods
            row["equipId"].Value = GoodsId;
            row["value"].Value = 500;
            if (row.Cells.Any(c => c.Def.InternalName == "mtrlId")) row["mtrlId"].Value = -1;
            if (row.Cells.Any(c => c.Def.InternalName == "eventFlag")) row["eventFlag"].Value = 0;
            if (row.Cells.Any(c => c.Def.InternalName == "qwcId")) row["qwcId"].Value = 0;
            param.Rows.Add(row);
            param.Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            file.Bytes = param.Write();
        }
        catch (Exception ex) { log.Add($"{label}: pill not added to shop ({ex.Message})"); }
    }

    static int AddClonedRow(BND3 bnd, Dictionary<string, PARAMDEF> defs, string fileSuffix, string paramType,
        int protoId, int newId, Action<PARAM.Row> tweak, List<string> log, string label)
    {
        if (!defs.TryGetValue(paramType, out var def)) { log.Add($"{label}: no {paramType} layout"); return 0; }
        var file = bnd.Files.FirstOrDefault(f => (f.Name ?? "").EndsWith(fileSuffix + ".param", StringComparison.OrdinalIgnoreCase));
        if (file == null) return 0;
        try
        {
            var param = PARAM.Read(file.Bytes);
            param.ApplyParamdef(def);
            if (param.Rows.Any(r => r.ID == newId)) return 1; // already present (idempotent)
            var proto = param.Rows.FirstOrDefault(r => r.ID == protoId);
            if (proto == null) { log.Add($"{label}: {paramType} row {protoId} not found"); return 0; }
            var row = new PARAM.Row(newId, null, def);
            foreach (var c in row.Cells)
                c.Value = proto.Cells.First(p => p.Def.InternalName == c.Def.InternalName).Value;
            tweak(row);
            param.Rows.Add(row);
            param.Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            file.Bytes = param.Write();
            return 1;
        }
        catch (Exception ex) { log.Add($"{label}: {paramType} not changed ({ex.Message})"); return 0; }
    }

    static IEnumerable<string> FindItemBnds(string usrDir)
    {
        var dir = Path.Combine(usrDir, "msg");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "item.msgbnd*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(GamePatcher.BackupSuffix, StringComparison.OrdinalIgnoreCase)
                && (f.EndsWith(".msgbnd", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msgbnd.dcx", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Adds the pill's name (id 9990) to the item-name text (every item.msgbnd, from its backup).</summary>
    public static bool AddName(string usrDir, List<string> log)
    {
        bool changed = false;
        foreach (var path in FindItemBnds(usrDir))
        {
            var backup = path + GamePatcher.BackupSuffix;
            var name = Path.GetRelativePath(Path.Combine(usrDir, "msg"), path);
            try
            {
                if (!File.Exists(backup)) File.Copy(path, backup);
                var bnd = BND3.Read(backup);
                int n = 0;
                foreach (var f in bnd.Files)
                {
                    FMG fmg;
                    try { fmg = FMG.Read(f.Bytes); } catch { continue; }
                    // The item-name FMG is the one that already holds goods names (e.g. Pure Bladestone = 2023).
                    if (!fmg.Entries.Any(e => e.ID == 2023)) continue;
                    var existing = fmg.Entries.FirstOrDefault(e => e.ID == GoodsId);
                    if (existing != null) existing.Text = PillName;
                    else fmg.Entries.Add(new FMG.Entry(GoodsId, PillName));
                    f.Bytes = MsgPatcher.WriteLikeOriginal(fmg, f.Bytes.Length);
                    n++;
                }
                if (n == 0) continue;
                var fresh = bnd.Write();
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(fresh)) { File.WriteAllBytes(path, fresh); changed = true; }
            }
            catch (Exception ex) { log.Add($"{name}: cyanide pill name not added ({ex.Message})"); }
        }
        return changed;
    }

    public static void Restore(string usrDir)
    {
        foreach (var p in FindItemBnds(usrDir))
        {
            var b = p + GamePatcher.BackupSuffix;
            if (File.Exists(b)) File.Copy(b, p, true);
        }
    }
}
