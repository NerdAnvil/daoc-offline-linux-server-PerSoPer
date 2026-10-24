using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// The shape every PerSoPer personality shares: who it is and how it answers
/// chat, sorted by the chat intent the existing classifier finds. Plain bots
/// have no personality and keep the generic replies.
/// Reply lines use {0} for the name of whoever spoke to it.
/// </summary>
public sealed record BotPersonality
{
    private readonly IReadOnlyList<string> _nicknames = [];
    private readonly IReadOnlyDictionary<eAutonomousChatIntent, string[]> _replies = new Dictionary<eAutonomousChatIntent, string[]>();
    private readonly string[] _fallbackReplies = [];
    private readonly string[] _songReactions;
    private readonly IReadOnlyDictionary<eAutonomousChatIntent, string[]> _cheekyReplies;
    private readonly string[] _ambient;

    public required string Name { get; init; }

    /// <summary>
    /// Where its chat lines come from (its dialogue file). Asked on every use, so a dialogue reload
    /// takes effect at once. When set, it gives Nicknames, Replies, FallbackReplies, SongReactions,
    /// CheekyReplies and Ambient; when null, those are the values set directly.
    /// </summary>
    public Func<TypeTalk> Talk { get; init; }

    /// <summary>Other names players may call it by ("Jovi", "KJ"), from its dialogue file.</summary>
    public IReadOnlyList<string> Nicknames { get => Talk != null ? Talk().Nicknames : _nicknames; init => _nicknames = value; }
    /// <summary>Stable lowercase id used in saved memories ("persoper", "kobaldjovi").</summary>
    public required string Key { get; init; }
    public required Func<GameBot, bool> Matches { get; init; }
    public IReadOnlyDictionary<eAutonomousChatIntent, string[]> Replies { get => Talk != null ? Talk().Replies : _replies; init => _replies = value; }
    public string[] FallbackReplies { get => Talk != null ? Talk().FallbackReplies : _fallbackReplies; init => _fallbackReplies = value; }

    /// <summary>How it reacts when Kobaldjovi sings nearby; {0} is his name. Null uses the plain-bot lines.</summary>
    public string[] SongReactions { get => Talk != null ? Talk().SongReactions : _songReactions; init => _songReactions = value; }

    /// <summary>Extra lines mixed in only when tone-down is off.</summary>
    public IReadOnlyDictionary<eAutonomousChatIntent, string[]> CheekyReplies { get => Talk != null ? Talk().CheekyReplies : _cheekyReplies; init => _cheekyReplies = value; }

    /// <summary>How it talks about players it remembers. Null: it remembers no one.</summary>
    public MemoryVoice Memory { get; init; }

    /// <summary>How it describes its build. Null: it never mentions one.</summary>
    public BuildVoice Build { get; init; }

    /// <summary>What it says when asked for a heal. Null: the neutral healer lines.</summary>
    public HealVoice Heals { get; init; }

    /// <summary>How much irritation it takes and how it reacts. Null: nothing annoys it.</summary>
    public PatienceProfile Patience { get; init; }

    /// <summary>Lines it starts conversations with (personality types). Null: none of its own.</summary>
    public string[] Ambient { get => Talk != null ? Talk().Ambient : _ambient; init => _ambient = value; }

    /// <summary>How often it speaks up, 0-100 (personality types; Loner low, Joker high).</summary>
    public int Chattiness { get; init; }

    /// <summary>The personality type this is a copy of ("joker"), or null for a unique personality.</summary>
    public string TypeKey { get; init; }

    /// <summary>
    /// A reply by the dialogue rules (DialoguePicker). Without <paramref name="facts"/> (bot to bot, tests)
    /// lines with conditions are left out; [once], cooldowns and recent lines still count.
    /// </summary>
    public string Reply(eAutonomousChatIntent intent, string speakerName, bool toneDown, Random random, DialogueFacts facts = null)
    {
        random ??= Random.Shared;
        string[] lines = Replies.GetValueOrDefault(intent) ?? FallbackReplies;
        if (!toneDown && CheekyReplies?.GetValueOrDefault(intent) is { Length: > 0 } cheeky && random.Next(2) == 0)
            lines = cheeky;
        string botKey = facts?.BotId ?? Key;
        DateTime now = DateTime.UtcNow;
        string line = DialoguePicker.Shared.Pick(lines, DialogueLibrary.RuleFor, facts, botKey, now, random)
            ?? DialoguePicker.Shared.Pick(FallbackReplies, DialogueLibrary.RuleFor, facts, botKey, now, random)
            ?? FallbackReplies.FirstOrDefault(fallback => DialogueLibrary.RuleFor(fallback) == null)
            ?? lines[random.Next(lines.Length)];
        return string.Format(line, string.IsNullOrWhiteSpace(speakerName) ? "friend" : speakerName);
    }
}

