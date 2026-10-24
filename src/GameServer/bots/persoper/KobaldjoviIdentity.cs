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

    // Songs and asides: dialogue/kobaldjovi.dlg. Original lines only, no real lyrics; he names artists and genres, never quotes them.
    private static readonly IdentityDialogue Dialogue = new("kobaldjovi");

    private static string[] Songs => Dialogue.Lines("song");

    private static string[] Asides => Dialogue.Lines("aside");

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
        Talk = () => Dialogue.Talk,
    };

    /// <summary>Chance that someone nearby reacts to one of his songs.</summary>
    public const double SongReactionChance = 0.65;

    /// <summary>Chance that he answers the reaction, once.</summary>
    public const double ThanksChance = 0.5;

    // How a plain bot reacts to a song ({0} is his name), and his answer to a reaction ({0} is whoever reacted).
    private static string[] PlainReactions => Dialogue.Lines("plain_reaction");

    private static string[] Thanks => Dialogue.Lines("thanks");

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
