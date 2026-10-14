using System;
using System.Collections.Generic;

namespace DOL.GS;

/// <summary>
/// The shape every PerSoPer personality shares: who it is and how it answers
/// chat, sorted by the chat intent the existing classifier finds. Plain bots
/// have no personality and keep the generic replies.
/// Reply lines use {0} for the name of whoever spoke to it.
/// </summary>
public sealed class BotPersonality
{
    public required string Name { get; init; }
    public required Func<GameBot, bool> Matches { get; init; }
    public required IReadOnlyDictionary<eAutonomousChatIntent, string[]> Replies { get; init; }
    public required string[] FallbackReplies { get; init; }

    /// <summary>Extra lines mixed in only when tone-down is off.</summary>
    public IReadOnlyDictionary<eAutonomousChatIntent, string[]> CheekyReplies { get; init; }

    public string Reply(eAutonomousChatIntent intent, string speakerName, bool toneDown, Random random)
    {
        random ??= Random.Shared;
        string[] lines = Replies.GetValueOrDefault(intent) ?? FallbackReplies;
        if (!toneDown && CheekyReplies?.GetValueOrDefault(intent) is { Length: > 0 } cheeky && random.Next(2) == 0)
            lines = cheeky;
        return string.Format(lines[random.Next(lines.Length)], string.IsNullOrWhiteSpace(speakerName) ? "friend" : speakerName);
    }
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
        return null;
    }
}
