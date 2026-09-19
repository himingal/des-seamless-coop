using SoulsFormats;

namespace DesCoop.Game;

/// <summary>
/// Adds the world-tendency-locked rarities to Blacksmith Boldwin's Nexus shop, so a co-op run never
/// permanently misses them. Implemented as extra <c>ShopLineupParam</c> rows (SoulsFormats PARAM, since these
/// are NEW rows, not in-place edits) cloned from one of Boldwin's own unconditional rows (id 9000) — same
/// shop menu, no tendency (qwcId) or event-flag gate, paid with souls. His buy menu opens rows [9000, 9099]
/// (OpenRegularShop in the talk ESD) and only 9000-9008 exist, so the new rows (9009+) are displayed. Pure
/// param, pristine backup kept, fully reversible.
/// </summary>
public static class MerchantPatcher
{
    /// <summary>Blacksmith Boldwin's Nexus shop opens rows [9000, 9099] (confirmed in the talk ESD:
    /// OpenRegularShop(9000, 9099)); only 9000-9008 exist, so 9009+ is free and displayed. Ed's block (5000)
    /// is a weapon-upgrade menu, not a buy shop, which is why added rows never showed there.</summary>
    const int ProtoRowId = 9000;
    const int FirstNewId = 9009;
    const int LastNewId = 9099;

    /// <summary>Extra stock: (equipType 0=weapon/1=protector/3=goods, id, soul price). Ids resolved by the
    /// retail item ids of the NA dump (verified against the game's own name FMGs).</summary>
    static readonly (byte Type, int Id, int Price)[] Stock =
    [
        // Unique world-tendency weapons (Pure White / Pure Black exclusives, or one-shot upgrades)
        (0, 90500, 30000), // Talisman of Beasts
        (0, 80300, 30000), // Phosphorescent Pole
        (0, 20700, 30000), // Dragon Bone Smasher
        (0, 40700, 30000), // Magic Sword "Makoto"
        (0, 70300, 30000), // Istarelle
        (0, 40600, 30000), // Blind
        (0, 21600, 30000), // Large Sword of Moonlight
        (0, 21700, 30000), // Blueblood Sword
        (1, 180000, 10000), // Monk's Head Wrappings (head armor)
        (3, 34, 10000), // Colorless Demon's Soul
        // Every Pure upgrade stone (the rarest tier, often locked behind world/character tendency)
        (3, 2003, 5000), (3, 2007, 5000), (3, 2014, 5000), (3, 2017, 5000), (3, 2023, 5000),
        (3, 2026, 5000), (3, 2029, 5000), (3, 2032, 5000), (3, 2035, 5000), (3, 2038, 5000),
        (3, 2041, 5000), (3, 2044, 5000), (3, 2047, 5000), (3, 2050, 5000),
    ];

    static readonly Dictionary<string, PARAMDEF?> _defCache = [];

    static PARAMDEF? ShopDef(string usrDir)
    {
        if (_defCache.TryGetValue(usrDir, out var cached)) return cached;
        PARAMDEF? def = null;
        try
        {
            var path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd.dcx");
            if (!File.Exists(path)) path = Path.Combine(usrDir, "paramdef", "paramdef.paramdefbnd");
            if (File.Exists(path))
                foreach (var f in BND3.Read(path).Files)
                {
                    try { var d = PARAMDEF.Read(f.Bytes); if (d.ParamType == "SHOP_LINEUP_PARAM") { def = d; break; } }
                    catch { }
                }
        }
        catch { }
        _defCache[usrDir] = def;
        return def;
    }

    /// <summary>Adds the bonus rows to ShopLineupParam inside <paramref name="bnd"/>; returns how many were added.</summary>
    public static int AddBonusStock(BND3 bnd, string usrDir, List<string> log, string label)
    {
        var def = ShopDef(usrDir);
        if (def == null) { log.Add($"{label}: no SHOP_LINEUP_PARAM layout, bonus merchant skipped"); return 0; }
        var file = bnd.Files.FirstOrDefault(f => (f.Name ?? "").EndsWith("ShopLineupParam.param", StringComparison.OrdinalIgnoreCase));
        if (file == null) return 0; // some region parambnds may not carry it

        PARAM param;
        try { param = PARAM.Read(file.Bytes); param.ApplyParamdef(def); }
        catch (Exception ex) { log.Add($"{label}: could not open ShopLineupParam ({ex.Message})"); return 0; }

        var proto = param.Rows.FirstOrDefault(r => r.ID == ProtoRowId) ?? param.Rows.FirstOrDefault();
        if (proto == null) { log.Add($"{label}: ShopLineupParam has no rows, bonus merchant skipped"); return 0; }

        bool Has(string f) => param.AppliedParamdef!.Fields.Any(x => x.InternalName == f);
        int id = FirstNewId, added = 0;
        foreach (var (type, itemId, price) in Stock)
        {
            while (param.Rows.Any(r => r.ID == id)) id++; // never collide with a real row
            if (id > LastNewId) break;                     // stay inside Boldwin's displayed range
            var row = new PARAM.Row(id, null, def);
            foreach (var c in row.Cells)
                c.Value = proto.Cells.First(p => p.Def.InternalName == c.Def.InternalName).Value;
            row["equipType"].Value = type;
            row["equipId"].Value = itemId;
            row["value"].Value = price;
            if (Has("mtrlId")) row["mtrlId"].Value = -1;        // paid with souls, not materials
            if (Has("eventFlag")) row["eventFlag"].Value = -1;  // -1 = always shown (0 would gate on flag 0 = hidden)
            if (Has("qwcId")) row["qwcId"].Value = -1;          // -1 = no world/character-tendency requirement
            param.Rows.Add(row);
            id++; added++;
        }
        param.Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
        file.Bytes = param.Write();
        log.Add($"{label}: bonus merchant — {added} rare item(s) added to Blacksmith Boldwin");
        return added;
    }
}
