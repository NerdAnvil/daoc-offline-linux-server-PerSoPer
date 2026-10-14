using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>
/// Persoper's fixed identity: her reserved first name, the healer class she
/// takes in each realm, and the punny surnames she picks for herself. Pure
/// logic only, so it can be unit tested without a running world.
/// </summary>
public static partial class PersoperIdentity
{
    public const string FirstName = "Persoper";
    public const int MaxSurnameLength = 14;
    public const int SurnameChangeCapPercent = 100;

    public enum eGreeting
    {
        FirstMeeting,
        Returning,
        NewSurname,
    }

    // Surnames are one capitalised word of letters, so the full name stays
    // short and works with name-based commands such as /tele.
    [GeneratedRegex("^[A-Z][a-z]{2,13}$")]
    private static partial Regex SurnameShape();

    private static readonly string[] GenericClean =
        ["Mendwell", "Patchwell", "Glowsworth", "Outrageous", "Showtime", "Glitterheart", "Sparklemend"];

    private static readonly string[] GenericCheeky =
        ["Lovesworth", "Bedsidemanner", "Kissitbetter", "Feelgood", "Laidhands", "Rubsalot", "Touchwell"];

    private static readonly Dictionary<eCharacterClass, string[]> ClassClean = new()
    {
        [eCharacterClass.Healer] = ["Healsalot", "Mendsalot", "Rezzalot", "Buffington", "Stitchwell", "Mezmerize"],
        [eCharacterClass.Cleric] = ["Smitesworth", "Blessington", "Prayalot", "Haloway"],
        [eCharacterClass.Druid] = ["Leafsalot", "Barkwell", "Mossgrove", "Rootwell"],
    };

    private static readonly Dictionary<eCharacterClass, string[]> ClassCheeky = new()
    {
        [eCharacterClass.Healer] = ["Healsgood", "Healsandheels", "Rezurrection"],
        [eCharacterClass.Cleric] = ["Kneelsworth", "Holywater", "Sinsworth", "Prayforplay"],
        [eCharacterClass.Druid] = ["Bushwhacker", "Mossbottom", "Treehugger", "Morningwood"],
    };

    private static readonly Dictionary<eRace, string[]> RaceClean = new()
    {
        [eRace.Dwarf] = ["Stoutheart", "Anvilhugs"],
        [eRace.Norseman] = ["Fjordsworth", "Fjordable"],
        [eRace.Briton] = ["Teatime"],
        [eRace.Avalonian] = ["Avalovely"],
        [eRace.Highlander] = ["Kiltworthy"],
        [eRace.Celt] = ["Celtsworth"],
        [eRace.Firbolg] = ["Bigbolg"],
        [eRace.Sylvan] = ["Treeline"],
    };

    private static readonly Dictionary<eRace, string[]> RaceCheeky = new()
    {
        [eRace.Dwarf] = ["Shortnsweet", "Shortstack"],
        [eRace.Norseman] = ["Longship", "Ravishing"],
        [eRace.Briton] = ["Crumpets"],
        [eRace.Highlander] = ["Underthekilt"],
        [eRace.Firbolg] = ["Bolgtastic"],
        [eRace.Sylvan] = ["Sapsalot"],
    };

    // {0} full name, {1} surname, {2} player name.
    private static readonly Dictionary<eGreeting, string[]> GreetingsClean = new()
    {
        [eGreeting.FirstMeeting] =
        [
            "Hello, {2}! I'm {0}. Yes, {1}. I picked it myself.",
            "{0}, reporting for healing duty. Try not to need me too much, {2}.",
        ],
        [eGreeting.Returning] =
        [
            "{0}, back again. Did you miss me, {2}?",
            "Still {1}, still fabulous. Lead the way, {2}.",
        ],
        [eGreeting.NewSurname] =
        [
            "Fresh start, {2}! From now on I'm {0}.",
            "I've decided {1} suits me better. Do keep up, {2}.",
        ],
    };

