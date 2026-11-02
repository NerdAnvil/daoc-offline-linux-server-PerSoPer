using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// What a bot's answer about itself can be filled with. Null or 0 means "not known": a line that needs it
/// is skipped. Leader is the name of whoever leads its group (or owns it, for a companion).
/// </summary>
public sealed record StateSlots(string Zone, int Level, string ClassName, string Monster, string Destination, int GroupSize, string Leader);

/// <summary>
/// Personality bots answering questions about themselves from their real state (intent Part B4):
/// "where are you?" (Where), "what level are you?" (Level), "are you in a group?" (GroupStatus) and "what
/// are you doing?" (Activity). The words come from the bot's "~ reply where" (and so on) lines, filled
/// with {zone}, {level}, {class}, {monster}, {destination}, {groupsize} and {leader}; the conditions
/// [if bot_doing fighting] and [if bot_grouped] pick the line that fits. A line with a slot or a condition
/// is said only when it can be filled and holds; a line with neither is the shrug, said only when no other
/// line fits (the bot doesn't know). Nothing is made up: a slot that isn't known leaves its line out.
/// Plain bots answer these from their task instead (AutonomousBotChatCoordinator.BuildLocalReply).
/// </summary>
public static class PersonalityStateAnswers
{
    /// <summary>The slots an answer line may use, besides {0} (whoever asked).</summary>
    public static readonly IReadOnlyList<string> Slots = ["{zone}", "{level}", "{class}", "{monster}", "{destination}", "{groupsize}", "{leader}"];

    /// <summary>The bot_doing values, from busiest to idlest: the first that is true is what it is doing.</summary>
    public static readonly IReadOnlyList<string> DoingValues = ["fighting", "riding", "following", "travelling", "resting"];

    /// <summary>The intents answered from the bot's state.</summary>
    public static bool IsAnswered(eAutonomousChatIntent intent) =>
        AutonomousChatIntentModel.IsStateQuestion(intent) || intent == eAutonomousChatIntent.Activity;

    /// <summary>
    /// What the bot is doing, for [if bot_doing ...]: fighting, then riding (a horse or a stable route), then
    /// following (in someone else's group, or a companion), then travelling (moving on its own), else resting.
    /// </summary>
    public static string Doing(bool fighting, bool riding, bool following, bool moving) =>
        fighting ? "fighting" : riding ? "riding" : following ? "following" : moving ? "travelling" : "resting";

    /// <summary>What a bot in the game is doing (see <see cref="Doing"/>).</summary>
    public static string DoingOf(GameBot bot) =>
        bot == null ? null : Doing(bot.InCombat, bot.IsOnHorse || bot.IsOnStableMasterRoute, LeaderOf(bot) != null, bot.IsMoving);

    /// <summary>Who the bot follows: its group's leader when that isn't the bot, else a companion's owner; null when it leads or is alone.</summary>
    public static GameLiving LeaderOf(GameBot bot)
    {
        GameLiving leader = bot?.Group?.LivingLeader;
        if (leader != null)
            return leader == bot ? null : leader;
        return bot is { IsTemporaryGroupHelper: true } ? bot.Owner : null;
    }

    /// <summary>
    /// The slots from a bot in the game. The monster is said only while it fights, and the destination only
    /// while it travels or rides, so an old task is never quoted as what it does now.
    /// </summary>
    public static StateSlots SlotsFor(GameBot bot)
    {
        if (bot == null)
            return null;
        string doing = DoingOf(bot);
        LineSlots seen = PersonalityReactions.SlotsFor(bot, bot.Level);
        int groupSize = bot.Group?.MemberCount ?? 0;
        return new StateSlots(
            seen.Zone,
            bot.Level,
            seen.ClassName,
            doing == "fighting" ? seen.Monster : null,
            doing is "travelling" or "riding" ? bot.PersistentRecord?.TravelDestination : null,
            groupSize > 1 ? groupSize : 0,
            LeaderOf(bot)?.Name);
    }

    /// <summary>
    /// Fills the slots and {0}; null when the line needs a slot that isn't known. {leader} is "you" when the
    /// one who asked leads ("Right behind you"), so a line never starts with it.
    /// </summary>
    public static string Fill(string line, StateSlots slots, string speakerName)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        slots ??= new StateSlots(null, 0, null, null, null, 0, null);
        string speaker = string.IsNullOrWhiteSpace(speakerName) ? "friend" : speakerName;
        string leader = string.IsNullOrWhiteSpace(slots.Leader) ? null
            : string.Equals(slots.Leader, speakerName, StringComparison.OrdinalIgnoreCase) ? "you" : slots.Leader;
        foreach ((string token, string value) in new[]
        {
            ("{zone}", slots.Zone), ("{level}", slots.Level > 0 ? slots.Level.ToString() : null),
            ("{class}", slots.ClassName), ("{monster}", slots.Monster), ("{destination}", slots.Destination),
            ("{groupsize}", slots.GroupSize > 1 ? slots.GroupSize.ToString() : null), ("{leader}", leader),
        })
        {
            if (!line.Contains(token, StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                return null;
            line = line.Replace(token, value.Trim(), StringComparison.Ordinal);
        }
        return line.Replace("{0}", speaker, StringComparison.Ordinal);
    }

    /// <summary>True when a line uses one of the state slots.</summary>
    public static bool HasSlot(string line) => line != null && Slots.Any(slot => line.Contains(slot, StringComparison.Ordinal));

    /// <summary>
    /// The bot's answer to a question about itself, or null when the intent isn't one, the bot has no lines for
    /// it, or none may be said now (then it answers as usual). Facts carry bot_doing and bot_grouped; without
    /// them (tests) lines with conditions are left out.
    /// </summary>
    public static string Answer(BotPersonality personality, eAutonomousChatIntent intent, StateSlots slots, DialogueFacts facts,
        string speakerName, Random random, Func<string, DialogueRule> ruleOf = null)
    {
        if (personality == null || !IsAnswered(intent) || personality.Replies.GetValueOrDefault(intent) is not { Length: > 0 } written)
            return null;
        ruleOf ??= DialogueLibrary.RuleFor;
        random ??= Random.Shared;
        string[] lines = BotPersonality.NotNaming(DialogueBotNames.Fill(written, facts?.Realm), speakerName);
        string[] stateful = lines.Where(line => (HasSlot(line) || ruleOf(line)?.Conditions.Count > 0) && Fill(line, slots, speakerName) != null).ToArray();
        string[] shrugs = lines.Where(line => !HasSlot(line) && !(ruleOf(line)?.Conditions.Count > 0)).ToArray();
        string botKey = facts?.BotId ?? personality.Key;
        DateTime now = DateTime.UtcNow;
        string picked = DialoguePicker.Shared.Pick(stateful, ruleOf, facts, botKey, now, random)
            ?? DialoguePicker.Shared.Pick(shrugs, ruleOf, facts, botKey, now, random);
        return picked == null ? null : Fill(picked, slots, speakerName);
    }
}
