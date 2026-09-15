namespace DesCoop.Game;

/// <summary>
/// New identities for the ten starting classes (CharaInitParam rows 1000-1009). Each keeps its
/// original stat total, so starting Soul Levels and level-up costs stay where the game expects them.
/// Only item ids that exist in the retail game are used; anything missing is skipped with a log line.
/// </summary>
public static class ClassRevamp
{
    public static readonly int[] ClassIds = [1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009];

    public sealed record Kit(
        int Id, string Title, string Pitch,
        int Vit, int Int, int End, int Str, int Dex, int Mag, int Fai, int Luc,
        int Right, int Right2, int Left, int Left2,
        int Helm, int Armor, int Gloves, int Legs,
        int Arrow, int ArrowNum, int Bolt, int BoltNum,
        int Ring1, int[] Spells, (int id, int num)[] Items);

    // Stats: VIT, INT, END, STR, DEX, MAG, FAI, LUCK (same totals as vanilla).
    // Ids: weapons base (+0), armor sets, rings, spells and goods from the retail tables.
    public static readonly Kit[] Kits =
    [
        new(1000, "Soldier: Vanguard", "Front-line wall: Broadsword, Winged Spear reach and a Heater Shield in full plate.",
            15, 8, 14, 13, 11, 7, 8, 10,
            20100, 70100, 150300, -1, 100600, 200600, 300600, 400600, -1, 0, -1, 0,
            -1, [], [(1000, 12), (1023, 4), (1013, 3), (99, 1)]),
        new(1001, "Knight: Oathsworn", "Long Sword and Mace, Kite Shield, fluted armor. Hits hard, never flinches.",
            12, 9, 12, 15, 11, 8, 10, 7,
            20200, 60100, 150200, -1, 100700, 200700, 300700, 400700, -1, 0, -1, 0,
            -1, [], [(1000, 8), (1001, 4), (1007, 2), (99, 1)]),
        new(1002, "Hunter: Ranger", "Scimitar and Compound Short Bow with a quiver of 60 arrows. Kite everything.",
            11, 9, 13, 10, 15, 8, 8, 12,
            40000, 130100, 151500, -1, 100300, 200300, 300300, 400300, 160000, 60, -1, 0,
            -1, [], [(1000, 10), (1011, 10), (1022, 5), (99, 1)]),
        new(1003, "Priest: Battle Cleric", "Morning Star for bleeding foes, Talisman of God, Heal and Antidote.",
            14, 11, 12, 13, 8, 6, 14, 8,
            60300, 90400, 150300, -1, 100400, 200400, 300400, 400400, -1, 0, -1, 0,
            -1, [2010, 2011], [(1000, 5), (1005, 5), (99, 1)]),
        new(1004, "Magician: Arcanist", "Pure glass cannon: 17 Magic, Soul Arrow and Flame Toss, a dagger for emergencies.",
            9, 16, 9, 8, 10, 17, 6, 11,
            10000, 90000, 150000, -1, 101700, 200100, 300100, 400100, -1, 0, -1, 0,
            -1, [1000, 1001], [(1000, 6), (1005, 8), (99, 1)]),
        new(1005, "Wanderer: Duelist", "Uchigatana and a Parrying Dagger in rogue's leathers. Riposte everything.",
            10, 10, 12, 10, 16, 9, 7, 12,
            40400, -1, 10100, -1, 101700, 201800, 301800, 401800, -1, 0, -1, 0,
            -1, [], [(1000, 8), (1012, 10), (1015, 2), (99, 1)]),
        new(1006, "Barbarian: Berserker", "Battle Axe and Club, shaman garb, firebombs. Strength first, questions never.",
            16, 7, 14, 17, 9, 8, 8, 10,
            50000, 60000, 150100, -1, 101700, 200000, 300000, 400000, -1, 0, -1, 0,
            -1, [], [(1000, 6), (1014, 3), (1013, 5), (99, 1)]),
        new(1007, "Thief: Shadow", "Mail Breaker, Short Bow and the Thief's Ring. Unseen, unheard, 16 Luck.",
            10, 12, 10, 9, 14, 10, 8, 16,
            10200, 130000, 150000, -1, 100200, 200200, 300200, 400200, 160000, 30, -1, 0,
            116, [], [(1000, 8), (1011, 10), (1024, 5), (99, 1)]),
        new(1008, "Temple Knight: Paladin", "Halberd and Talisman, Mirdan plate, Heal and Antidote. Faith with a big stick.",
            12, 8, 13, 14, 10, 6, 14, 7,
            80200, 90400, 150300, -1, 100500, 200500, 300500, 400500, -1, 0, -1, 0,
            -1, [2010, 2011], [(1000, 4), (1023, 2), (99, 1)]),
        new(1009, "Royalty: Exiled Heir", "Rapier and Silver Catalyst, Fragrant Ring, Soul Arrow. Still born lucky.",
            9, 13, 8, 9, 12, 13, 11, 6,
            30000, 90100, 150000, -1, 100100, 200100, 300100, 400100, -1, 0, -1, 0,
            105, [1000], [(1001, 4), (1007, 3), (99, 1)]),
    ];

    public static void Apply(RawParam p, Action<RawParam, int, string, double> set, Func<string, int, bool> exists, List<string> log, string label)
    {
        int applied = 0;
        foreach (var k in Kits)
        {
            if (!p.Has(k.Id)) continue;
            void S(string f, double v) => set(p, k.Id, f, v);
            int Chk(string kind, int id) { if (id <= 0 || exists(kind, id)) return id; log.Add($"{label}: {k.Title}: {kind} {id} not in this game, skipped"); return -1; }

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
        log.Add($"{label}: {applied} starting classes revamped");
    }
}