/// <summary>
/// Remembered lines by mood. Templates: {0} player name, {1} how many times as a word
/// ("third"), {2} time away ("3 days"), {3} the last rude thing they said. A template
/// that needs {2} or {3} is skipped when there is nothing to fill it with.
/// </summary>
public sealed class MemoryVoice
{
    public required string[] Warm { get; init; }
    public required string[] Cool { get; init; }
    public required string[] Neutral { get; init; }
    /// <summary>"Do you remember me?" from someone it has not met; {0} player.</summary>
    public required string JustMet { get; init; }
    /// <summary>{0} how long ago they met, {1} player, {2} "4 times", {3} where, {4} build, {5} mood.</summary>
    public required string Recall { get; init; }
    /// <summary>Recall's place sentence; {0} zone.</summary>
    public required string RecallWhere { get; init; }
    /// <summary>Recall's build sentence; {0} build phrase.</summary>
    public required string RecallBuild { get; init; }
    public required string WarmMood { get; init; }
    /// <summary>{0} how many rude messages ("3 times").</summary>
    public required string CoolMood { get; init; }
    public required string NeutralMood { get; init; }
    /// <summary>Sometimes put before a reply to a player it dislikes.</summary>
    public required string CoolTint { get; init; }
    /// <summary>Sometimes put after a reply to a player it likes.</summary>
    public required string WarmTint { get; init; }
}

/// <summary>How a personality talks about its build (spec).</summary>
public sealed class BuildVoice
{
    /// <summary>The build in a few plain words, or null for builds it does not describe.</summary>
    public required Func<eSpecType, string> Phrase { get; init; }
    /// <summary>What it says about a build; {0} player.</summary>
    public required Func<eSpecType, string[]> Lines { get; init; }
    /// <summary>Said instead, sometimes, when the build changed: {0} this build, {1} last build, {2} player.</summary>
    public required string ComparedToLast { get; init; }
}

/// <summary>A personality's patience: when it warns, when it quits, what it says.</summary>
public sealed class PatienceProfile
{
    public required int WarnAt { get; init; }
    public required int LastWarnAt { get; init; }
    public required int QuitAt { get; init; }
    public required TimeSpan CalmDown { get; init; }
    /// <summary>Whether the player dying while grouped with it counts as an irritation.</summary>
    public required bool CountsDeaths { get; init; }
    /// <summary>Mild lines per irritation; {0} player.</summary>
    public required IReadOnlyDictionary<PersonalityAnnoyance.eIrritation, string[]> Grumbles { get; init; }
    public required string[] Warnings { get; init; }
    public required string[] LastWarnings { get; init; }
    public required string[] Quits { get; init; }
    /// <summary>Shown when called while it sulks; {0} player, {1} time left ("7 minutes").</summary>
    public required string[] SulkRefusals { get; init; }
    /// <summary>World bots only: the tell it sends when its sulk ends and it heads back; {0} player.</summary>
    public string[] MakeUps { get; init; }
    /// <summary>World bots only: the tell when it can't rejoin by itself (not leader, group full); {0} player.</summary>
    public string[] MakeUpInvites { get; init; }
}

/// <summary>All known personalities; a bot has at most one.</summary>
public static class BotPersonalities
{
    public static readonly IReadOnlyList<BotPersonality> All = [PersoperIdentity.Personality, KobaldjoviIdentity.Personality];

    public static BotPersonality For(GameBot bot)
    {
        if (bot == null)
            return null;
        foreach (BotPersonality personality in All)
        {
            if (personality.Matches(bot))
                return personality;
        }
        // A generated world bot given a personality type (Joker, Loner...).
        return bot.IsAutonomousWorldBot ? PersonalityTypes.InstanceFor(bot) : null;
    }
}
