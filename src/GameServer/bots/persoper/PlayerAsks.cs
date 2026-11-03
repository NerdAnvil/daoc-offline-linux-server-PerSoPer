using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>What a player's reply to a question came to: the matching answer row (maybe "other") of the ask.</summary>
public sealed record AskOutcome(DialogueSection Ask, DialogueAnswer Answer);

/// <summary>
/// The questions bots have put to players and are waiting on (docs/DIALOGUE-DESIGN.md, "A question
/// with branching replies"). A player has one open question at a time. It stays open for
/// <see cref="Timeout"/>, then drops without a word. The next short chat line from that player counts
/// as the answer: the first answer word in it picks the reply, and anything else gets "other". A long
/// line is ordinary chat and leaves the question open. Pure and thread safe; the clock is passed in.
/// </summary>
public sealed class AskTracker
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Words a line may have and still count as an answer, and open questions kept at most.</summary>
    public const int MaxAnswerWords = 6;
    public const int MaxOpen = 200;

    public static readonly AskTracker Shared = new();

    private sealed record Open(string BotKey, DialogueSection Ask, DateTime Expires);

    private readonly Dictionary<string, Open> _open = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>Opens a question for a player. False when they already have one open, or the store is full.</summary>
    public bool Start(string botKey, string playerId, DialogueSection ask, DateTime now)
    {
        if (string.IsNullOrEmpty(playerId) || ask?.Answers is not { Count: > 0 })
            return false;
        lock (_lock)
        {
            Prune(now);
            if (_open.ContainsKey(playerId) || _open.Count >= MaxOpen)
                return false;
            _open[playerId] = new Open(botKey ?? string.Empty, ask, now + Timeout);
            return true;
        }
    }

    /// <summary>True when the player has a question open (for this bot, when one is named).</summary>
    public bool IsOpen(string playerId, DateTime now, string botKey = null)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        lock (_lock)
            return _open.TryGetValue(playerId, out Open open) && now < open.Expires && (botKey == null || open.BotKey == botKey);
    }

    /// <summary>
    /// The player's line as an answer to their open question (to this bot's, when one is named). Null
    /// when they have none, it ran out, or the line is too long to be an answer. Closes the question
    /// when it matches.
    /// </summary>
    public AskOutcome TryAnswer(string playerId, string message, DateTime now, string botKey = null, bool allowOther = true)
    {
        if (string.IsNullOrEmpty(playerId))
            return null;
        lock (_lock)
        {
            if (!_open.TryGetValue(playerId, out Open open))
                return null;
            if (now >= open.Expires)
            {
                _open.Remove(playerId);
                return null;
            }
            if (botKey != null && open.BotKey != botKey)
                return null;
            DialogueAnswer answer = Match(open.Ask, message, allowOther);
            if (answer == null)
                return null;
            _open.Remove(playerId);
            return new AskOutcome(open.Ask, answer);
        }
    }

    /// <summary>
    /// The answer row a line picks: the first answer word in it, else "other" (unless <paramref name="allowOther"/>
    /// is false: a line that talks to the bot by name is chat unless it holds an answer word); null for an
    /// empty or long line.
    /// </summary>
    public static DialogueAnswer Match(DialogueSection ask, string message, bool allowOther = true)
    {
        if (ask?.Answers is not { Count: > 0 })
            return null;
        string[] words = AutonomousChatIntentModel.Tokens(message).ToArray();
        if (words.Length == 0 || words.Length > MaxAnswerWords)
            return null;
        foreach (string word in words)
            if (ask.Answers.FirstOrDefault(answer => answer.Key == word && answer.Key != DialogueFile.OtherAnswer) is { } found)
                return found;
        return allowOther ? ask.Answers.FirstOrDefault(answer => answer.Key == DialogueFile.OtherAnswer) : null;
    }

    /// <summary>Forgets every open question (tests).</summary>
    public void Clear()
    {
        lock (_lock)
            _open.Clear();
    }

    private void Prune(DateTime now)
    {
        foreach (string player in _open.Where(pair => now >= pair.Value.Expires).Select(pair => pair.Key).ToList())
            _open.Remove(player);
    }
}
