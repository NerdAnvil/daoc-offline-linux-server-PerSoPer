using System;
using System.Collections.Generic;

namespace DOL.GS;

/// <summary>
/// Kobaldjovi, the Singer: the second PerSoPer personality. Unlike Persoper he
/// is not summoned; he lives in the world as an ordinary autonomous gamebot
/// (a Midgard Kobold Skald) and mostly sings when it is his turn to chat.
/// Pure logic only, so it can be unit tested without a running world.
/// </summary>
public static class KobaldjoviIdentity
{
    public const string Name = "Kobaldjovi";
    public const eRealm Realm = eRealm.Midgard;
    public const eGender Gender = eGender.Male;
    public const eCharacterClass CharacterClass = eCharacterClass.Skald;
    public const eRace Race = eRace.Kobold;

    /// <summary>Chance that his ambient chat line is a song rather than ordinary talk.</summary>
    public const double SingChance = 0.8;

    // Original lines only: no real lyrics. He names artists and genres, never quotes them.
    private static readonly string[] Songs =
    [
        "~ Oh Jordheim, my frozen love, your snow falls soft as power ballads from above ~",
        "~ I'm a Kobold on a long road, wanted dead or rezzed ~ ...that one needs work.",
        "~ Hair this big takes courage, and courage takes hairspray ~",
        "~ Spin the lute, drop the beat, the troll in the back has got two left feet ~",
        "~ RAAAARGH, THE FROST GIANT HUNGERS ~ ...sorry, death metal phase. It comes and goes.",
        "~ Whoop whoop, the juggalo of Jotunheim ~ ...the faygo is mead, it counts.",
        "~ Hmmmmmmmmm-ooooo ~ That was throat singing. You could feel it in your boots, admit it.",
        "~ Glitter on my beard and sequins on my axe ~ Madonna would approve.",
        "~ My horse left me at the stable, my mead is warm, my boots are sad ~ Country is hard.",
        "~ Om... the wind... the pine... the gentle respawn timer ~ New age. Very calming.",
        "~ Da da da DUM ~ Beethoven, but on a lute. Close enough.",
        "~ Two hearts, one group, zero mana potions ~ A romance. Tragic, really.",
        "~ Chorus! Everybody now! ~ ...anybody? Fine, I'll do the harmony too.",
        "~ They say a Skald can't rap, but a Skald just did ~ Mic drop. Lute drop. Careful, that's my lute.",
        "~ Comic books and candlelight, I'll read until the end of night ~",
        "~ Pages turning, dragons burning, every chapter, I keep learning ~",
    ];

    private static readonly string[] Asides =
    [
        "Anyone up for a sing-along? No? I'll start, you join in.",
        "Picked up a new comic in Jordheim. The hero is a Kobold. Obviously.",
        "Reading tip: never lend a book to a Troll. Ask me how I know.",
        "Best decade for music? Trick question. It's the one with the big hair.",
        "If you hear singing in the hills, that's me. If it's bad singing, also me.",
        "I take requests. I ignore most of them, but I take them.",
    ];

    // {0} is whoever spoke to him.
    public static readonly BotPersonality Personality = new()
    {
        Name = Name,
        Matches = Is,
        Replies = new Dictionary<eAutonomousChatIntent, string[]>
        {
            [eAutonomousChatIntent.Greeting] =
            [
                "{0}! ~ Hello, hello, it's good to see you ~ ...sorry, it just comes out.",
                "Hail, {0}! Got a request? I do ballads, metal and very loud whale noises.",
                "Well met, {0}. You look like someone who appreciates a power ballad.",
            ],
            [eAutonomousChatIntent.Banter] =
            [
                "Ha! {0}, that's going in a song. You won't like the rhyme.",
                "Funny, {0}. Not as funny as my comedy album, but close.",
                "{0}, you're wasted on adventuring. Ever thought of joining a band?",
            ],
            [eAutonomousChatIntent.Abuse] =
            [
                "Harsh, {0}. I've had worse reviews. Mostly from my mother.",
                "Easy there, {0}. Save that energy for a death metal chorus.",
                "~ Sticks and stones may break my bones, but haters make great B-sides ~",
            ],
            [eAutonomousChatIntent.Grouping] =
            [
                "A group, {0}? I bring speed songs and a deep catalogue. Mostly the songs.",
                "I'll join, {0}, but I get to pick the travel music.",
                "Every band needs a Skald, {0}. Every group too.",
            ],
            [eAutonomousChatIntent.Grinding] =
            [
                "Grinding is just a song with a lot of verses, {0}.",
                "I hum while I hunt, {0}. The monsters hate it. Keeps them off balance.",
            ],
            [eAutonomousChatIntent.Travel] =
            [
                "Road trip, {0}? I've got a whole mixtape for the walk.",
                "Wherever it is, {0}, it's closer with a speed song. Allegedly.",
            ],
            [eAutonomousChatIntent.Dungeon] =
            [
                "Dungeons have great echo, {0}. Terrible audiences though.",
                "Dark, damp, full of screaming? Sounds like my first gig, {0}.",
            ],
            [eAutonomousChatIntent.RvR] =
            [
                "The frontier, {0}! Nothing says romance like a keep siege at sunset.",
                "I'll sing the battle hymn, {0}. You do the actual battling.",
            ],
            [eAutonomousChatIntent.Trading] =
            [
                "I'd trade, {0}, but all I own is a lute and a stack of comics. Not selling the comics.",
                "Any old books for sale, {0}? I'm halfway through a saga and it's getting good.",
            ],
            [eAutonomousChatIntent.Help] =
            [
                "Help? Sure, {0}. Step one: hum something. Step two: improvise.",
                "I'm no trainer, {0}, but I know a song about it.",
            ],
            [eAutonomousChatIntent.Farewell] =
            [
                "~ Fare thee well, {0}, the road is long, and I will write you a song ~",
                "Bye, {0}! Come back for the encore.",
            ],
            [eAutonomousChatIntent.IgnoreOutOfWorld] =
            [
                "No idea what that means, {0}. Is it a new music style?",
                "Sounds like something from the far side of the world, {0}. Can I sing it?",
            ],
        },
        FallbackReplies =
        [
            "Hm, {0}, that's got a rhythm to it. Mind if I use it?",
            "Interesting, {0}. Reminds me of a comic I read once.",
            "{0}, I'll be honest, I was humming. Say that again?",
        ],
    };

    /// <summary>True when a character or bot name would impersonate Kobaldjovi.</summary>
    public static bool IsReservedName(string name) =>
        !string.IsNullOrEmpty(name) && name.Contains(Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for the one roster bot that is Kobaldjovi.</summary>
    public static bool Is(GameBot bot) =>
        bot != null && string.Equals(bot.Name, Name, StringComparison.OrdinalIgnoreCase);

    public static AutonomousBotIdentityGenerator.Identity CreateIdentity() =>
        new(Name, Realm, Gender, CharacterClass, Race);

    /// <summary>Picks his ambient line: usually a song, sometimes an aside.</summary>
    public static string AmbientLine(Random random, out bool isSong)
    {
        random ??= Random.Shared;
        isSong = random.NextDouble() < SingChance;
        string[] lines = isSong ? Songs : Asides;
        return lines[random.Next(lines.Length)];
    }
}
