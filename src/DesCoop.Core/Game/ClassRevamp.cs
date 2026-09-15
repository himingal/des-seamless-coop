namespace DesCoop.Game;

/// <summary>
/// Ten brand-new starting classes that replace the vanilla ones (CharaInitParam rows 1000-1009), grouped
/// two per focus: Strength, Dexterity, Strength/Dexterity, Faith and Intelligence. Every weapon is usable
/// one-handed with the class's own stats (checked against the retail requirements by the tests) and Soul
/// Levels stay between 1 and 9. Only item ids that exist in the retail game are used; anything missing is
/// skipped with a log line.
/// </summary>
public static class ClassRevamp
{
    public static readonly int[] ClassIds = [1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009];

    public const int NoHelm = 101700, NoArmor = 201700, NoGloves = 301500, NoLegs = 401500;

    public sealed record Kit(
        int Id, string Original, string Name, string Focus, string Pitch,
        int Vit, int Int, int End, int Str, int Dex, int Mag, int Fai, int Luc,
        int Right, int Right2, int Left, int Left2,
        int Helm, int Armor, int Gloves, int Legs,
        int Arrow, int ArrowNum, int Bolt, int BoltNum,
        int Ring1, int[] Spells, (int id, int num)[] Items)
    {
        public int SoulLevel => Vit + Int + End + Str + Dex + Mag + Fai + Luc - 80;
        public string Title => $"{Name} — {Focus} (replaces {Original})";
    }

    // Weapon ids (+0): 10000 Dagger, 10100 Parrying Dagger, 20000 Short Sword, 20200 Long Sword,
    // 20400 Bastard Sword, 30000 Rapier, 40400 Uchigatana, 50000 Battle Axe, 60000 Club, 60100 Mace,
    // 60300 Morning Star, 90000 Wooden Catalyst, 90100 Silver Catalyst, 90400 Talisman of God,
    // 150000 Buckler, 150200 Kite Shield, 150300 Heater Shield, 150800 Soldier's Shield, 151500 Leather Shield.
    // Spells: 1000 Soul Arrow, 1003 Enchant Weapon, 1017 Fire Spray. Miracles: 2004 Regeneration, 2006 Cure, 2010 Heal.
    public static readonly Kit[] Kits =
    [
        // --- Strength ---
        new(1000, "Soldier", "Berserker", "Strength", "Bastard Sword and Battle Axe, no shield, just rage. Firebombs for the rest.",
            15, 6, 14, 18, 9, 6, 7, 9,
            20400, 50000, -1, -1, 100800, 200400, 300400, 400400, -1, 0, -1, 0,
            -1, [], [(1000, 5), (1013, 5), (99, 1)]),
        new(1001, "Barbarian", "Warrior", "Strength", "Long Sword and a Heater Shield in fluted plate. The wall that hits back.",
            15, 6, 13, 16, 11, 6, 8, 9,
            20200, -1, 150300, -1, 100700, 200700, 300700, 400700, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1023, 3), (99, 1)]),
        // --- Dexterity ---
        new(1002, "Hunter", "Samurai", "Dexterity", "Uchigatana and a Buckler to parry. Bleed them, then riposte.",
            12, 6, 12, 18, 16, 6, 6, 8,
            40400, -1, 150000, -1, 100200, 200200, 300200, 400200, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1012, 8), (99, 1)]),
        new(1003, "Wanderer", "Swordsman", "Dexterity", "Rapier and Parrying Dagger in rogue's leathers. All footwork and criticals.",
            11, 8, 12, 10, 16, 7, 7, 13,
            30000, -1, 10100, -1, NoHelm, 201800, 301800, 401800, -1, 0, -1, 0,
            -1, [], [(1000, 8), (1011, 10), (99, 1)]),
        // --- Strength / Dexterity ---
        new(1004, "Knight", "Knight", "Strength/Dexterity", "Long Sword and Kite Shield in fluted armor. The balanced blade.",
            13, 7, 13, 14, 13, 6, 9, 9,
            20200, -1, 150200, -1, 100700, 200700, 300700, 400700, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1001, 3), (99, 1)]),
        new(1005, "Temple Knight", "Squire", "Strength/Dexterity", "Short Sword and Soldier's Shield in chain mail. The recruit who grows into anything.",
            12, 8, 12, 13, 13, 8, 8, 10,
            20000, -1, 150800, -1, 100400, 200400, 300400, 400400, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1015, 3), (99, 1)]),
        // --- Faith ---
        new(1006, "Priest", "Cleric", "Faith", "Mace and Talisman of God, Heal and Regeneration for the whole party.",
            13, 9, 12, 13, 9, 6, 15, 7,
            60100, -1, 90400, -1, 102000, 202000, 302000, 402000, -1, 0, -1, 0,
            -1, [2010, 2004], [(1000, 5), (99, 1)]),
        new(1007, "Royalty", "Battle Priest", "Faith", "Morning Star and Heater Shield in plate, Talisman on the hip. Heal and Cure between swings.",
            13, 8, 13, 14, 11, 6, 14, 5,
            60300, -1, 150300, 90400, 100600, 200600, 300600, 400600, -1, 0, -1, 0,
            -1, [2010, 2006], [(1000, 5), (99, 1)]),
        // --- Intelligence ---
        new(1008, "Magician", "Mage", "Intelligence", "Wooden Catalyst, Soul Arrow and Fire Spray, a Dagger for emergencies. Glass cannon.",
            9, 16, 10, 8, 10, 16, 6, 9,
            10000, -1, 90000, -1, 100100, 200100, 300100, 400100, -1, 0, -1, 0,
            -1, [1000, 1017], [(1000, 6), (99, 1)]),
        new(1009, "Thief", "Battle Mage", "Intelligence", "Short Sword and Silver Catalyst: Soul Arrow at range, Enchant Weapon up close.",
            11, 13, 12, 10, 12, 14, 6, 6,
            20000, -1, 90100, -1, 100200, 200200, 300200, 400200, -1, 0, -1, 0,
            -1, [1000, 1003], [(1000, 6), (99, 1)]),
    ];

    /// <summary>Old class name -> new class name, for the menu text.</summary>
    public static IReadOnlyDictionary<string, string> Renames => Kits.ToDictionary(k => k.Original, k => k.Name);

    public static void Apply(RawParam p, Func<RawParam, int, string, double, bool> set, Func<string, int, bool> exists, List<string> log, string label)
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
