using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Persoper asking players questions from dialogue/persoper.dlg ("~ ask fav_music") and taking their
/// answers: the question goes to group chat or nearby, the player's next short line is matched
/// (AskTracker), the reply is said and the answer saved (PlayerAnswers) for [if player_likes ...].
/// </summary>
public static class PersoperAsks
{
    /// <summary>Gap between questions when she asks unprompted, in minutes.</summary>
    public const int MinGapMinutes = 6;
    public const int MaxGapMinutes = 12;

    private static DateTime _nextUnprompted = DateTime.MinValue;

    /// <summary>
    /// Persoper puts a question to a player: <paramref name="questionId"/>, or a random one they haven't
    /// answered. Null when asked; else why not, in words for a GM.
    /// </summary>
    public static string Ask(GameBot bot, GamePlayer player, string questionId, Random random, DateTime now)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        if (bot == null || player == null || personality != PersoperIdentity.Personality)
            return "Only Persoper asks questions for now.";
        IReadOnlyList<DialogueSection> asks = PersoperIdentity.Asks;
        DialogueSection ask;
        if (!string.IsNullOrEmpty(questionId))
        {
            ask = asks.FirstOrDefault(found => string.Equals(found.Argument, questionId, StringComparison.OrdinalIgnoreCase));
            if (ask == null)
                return asks.Count == 0 ? "persoper.dlg has no questions." : $"No question '{questionId}'. Known: {string.Join(", ", asks.Select(found => found.Argument))}.";
        }
        else
        {
            DialogueSection[] unanswered = asks.Where(found => PlayerAnswers.Cache.Get(player.InternalID, found.Argument) == null).ToArray();
            if (unanswered.Length == 0)
                return "Nothing left to ask this player.";
            ask = unanswered[(random ?? Random.Shared).Next(unanswered.Length)];
        }
        if (!AskTracker.Shared.Start(personality.Key, player.InternalID, ask, now))
            return "Persoper is already waiting for an answer from this player.";
        PersonalityMeetings.Speak(bot, Fill(ask.Lines[0], player));
        return null;
    }

    /// <summary>
    /// A player's chat line as the answer to Persoper's open question (when sent to a bot, only to her):
    /// says the matching reply and saves the answer. False when it isn't an answer.
    /// </summary>
    public static bool TryAnswer(GamePlayer player, string message, GameBot toBot = null)
    {
        DateTime now = DateTime.UtcNow;
        string botKey = PersoperIdentity.Personality.Key;
        if (player == null || string.IsNullOrWhiteSpace(message) || !AskTracker.Shared.IsOpen(player.InternalID, now, botKey))
            return false;
        if (toBot != null && BotPersonalities.For(toBot) != PersoperIdentity.Personality)
            return false;
        GameBot persoper = toBot ?? Commands.PersoperCommandHandler.Active;
        // "Persoper, hello" is talk to her, not an answer; "Persoper, metal" still is.
        bool allowOther = persoper == null || !AutonomousBotChatCoordinator.IsMentionedPublic(message, persoper);
        if (AskTracker.Shared.TryAnswer(player.InternalID, message, now, botKey, allowOther) is not { } outcome)
            return false;

        PlayerAnswers.Cache.Set(player.InternalID, outcome.Ask.Argument, outcome.Answer.Key);
        string reply = Fill(outcome.Answer.Reply, player);
        if (persoper != null)
            _ = new ECSGameTimer(persoper, timer =>
            {
                if (persoper.ObjectState == GameObject.eObjectState.Active)
                    PersonalityMeetings.Speak(persoper, reply);
                return 0;
            }, 1_200 + Random.Shared.Next(1_000));
        return true;
    }

    /// <summary>
    /// Now and then, when she is in a group with a player and has nothing else going on, she asks
    /// a question they haven't answered. Called from the chatter tick, only while chatter is on.
    /// </summary>
    public static void Tick(DateTime now)
    {
        if (now < _nextUnprompted)
            return;
        GameBot persoper = Commands.PersoperCommandHandler.Active;
        if (persoper is not { IsAlive: true, InCombat: false, IsCasting: false } ||
            persoper.Group?.GetMembersInTheGroup().OfType<GamePlayer>().FirstOrDefault() is not { IsAlive: true, InCombat: false } player)
            return;
        _nextUnprompted = now.AddMinutes(MinGapMinutes + Random.Shared.NextDouble() * (MaxGapMinutes - MinGapMinutes));
        Ask(persoper, player, null, Random.Shared, now);
    }

    private static string Fill(string line, GamePlayer player) => line.Replace("{0}", player.Name);
}