    private static readonly Dictionary<eGreeting, string[]> GreetingsCheeky = new()
    {
        [eGreeting.FirstMeeting] =
        [
            "Well hello, {2}. I'm {0}. Stick close, I'm very hands-on.",
            "The name's {0}. {1}, darling. Say it slowly.",
        ],
        [eGreeting.Returning] =
        [
            "Miss me, {2}? {0} is back, and so are these healing hands.",
            "{0}, at your service. All of it, {2}.",
        ],
        [eGreeting.NewSurname] =
        [
            "New name, same curves. Call me {0}, {2}.",
            "I felt like slipping into something new: {0}.",
        ],
    };

    private static readonly string[] OffClassGrumbles =
    [
        "A {0} this time? Fine, but I'm a Healer at heart.",
        "{0} duty again. Midgard would never do this to me.",
    ];

    /// <summary>True when a character or bot name would impersonate Persoper.</summary>
    public static bool IsReservedName(string name) =>
        !string.IsNullOrEmpty(name) && name.Contains(FirstName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The healer class Persoper takes for a realm. Midgard Healer is her favourite.</summary>
    public static eCharacterClass HealerClassFor(eRealm realm) => realm switch
    {
        eRealm.Albion => eCharacterClass.Cleric,
        eRealm.Hibernia => eCharacterClass.Druid,
        _ => eCharacterClass.Healer,
    };

    public static string FullName(string surname) =>
        string.IsNullOrWhiteSpace(surname) ? FirstName : $"{FirstName} {surname}";

    public static bool IsValidSurname(string surname) =>
        !string.IsNullOrEmpty(surname) && surname.Length <= MaxSurnameLength && SurnameShape().IsMatch(surname);

    /// <summary>Every surname she may pick for this class and race.</summary>
    public static IReadOnlyList<string> SurnamePool(eCharacterClass characterClass, eRace race, bool toneDown)
    {
        IEnumerable<string> pool = GenericClean
            .Concat(ClassClean.GetValueOrDefault(characterClass, []))
            .Concat(RaceClean.GetValueOrDefault(race, []));
        if (!toneDown)
        {
            pool = pool
                .Concat(GenericCheeky)
                .Concat(ClassCheeky.GetValueOrDefault(characterClass, []))
                .Concat(RaceCheeky.GetValueOrDefault(race, []));
        }

        return pool.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>True when the surname is acceptable with the tone-down setting on.</summary>
    public static bool IsCleanSurname(string surname) =>
        !string.IsNullOrEmpty(surname) &&
        !GenericCheeky.Concat(ClassCheeky.Values.SelectMany(names => names))
            .Concat(RaceCheeky.Values.SelectMany(names => names))
            .Contains(surname, StringComparer.Ordinal);

    /// <summary>Picks a surname, never repeating the current one when another exists.</summary>
    public static string ChooseSurname(eCharacterClass characterClass, eRace race, bool toneDown, string current, Random random)
    {
        random ??= Random.Shared;
        IReadOnlyList<string> pool = SurnamePool(characterClass, race, toneDown);
        string[] fresh = pool.Where(name => !string.Equals(name, current, StringComparison.Ordinal)).ToArray();
        IReadOnlyList<string> choices = fresh.Length > 0 ? fresh : pool;
        return choices[random.Next(choices.Count)];
    }

    /// <summary>Rolls her rare whim to change surname; percent is clamped to 0-100.</summary>
    public static bool ShouldChangeSurname(int percent, Random random)
    {
        random ??= Random.Shared;
        int chance = Math.Clamp(percent, 0, SurnameChangeCapPercent);
        return chance > 0 && random.Next(100) < chance;
    }

    public static string Greeting(eGreeting kind, string surname, string playerName, bool toneDown, Random random)
    {
        random ??= Random.Shared;
        string[] lines = toneDown || random.Next(2) == 0 ? GreetingsClean[kind] : GreetingsCheeky[kind];
        return string.Format(lines[random.Next(lines.Length)], FullName(surname), surname, playerName);
    }

    /// <summary>A grumble when she isn't her favourite class, or null when she is.</summary>
    public static string OffClassGrumble(eCharacterClass characterClass, Random random)
    {
        if (characterClass == eCharacterClass.Healer)
            return null;
        random ??= Random.Shared;
        return string.Format(OffClassGrumbles[random.Next(OffClassGrumbles.Length)], characterClass);
    }
}
