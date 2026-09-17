namespace DesCoop.Game;

/// <summary>
/// The ten starting classes (CharaInitParam rows 1000-1009), built to the owner's spec: two per focus,
/// exact stats and gear. Kit.Original must be the row's real vanilla class, because the menu shows each
/// row's own archetype name and <see cref="MsgPatcher"/> renames those (row 1000 Soldier, 1001 Knight,
/// 1002 Hunter, 1003 Priest, 1004 Magician, 1005 Wanderer, 1006 Barbarian, 1007 Thief, 1008 Temple Knight,
/// 1009 Royalty). Every armor piece is gender=3 (unisex) so it fits a male or female character; where the
/// requested set is female/male-only or does not exist it is swapped for the closest unisex look. Every
/// weapon is one-hand-equippable; casters carry a catalyst or talisman plus their spell.
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
        public string Title => $"{Name} — {Focus}";
    }

    // Weapons: 20600 Great Sword, 20400 Bastard Sword, 40400 Uchigatana, 130300 Compound Long Bow, 30100 Estoc,
    // 20200 Long Sword, 70100 Winged Spear, 60100 Mace, 80200 Halberd, 90000 Wooden Catalyst, 20000 Short Sword,
    // 90400 Talisman of God, 90100 Silver Catalyst, 10000 Dagger. Shields: 150200 Kite, 150900 Knight's,
    // 150600 Tower, 150400 Adjudicator's, 10100 Parrying Dagger. Spells: 1001 Flame Toss, 1003 Enchant Weapon;
    // miracles 2004 Regeneration, 2010 Heal. Goods: 1000 Crescent Moon Grass, 1001 Half Moon Grass,
    // 1002 Late Moon Grass, 1003 Full Moon Grass, 1005 Fresh Spice, 1006 Old Spice, 1013 Firebomb,
    // 1015 Turpentine, 1016 Black Turpentine, 1024 Secret Throwing Dagger, 99 Augite of Souls (lantern).
    public static readonly Kit[] Kits =
    [
        // ---- Strength ----
        new(1000, "Soldier", "Berserker", "Strength", "Great Sword in both hands, no shield, just rage.",
            15, 8, 14, 18, 10, 6, 6, 7,
            20600, -1, -1, -1, NoHelm, 200300, 300300, 400300, -1, 0, -1, 0,
            -1, [], [(1000, 5), (99, 1)]),
        new(1001, "Knight", "Warrior", "Strength", "Bastard Sword and Kite Shield in brushwood plate.",
            13, 9, 13, 16, 11, 6, 8, 8,
            20400, -1, 150200, -1, 100400, 200800, 300400, 400800, -1, 0, -1, 0,
            -1, [], [(1015, 4), (99, 1)]),
        // ---- Dexterity ----
        new(1002, "Hunter", "Samurai", "Dexterity", "Uchigatana and a Compound Long Bow with 30 arrows, in dark armor.",
            11, 10, 11, 14, 16, 8, 8, 7,
            40400, -1, 130300, -1, 100200, 201000, 300200, 401000, 160000, 30, -1, 0,
            -1, [], [(1016, 3), (99, 1)]),
        new(1005, "Wanderer", "Swordsman", "Dexterity", "Estoc and Parrying Dagger in black leather. Riposte everything.",
            10, 10, 12, 10, 17, 8, 8, 10,
            30100, -1, 10100, -1, 100200, 200200, 300200, 400200, -1, 0, -1, 0,
            -1, [], [(1024, 10), (99, 1)]),
        // ---- Strength/Dexterity (Quality) ----
        new(1006, "Barbarian", "Knight", "Strength/Dexterity", "Long Sword and Knight's Shield in fluted plate.",
            12, 11, 12, 14, 14, 9, 9, 7,
            20200, -1, 150900, -1, 100700, 200700, 300700, 400700, -1, 0, -1, 0,
            -1, [], [(1001, 4), (99, 1)]),
        new(1007, "Thief", "Squire", "Strength/Dexterity", "Winged Spear and Tower Shield in full plate. The turtle.",
            13, 10, 13, 13, 12, 8, 8, 8,
            70100, -1, 150600, -1, 100600, 200600, 300600, 400600, -1, 0, -1, 0,
            -1, [], [(1013, 3), (99, 1)]),
        // ---- Faith ----
        new(1003, "Priest", "Cleric", "Faith", "Mace and Talisman of God in chain mail. Heal for the party.",
            12, 10, 10, 11, 8, 6, 16, 8,
            60100, -1, 90400, -1, 100400, 200400, 300400, 400400, -1, 0, -1, 0,
            -1, [2010], [(1002, 4), (99, 1)]),
        new(1008, "Temple Knight", "Battle Priest", "Faith", "Halberd, Adjudicator's Shield and a Talisman in Mirdan mail. Regeneration.",
            11, 11, 11, 14, 12, 6, 14, 7,
            80200, -1, 150400, 90400, 100500, 200500, 300500, 400500, -1, 0, -1, 0,
            -1, [2004], [(1003, 4), (99, 1)]),
        // ---- Intelligence ----
        new(1004, "Magician", "Mage", "Intelligence", "Wooden Catalyst and a Dagger. Flame Toss. Pure glass cannon.",
            9, 16, 9, 9, 10, 16, 6, 9,
            90000, -1, 10000, -1, 100100, 200100, 300100, 400100, -1, 0, -1, 0,
            -1, [1001], [(1005, 4), (99, 1)]),
        new(1009, "Royalty", "Battle Mage", "Intelligence", "Short Sword and Silver Catalyst in black leather. Enchant Weapon.",
            11, 14, 11, 11, 11, 14, 6, 7,
            20000, -1, 90100, -1, NoHelm, 200200, 300200, 400200, -1, 0, -1, 0,
            -1, [1003], [(1006, 4), (99, 1)]),
    ];

    /// <summary>Old class name -> new class name, for the menu text. One entry per real vanilla class.</summary>
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
