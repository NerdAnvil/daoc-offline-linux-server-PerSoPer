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
        Key = "kobaldjovi",
        Matches = Is,
        Memory = new MemoryVoice
        {
            Warm =
            [
                "My favourite listener is back! Hi, {0}.",
                "That's the {1} time we've teamed up, {0}. That's a trilogy at least.",
                "{2} without you, {0}. I wrote you a song. It's sad.",
            ],
            Cool =
            [
                "Oh, {0}. The heckler returns.",
                "Last time you said \"{3}\". It's in a song now, {0}. Not a nice one.",
                "{2} of peace and quiet, {0}. It was lovely.",
            ],
            Neutral =
            [
                "Hey, {0}! Back for the encore?",
                "That's the {1} time we've grouped, {0}.",
                "Haven't seen you in {2}, {0}. Good to have you in the audience.",
            ],
            JustMet = "Remember you? We just met, {0}. But I never forget a face. Or a chorus.",
            Recall = "First jam with you was {0} ago, {1}. We've grouped {2}.{3}{4} {5}",
            RecallWhere = " Last gig was in {0}.",
            RecallBuild = " I was playing {0}.",
            WarmMood = "You're one of my favourite fans.",
            CoolMood = "You've heckled me {0}. I keep a list.",
            NeutralMood = "You're a decent audience.",
            CoolTint = "*sour chord*",
            WarmTint = "*strums happily*",
        },
        Build = new BuildVoice
        {
            Phrase = spec => spec switch
            {
                eSpecType.TwoHanded => "two-handed",
                eSpecType.OneHandAndShield => "sword and board",
                _ => null,
            },
            Lines = spec => spec == eSpecType.TwoHanded
                ? ["Two-hander today. Louder that way.", "Big weapon, big chorus, {0}."]
                : ["Sword and board today, like a proper ballad.", "Shield up, {0}. It doubles as a drum."],
            ComparedToLast = "{0} today. Last time it was {1}; the crowd preferred that, {2}.",
        },
        Patience = new PatienceProfile
        {
            // Chill: takes more, calms down faster, and doesn't mind your deaths.
            WarnAt = 5,
            LastWarnAt = 6,
            QuitAt = 7,
            CalmDown = TimeSpan.FromMinutes(3),
            CountsDeaths = false,
            Grumbles = new Dictionary<PersonalityAnnoyance.eIrritation, string[]>
            {
                [PersonalityAnnoyance.eIrritation.QuickRecall] = ["Make up your mind, {0}. In or out?", "Again? I was mid-verse."],
                [PersonalityAnnoyance.eIrritation.Rude] = ["Everyone's a critic, {0}.", "Boo yourself, {0}."],
                [PersonalityAnnoyance.eIrritation.EmoteSpam] = ["Easy with the dancing, {0}.", "I said dance, not that."],
                [PersonalityAnnoyance.eIrritation.Death] = ["Rest in peace, {0}. I'll write you a dirge."],
            },
            Warnings = ["Careful, {0}. I feel a breakup song coming on.", "You're killing the vibe, {0}."],
            LastWarnings = ["One more, {0}, and I'm off to write that breakup song.", "Last verse, {0}. Then I walk."],
            Quits = ["That's it, {0}! I'm off to write a breakup song about you.", "Band's breaking up, {0}. Solo career starts now."],
            SulkRefusals =
            [
                "Kobaldjovi is still writing his breakup song about you. Try again in {1}.",
                "\"Not now, {0}, I'm working on the bridge.\" Try again in {1}.",
            ],
            MakeUps =
            [
                "Finished the breakup song, {0}. It's actually good. On my way back.",
                "Okay, I'm over it, {0}. Coming back. Don't make it weird.",
                "The band's getting back together, {0}! Be there soon.",
            ],
            MakeUpInvites =
            [
                "Finished the breakup song, {0}. Invite me if you want the encore.",
                "I'm over it, {0}. Send an invite when you're ready.",
            ],
        },
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

    /// <summary>Chance that someone nearby reacts to one of his songs.</summary>
    public const double SongReactionChance = 0.65;

    /// <summary>Chance that he answers the reaction, once.</summary>
    public const double ThanksChance = 0.5;

    // How a plain bot reacts to a song; {0} is his name.
    private static readonly string[] PlainReactions =
    [
        "Nice tune, {0}.",
        "*claps politely*",
        "Is that a new one, {0}?",
        "Not bad. Bit loud.",
        "{0}, do you know any shorter songs?",
        "My ears are still ringing.",
        "Encore! Actually, no. Not right now.",
        "I have heard worse in Jordheim taverns.",
    ];

    // His answer to a reaction; {0} is whoever reacted.
    private static readonly string[] Thanks =
    [
        "Thank you, thank you, {0}! I'm here all week.",
        "{0} gets it. Finally, an audience.",
        "Tough crowd, {0}. Tough crowd.",
        "Next one's dedicated to you, {0}.",
        "I'll put you down for the fan club, {0}.",
    ];

    /// <summary>A reaction to his song: the reactor's own line if it has a personality, else a plain one.</summary>
    public static string SongReaction(BotPersonality reactor, Random random)
    {
        random ??= Random.Shared;
        string[] lines = reactor?.SongReactions is { Length: > 0 } own ? own : PlainReactions;
        return string.Format(lines[random.Next(lines.Length)], Name);
    }

    public static string ThanksFor(string reactorName, Random random)
    {
        random ??= Random.Shared;
        return string.Format(Thanks[random.Next(Thanks.Length)], string.IsNullOrWhiteSpace(reactorName) ? "friend" : reactorName);
    }

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
