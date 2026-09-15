namespace DesCoop.Game;

/// <summary>
/// Ten brand-new starting classes that replace the vanilla ones (CharaInitParam rows 1000-1009):
/// new name, stats and full kit. Every weapon is usable one-handed with the class's own stats
/// (checked against the retail requirements by the tests) and Soul Levels stay between 1 and 9.
/// Only item ids that exist in the retail game are used; anything missing is skipped with a log line.
/// </summary>
public static class ClassRevamp
{
    public static readonly int[] ClassIds = [1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009];

    public const int NoHelm = 101700, NoArmor = 201700, NoGloves = 301500, NoLegs = 401500;

    public sealed record Kit(
        int Id, string Original, string Name, string Pitch,
        int Vit, int Int, int End, int Str, int Dex, int Mag, int Fai, int Luc,
        int Right, int Right2, int Left, int Left2,
        int Helm, int Armor, int Gloves, int Legs,
        int Arrow, int ArrowNum, int Bolt, int BoltNum,
        int Ring1, int[] Spells, (int id, int num)[] Items)
    {
        public int SoulLevel => Vit + Int + End + Str + Dex + Mag + Fai + Luc - 80;
        public string Title => $"{Name} (replaces {Original})";
    }

    // Stats: VIT, INT, END, STR, DEX, MAG, FAI, LUCK. Soul Level = total - 80.
    // Weapons are the +0 retail ids; items: 1000 Crescent Moon Grass, 1001 Half Moon Grass, 1005 Fresh Spice,
    // 1011 Throwing Knife, 1012 Kunai, 1013 Firebomb, 1024 Secret Throwing Dagger, 99 Augite of Souls (lantern).
    public static readonly Kit[] Kits =
    [
        new(1000, "Soldier", "Sellsword", "Kilij and a Light Crossbow with 30 bolts, Soldier's Shield, chain mail. Paid in advance.",
            14, 8, 13, 14, 11, 7, 8, 12,
            40100, 140000, 150800, -1, 100400, 200400, 300400, 400400, -1, 0, 170000, 30,
            -1, [], [(1000, 8), (1013, 4), (99, 1)]),
        new(1001, "Knight", "Sentinel", "Knight Sword, Morning Star and Kite Shield in the Gloom armor. The wall that bleeds you.",
            12, 9, 13, 14, 10, 8, 11, 8,
            21300, 60300, 150200, -1, 101000, 201000, 301000, 401000, -1, 0, -1, 0,
            -1, [], [(1000, 8), (1001, 3), (99, 1)]),
        new(1002, "Hunter", "Tracker", "Short Spear, Long Bow with 60 arrows and Leather Shield in wild shaman garb.",
            11, 9, 12, 15, 13, 7, 7, 12,
            70000, 130200, 151500, -1, 100300, 200000, 300000, 400000, 160000, 60, -1, 0,
            -1, [], [(1000, 10), (1011, 10), (99, 1)]),
        new(1003, "Priest", "Friar", "Club and Talisman of God in the Binded robes. Heal and Regeneration for the whole party.",
            13, 10, 11, 12, 8, 7, 15, 9,
            60000, 90400, 151500, -1, 102000, 202000, 302000, 402000, -1, 0, -1, 0,
            -1, [2010, 2004], [(1000, 5), (1005, 4), (99, 1)]),
        new(1004, "Magician", "Sorcerer", "Soul Arrow and Fire Spray, a Kris Blade that scales with Magic, and the witch's hat.",
            9, 15, 10, 8, 11, 16, 6, 11,
            90000, 10600, 150100, -1, 102100, 202100, 302100, 402100, -1, 0, -1, 0,
            -1, [1000, 1017], [(1000, 6), (1005, 8), (99, 1)]),
        new(1005, "Wanderer", "Blade Dancer", "Shotel and Estoc, a Parrying Dagger in the off hand, black leathers. Parry, riposte, repeat.",
            10, 10, 12, 10, 16, 8, 7, 13,
            40200, 30100, 10100, -1, NoHelm, 200200, 300200, 400200, -1, 0, -1, 0,
            -1, [], [(1000, 8), (1012, 10), (99, 1)]),
        new(1006, "Barbarian", "Juggernaut", "20 Strength: Great Club in one hand, Battle Axe in the other, Brushwood helm. Shield optional.",
            15, 7, 13, 20, 9, 7, 8, 10,
            60400, -1, 50000, 150100, 100800, 200300, 300300, 400300, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1013, 5), (99, 1)]),
        new(1007, "Thief", "Nightblade", "Mail Breaker for criticals, Short Bow with 30 arrows, Buckler for parries and the Thief's Ring.",
            10, 11, 10, 12, 14, 9, 8, 15,
            10200, 130000, 150000, -1, 100200, 201800, 301800, 401800, 160000, 30, -1, 0,
            116, [], [(1000, 8), (1024, 5), (99, 1)]),
        new(1008, "Temple Knight", "Crusader", "Mirdan Hammer, Heater Shield and Talisman of God in full plate. Heal and Cure.",
            12, 8, 12, 14, 12, 6, 14, 6,
            80100, 90400, 150300, -1, 100600, 200600, 300600, 400600, -1, 0, -1, 0,
            -1, [2010, 2006], [(1000, 6), (1005, 3), (99, 1)]),
        new(1009, "Royalty", "Exiled Heir", "Soul Level 1. Scimitar, Silver Catalyst and Flame Toss, a Gold Mask and a Slave's Shield.",
            9, 12, 8, 9, 12, 13, 10, 8,
            40000, 90100, 151000, -1, 100000, 201200, 301200, 401200, -1, 0, -1, 0,
            102, [1001], [(1000, 6), (1005, 5), (99, 1)]),
    ];

    /// <summary>Old class name -> new class name, for the menu text.</summary>
    public static IReadOnlyDictionary<string, string> Renames => Kits.ToDictionary(k => k.Original, k => k.Name);

    public static void Apply(RawParam p, Action<RawParam, int, string, double> set, Func<string, int, bool> exists, List<string> log, string label)
    {
        int applied = 0;
        foreach (var k in Kits)
        {
            if (!p.Has(k.Id)) continue;
            void S(string f, double v) => set(p, k.Id, f, v);
            int Chk(string kind, int id) { if (id <= 0 || exists(kind, id)) return id; log.Add($"{label}: {k.Name}: {kind} {id} not in this game, skipped"); return -1; }

            S("baseVit", k.Vit); S("baseWil", k.Int); S("baseEnd", k.End); S("baseStr", k.Str);
            S("baseDex", k.Dex); S("baseMag", k.Mag); S("baseFai", k.Fai); S("baseLuc", k.Luc);
            S("equip_Wep_Right", Chk("weapon", k.Right)); S("equip_Subwep_Right", Chk("weapon", k.Right2));
            S("equip_Wep_Left", Chk("weapon", k.Left)); S("equip_Subwep_Left", Chk("weapon", k.Left2));
            S("equip_Helm", Chk("protector", k.Helm)); S("equip_Armer", Chk("protector", k.Armor));
            S("equip_Gaunt", Chk("protector", k.Gloves)); S("equip_Leg", Chk("protector", k.Legs));
            int arrow = Chk("weapon", k.Arrow), bolt = Chk("weapon", k.Bolt);
            S("equip_Arrow", arrow); S("arrowNum", arrow > 0 ? k.ArrowNum : 0);
            S("equip_Bolt", bolt); S("boltNum", bolt > 0 ? k.BoltNum : 0);
            S("equip_Accessory01", Chk("accessory", k.Ring1));
            for (int i = 1; i <= 7; i++) S($"equip_Spell_{i:00}", i <= k.Spells.Length ? Chk("magic", k.Spells[i - 1]) : -1);
            for (int i = 1; i <= 10; i++)
            {
                var (id, num) = i <= k.Items.Length ? k.Items[i - 1] : (-1, 0);
                id = Chk("goods", id);
                S($"item_{i:00}", id);
                S($"itemNum_{i:00}", id > 0 ? num : 0);
            }
            applied++;
        }
        log.Add($"{label}: {applied} starting classes replaced");
    }
}
