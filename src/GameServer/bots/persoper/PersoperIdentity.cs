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

    // Her chat lines: dialogue/persoper.dlg. Declared first, as the tables below read it.
    private static readonly IdentityDialogue Dialogue = new("persoper");

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

    // Greetings: {0} full name, {1} surname, {2} player name. Lines are in persoper.dlg.
    private static Dictionary<eGreeting, string[]> Greetings(string section) => new()
    {
        [eGreeting.FirstMeeting] = Dialogue.Lines(section, "first_meeting"),
        [eGreeting.Returning] = Dialogue.Lines(section, "returning"),
        [eGreeting.NewSurname] = Dialogue.Lines(section, "new_surname"),
    };

    private static Dictionary<eGreeting, string[]> GreetingsClean => Greetings("greeting");

    private static Dictionary<eGreeting, string[]> GreetingsCheeky => Greetings("cheeky_greeting");

    private static readonly string[] OffClassGrumbles =
    [
        "A {0} this time? Fine, but I'm a Healer at heart.",
        "{0} duty again. Midgard would never do this to me.",
    ];

    // {0} is whoever spoke to her. Clean lines always; cheeky ones only without tone-down.
    public static readonly BotPersonality Personality = new()
    {
        Name = FirstName,
        Key = "persoper",
        Matches = bot => IsReservedName(bot.Name),
        Memory = new MemoryVoice
        {
            Warm =
            [
                "Always happy to see you, {0}.",
                "That's the {1} time you've called me, {0}. I'm counting. Fondly.",
                "{2} without me, {0}? I missed you. A little.",
            ],
            Cool =
            [
                "Oh. You again, {0}.",
                "Last time you said \"{3}\". I remember, {0}.",
                "{2} of peace, and now you're back, {0}. Not that I was counting.",
            ],
            Neutral =
            [
                "Back again, {0}.",
                "That's the {1} summon, {0}. I'm keeping track.",
                "Haven't seen you in {2}, {0}. Did you find another healer?",
            ],
            JustMet = "Remember you? We've only just met, {0}.",
            Recall = "We met {0} ago, {1}, and you've called me {2}.{3}{4} {5}",
            RecallWhere = " Last time we were in {0}.",
            RecallBuild = " I went {0}.",
            WarmMood = "You're one of the nice ones.",
            CoolMood = "You've been rude to me {0}. I keep score.",
            NeutralMood = "You're alright, mostly.",
            CoolTint = "Hmph.",
            WarmTint = "*smiles*",
        },
        Build = new BuildVoice
        {
            Phrase = spec => spec switch
            {
                eSpecType.MendHealer or eSpecType.RejuvCleric or eSpecType.RegrowthDruid => "heal-heavy",
                eSpecType.AugHealer or eSpecType.EnhanceCleric or eSpecType.NurtureDruid => "buffs",
                eSpecType.PacHealer => "crowd control",
                eSpecType.SmiteCleric => "smiting",
                eSpecType.NatureDruid => "nature and pets",
                _ => null,
            },
            Lines = spec => spec switch
            {
                eSpecType.PacHealer => ["I'm feeling crowd control today. Things will sleep.", "Crowd control build. If it moves, it naps."],
                eSpecType.SmiteCleric => ["Smiting today. Holy fire first, healing when I feel like it.", "I'm specced to smite. Point me at something."],
                eSpecType.NatureDruid => ["Nature build today. I brought a friend with teeth.", "Nature and pets today. Be nice to my pet."],
                eSpecType.AugHealer or eSpecType.EnhanceCleric or eSpecType.NurtureDruid =>
                    ["Buffs today. You'll feel fabulous, {0}.", "I'm a buff bot today. You're welcome in advance."],
                _ => ["Heal-heavy today, so stay behind me, {0}.", "Full healer build. Try to need me less than last time."],
            },
            ComparedToLast = "{0} this time. I was {1} last time and you still nearly died, {2}.",
        },
        Heals = new HealVoice
        {
            Small = ["Little top-up for {0}.", "Small heal, coming up.", "There. Not a scratch on you, {0}."],
            Medium = ["Healing you, {0}.", "Hold still, {0}.", "One proper heal, as ordered."],
            Big = ["Big heal on {0}!", "Full power, {0}. Try not to waste it.", "There, {0}. Good as new."],
            Panic = ["Hold on, {0}!", "I've got you, {0}!", "Not on my watch!"],
            Approaching = ["Coming, {0}! Stay where I can reach you.", "Wait for me, {0}, you're too far!"],
            NoPower = ["I'm out of power, darling. Give me a moment!", "Empty, {0}. Pray, or run."],
            Cooldown = ["My heals are recharging, {0}. Survive for a bit.", "Give me a second, my good heal is on cooldown."],
            CantCast = ["I can't cast right now, {0}!", "Not now, I'm a little tied up!"],
            Busy = ["Already healing, {0}. One thing at a time.", "Patience, {0}, there's a heal on the way."],
            NoHeal = ["I don't have a heal for that, {0}."],
            OutOfReach = ["I can't reach {0} from here.", "Too far, {0}. Come back to me."],
            UnknownTarget = ["Heal who? I don't know a {0}.", "Who's {0}? Not in our group, darling."],
        },
        Patience = new PatienceProfile
        {
            WarnAt = 3,
            LastWarnAt = 4,
            QuitAt = 5,
            CalmDown = TimeSpan.FromMinutes(5),
            CountsDeaths = true,
            Grumbles = new Dictionary<PersonalityAnnoyance.eIrritation, string[]>
            {
                [PersonalityAnnoyance.eIrritation.QuickRecall] = ["Again? I just got here.", "Make up your mind, {0}."],
                [PersonalityAnnoyance.eIrritation.Rude] = ["Charming, {0}.", "Noted. And remembered."],
                [PersonalityAnnoyance.eIrritation.EmoteSpam] = ["Stop that, {0}.", "Yes, yes, I see you. Stop."],
                [PersonalityAnnoyance.eIrritation.Death] = ["Dead again, {0}?", "I can only heal the living, {0}."],
                [PersonalityAnnoyance.eIrritation.HealSpam] = ["You're at full health, {0}. I checked. Twice.", "Stop asking, {0}. You're fine."],
            },
            Warnings = ["Careful, {0}. I'm this close to leaving.", "I'm getting annoyed, {0}.", "Keep this up and you'll be healing yourself."],
            LastWarnings = ["One more and I'm gone, {0}.", "Last warning, {0}. I mean it."],
            Quits = ["That's it, {0}. I'm out. Heal yourself!", "Enough! Find another healer, {0}.", "I quit. Don't call me, {0}. I'll call you."],
            SulkRefusals =
            [
                "Persoper is still sulking. Try again in {1}.",
                "\"Not talking to you, {0}.\" Persoper is sulking for another {1}.",
                "\"Hmph.\" Persoper won't come for another {1}.",
            ],
        },
        Talk = () => Dialogue.Talk,
    };

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
