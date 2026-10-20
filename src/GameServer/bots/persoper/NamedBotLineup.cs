using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// The 33 named personality bots: one per type per realm. The master for names, classes and
/// characters is docs/BOT-PERSONALITIES.md; this list must match it.
/// Pure data only, so it can be unit tested without a running world.
/// </summary>
public static class NamedBotLineup
{
    /// <summary>
    /// One named bot. <see cref="Gender"/> is null for "Random": picked once when the bot is
    /// created, then fixed. <see cref="TypeKey"/> is the personality type ("joker"); the first
    /// six match <see cref="PersonalityTypes"/>, the last five exist only as named bots.
    /// </summary>
    public sealed record Entry(string Key, string Name, eRealm Realm, eCharacterClass CharacterClass, eRace Race, eGender? Gender, string TypeKey);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("hardcore-mid", "xXxNataSehSxXx", eRealm.Midgard, eCharacterClass.Shadowblade, eRace.Kobold, eGender.Female, "hardcore"),
        new("hardcore-alb", "xAxNataSehSxAx", eRealm.Albion, eCharacterClass.Infiltrator, eRace.Inconnu, eGender.Female, "hardcore"),
        new("hardcore-hib", "xHxNataSehSxHx", eRealm.Hibernia, eCharacterClass.Nightshade, eRace.Lurikeen, eGender.Female, "hardcore"),

        new("casual-mid", "Shempenstein", eRealm.Midgard, eCharacterClass.Warrior, eRace.Valkyn, eGender.Male, "casual"),
        new("casual-alb", "Shempenshire", eRealm.Albion, eCharacterClass.Armsman, eRace.Saracen, eGender.Male, "casual"),
        new("casual-hib", "Shempenmore", eRealm.Hibernia, eCharacterClass.Hero, eRace.Sylvan, eGender.Male, "casual"),

        new("leeroy-mid", "TheriousTham", eRealm.Midgard, eCharacterClass.Thane, eRace.Troll, eGender.Male, "leeroy"),
        new("leeroy-alb", "TheriousThamm", eRealm.Albion, eCharacterClass.Paladin, eRace.Highlander, eGender.Male, "leeroy"),
        new("leeroy-hib", "TheriousThamh", eRealm.Hibernia, eCharacterClass.Champion, eRace.Celt, eGender.Male, "leeroy"),

        new("loner-mid", "FemurTamer", eRealm.Midgard, eCharacterClass.Bonedancer, eRace.Valkyn, eGender.Female, "loner"),
        new("loner-alb", "Deaditetamer", eRealm.Albion, eCharacterClass.Necromancer, eRace.Saracen, eGender.Female, "loner"),
        new("loner-hib", "Shroomtamer", eRealm.Hibernia, eCharacterClass.Animist, eRace.Sylvan, eGender.Female, "loner"),

        new("minmaxer-mid", "Redtornader", eRealm.Midgard, eCharacterClass.Spiritmaster, eRace.Norseman, eGender.Male, "minmaxer"),
        new("minmaxer-alb", "Greenfogger", eRealm.Albion, eCharacterClass.Cabalist, eRace.Briton, eGender.Male, "minmaxer"),
        new("minmaxer-hib", "Sunrayder", eRealm.Hibernia, eCharacterClass.Enchanter, eRace.Elf, eGender.Male, "minmaxer"),

        new("joker-mid", "Shnacksize", eRealm.Midgard, eCharacterClass.Berserker, eRace.Valkyn, eGender.Female, "joker"),
        new("joker-alb", "Shnacksizer", eRealm.Albion, eCharacterClass.Mercenary, eRace.Saracen, eGender.Female, "joker"),
        new("joker-hib", "Shnacksizey", eRealm.Hibernia, eCharacterClass.Blademaster, eRace.Elf, eGender.Female, "joker"),

        new("crafter-mid", "Muckaduck", eRealm.Midgard, eCharacterClass.Runemaster, eRace.Norseman, eGender.Female, "crafter"),
        new("crafter-alb", "Muckaducke", eRealm.Albion, eCharacterClass.Wizard, eRace.Briton, eGender.Female, "crafter"),
        new("crafter-hib", "Muckaduckeen", eRealm.Hibernia, eCharacterClass.Eldritch, eRace.Elf, eGender.Female, "crafter"),

        new("roleplayer-mid", "Hunterson", eRealm.Midgard, eCharacterClass.Hunter, eRace.Norseman, eGender.Male, "roleplayer"),
        new("roleplayer-alb", "FitzHunter", eRealm.Albion, eCharacterClass.Scout, eRace.Briton, eGender.Male, "roleplayer"),
        new("roleplayer-hib", "MacHunter", eRealm.Hibernia, eCharacterClass.Ranger, eRace.Celt, eGender.Male, "roleplayer"),

        new("homeowner-mid", "Barbalicious", eRealm.Midgard, eCharacterClass.Savage, eRace.Troll, null, "homeowner"),
        new("homeowner-alb", "Barbaliciouse", eRealm.Albion, eCharacterClass.Reaver, eRace.Inconnu, null, "homeowner"),
        new("homeowner-hib", "Barbalicioush", eRealm.Hibernia, eCharacterClass.Valewalker, eRace.Firbolg, null, "homeowner"),

        new("herbaldoctor-mid", "Spyburd", eRealm.Midgard, eCharacterClass.Shaman, eRace.Kobold, eGender.Male, "herbaldoctor"),
        new("herbaldoctor-alb", "Spyburde", eRealm.Albion, eCharacterClass.Friar, eRace.Briton, eGender.Male, "herbaldoctor"),
        new("herbaldoctor-hib", "Spyburdagh", eRealm.Hibernia, eCharacterClass.Warden, eRace.Celt, eGender.Male, "herbaldoctor"),

        new("gruffdoc-mid", "Eaglefang", eRealm.Midgard, eCharacterClass.Healer, eRace.Dwarf, null, "gruffdoc"),
        new("gruffdoc-alb", "Eaglefange", eRealm.Albion, eCharacterClass.Cleric, eRace.Highlander, null, "gruffdoc"),
        new("gruffdoc-hib", "Eaglefangh", eRealm.Hibernia, eCharacterClass.Druid, eRace.Firbolg, null, "gruffdoc"),
    ];

    private static readonly HashSet<string> Names = new(All.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);

    /// <summary>The entry with this name (any case), or null.</summary>
    public static Entry FindByName(string name) =>
        string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when a character or bot name is one of the 33, in any case. Exact names only, so
    /// players can still pick look-alikes; a named bot is matched by its roster id, not its name.
    /// </summary>
    public static bool IsReservedName(string name) =>
        !string.IsNullOrEmpty(name) && Names.Contains(name);
}
