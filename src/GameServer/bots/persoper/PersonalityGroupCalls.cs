using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>A personality bot's answer to "who wants to group?": silence, yes, no, busy, already grouped, or the level gap.</summary>
public enum eGroupCallAnswer
{
    Quiet,
    Yes,
    No,
    Busy,
    Grouped,
    Level,
}

/// <summary>
/// What a bot's answer to a group call is decided from, all read from the game: the caller's group (same group as the
/// bot, or full), the bot's fight or ride, a player-led group it is already in, both levels, and a sulk at the caller.
/// </summary>
public sealed record GroupCallState(bool SameGroup, bool CallerGroupFull, bool Fighting, bool Riding, bool InPlayerGroup,
    int BotLevel, int CallerLevel, bool Sulking);

/// <summary>
/// Personality world bots answering an open group call in /say or faction chat (intent Part B5): "who wants to group?",
/// "anyone up for a dungeon?", "lfg". Each bot that hears it decides from its real state and its type how keen it is
/// (<see cref="Decide"/>), and at most <see cref="MaxAnswers"/> of them answer, in their own "~ reply groupyes" (groupno,
/// groupbusy, grouphave, grouplevel) lines. Saying yes is a promise only: the player still sends a real /invite, and a
/// bot that said yes doesn't then turn it down for being a loner (<see cref="Promised"/>). The bot never walks over on
/// its own. Companions (Persoper, /spawn helpers) and plain bots don't take part: they answer as before.
/// </summary>
public static class PersonalityGroupCalls
{
    /// <summary>At most this many bots answer one call.</summary>
    public const int MaxAnswers = 3;

    /// <summary>At most this many bots that heard it are weighed.</summary>
    public const int MaxWeighed = 8;

    /// <summary>After a call, the same player's next call within this time gets no answers (no flood from "lfg lfg lfg").</summary>
    public const long CooldownMs = 20_000;

    /// <summary>How long a "yes" holds: a real /invite within it is accepted without the loner's refusal roll.</summary>
    public const long PromiseMs = 120_000;

    private const int MaxRemembered = 256;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, long> LastCall = new(StringComparer.Ordinal);
    private static readonly Dictionary<(int Bot, string Player), long> Promises = new();

    /// <summary>Test switch (/dialogue groupcall always): every bot weighed answers, up to <see cref="MaxAnswers"/>.</summary>
    public static bool Always { get; set; }

    /// <summary>How keen a type is to group, from 0 (never) to 1 (always), before its state is weighed.</summary>
    public static double Keenness(string typeKey) => typeKey switch
    {
        "leeroy" => 0.9,
        "joker" => 0.7,
        "casual" or "herbaldoctor" or "roleplayer" => 0.65,
        "hardcore" => 0.55,
        "homeowner" or "gruffdoc" => 0.5,
        "minmaxer" => 0.45,
        "crafter" => 0.3,
        "loner" => 0.15,
        _ => 0.5,
    };

    /// <summary>How far apart two levels may be for a group to make sense: 4 at low levels, a fifth of the caller's level above 20.</summary>
    public static int LevelRange(int callerLevel) => Math.Max(4, callerLevel / 5);

    /// <summary>
    /// The bot's answer. Quiet when it is already in the caller's group or the caller's group is full; busy while it
    /// fights or rides; grouped when it follows another player; the level gap when the levels are too far apart (only
    /// when both are known); no when it sulks at the caller; else yes or no by its type's keenness. Pure.
    /// </summary>
    public static eGroupCallAnswer Decide(GroupCallState state, string typeKey, Random random)
    {
        if (state == null || state.SameGroup || state.CallerGroupFull)
            return eGroupCallAnswer.Quiet;
        if (state.Fighting || state.Riding)
            return eGroupCallAnswer.Busy;
        if (state.InPlayerGroup)
            return eGroupCallAnswer.Grouped;
        if (state.BotLevel > 0 && state.CallerLevel > 0 && Math.Abs(state.BotLevel - state.CallerLevel) > LevelRange(state.CallerLevel))
            return eGroupCallAnswer.Level;
        if (state.Sulking)
            return eGroupCallAnswer.No;
        return (random ?? Random.Shared).NextDouble() < Keenness(typeKey) ? eGroupCallAnswer.Yes : eGroupCallAnswer.No;
    }

    /// <summary>The .dlg section an answer is said from; Unknown for Quiet.</summary>
    public static eAutonomousChatIntent KindFor(eGroupCallAnswer answer) => answer switch
    {
        eGroupCallAnswer.Yes => eAutonomousChatIntent.GroupYes,
        eGroupCallAnswer.No => eAutonomousChatIntent.GroupNo,
        eGroupCallAnswer.Busy => eAutonomousChatIntent.GroupBusy,
        eGroupCallAnswer.Grouped => eAutonomousChatIntent.GroupHave,
        eGroupCallAnswer.Level => eAutonomousChatIntent.GroupLevel,
        _ => eAutonomousChatIntent.Unknown,
    };

    /// <summary>How many of the bots that would answer do: 0 (one call in ten goes unanswered) to <see cref="MaxAnswers"/>.</summary>
    public static int AnswerCount(int willing, Random random)
    {
        if (willing <= 0)
            return 0;
        if (Always)
            return Math.Min(willing, MaxAnswers);
        double roll = (random ?? Random.Shared).NextDouble();
        int count = roll < 0.1 ? 0 : roll < 0.5 ? 1 : roll < 0.8 ? 2 : 3;
        return Math.Min(willing, count);
    }

    /// <summary>
    /// The bot's line for its answer, filled from its state; a reason it has no line for falls back to its "no", and
    /// a "yes" to its old "~ reply grouping". Null when it has nothing to say (then it stays quiet).
    /// </summary>
    public static string Line(BotPersonality personality, eGroupCallAnswer answer, StateSlots slots, DialogueFacts facts,
        string speakerName, Random random, Func<string, DialogueRule> ruleOf = null)
    {
        if (personality == null || answer == eGroupCallAnswer.Quiet)
            return null;
        string said = PersonalityStateAnswers.Pick(personality, KindFor(answer), slots, facts, speakerName, random, ruleOf);
        if (said != null)
            return said;
        return answer == eGroupCallAnswer.Yes
            ? PersonalityStateAnswers.Pick(personality, eAutonomousChatIntent.Grouping, slots, facts, speakerName, random, ruleOf)
            : answer == eGroupCallAnswer.No ? null
            : PersonalityStateAnswers.Pick(personality, eAutonomousChatIntent.GroupNo, slots, facts, speakerName, random, ruleOf);
    }

    /// <summary>
    /// True when this player's call may be answered now (and starts its cooldown); false within
    /// <see cref="CooldownMs"/> of the last one.
    /// </summary>
    public static bool TryStart(string playerId, long nowMs)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        lock (Sync)
        {
            if (LastCall.TryGetValue(playerId, out long last) && nowMs - last >= 0 && nowMs - last < CooldownMs)
                return false;
            if (LastCall.Count >= MaxRemembered)
                LastCall.Clear();
            LastCall[playerId] = nowMs;
            return true;
        }
    }

    /// <summary>Notes that a bot said yes to a player's call.</summary>
    public static void Promise(int botId, string playerId, long nowMs)
    {
        if (string.IsNullOrEmpty(playerId))
            return;
        lock (Sync)
        {
            if (Promises.Count >= MaxRemembered)
                Promises.Clear();
            Promises[(botId, playerId)] = nowMs;
        }
    }

    /// <summary>True when the bot said yes to this player's call within <see cref="PromiseMs"/>.</summary>
    public static bool Promised(int botId, string playerId, long nowMs)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        lock (Sync)
            return Promises.TryGetValue((botId, playerId), out long said) && nowMs - said >= 0 && nowMs - said < PromiseMs;
    }

    /// <summary>The longest a test hold (/dialogue hold) lasts.</summary>
    public const long MaxHoldMs = 120_000;

    private static readonly Dictionary<int, long> HeldUntil = new();

    /// <summary>
    /// Test hold (/dialogue hold): the bot's own plans pause for up to <see cref="MaxHoldMs"/>, so a check can leave
    /// its group and still find it there (tools/check-groupcalls-ingame.sh).
    /// </summary>
    public static void Hold(int botId, long nowMs, long forMs)
    {
        lock (Sync)
        {
            if (HeldUntil.Count >= MaxRemembered)
                HeldUntil.Clear();
            HeldUntil[botId] = nowMs + Math.Clamp(forMs, 0, MaxHoldMs);
        }
    }

    /// <summary>True while a test hold keeps the bot's own plans paused.</summary>
    public static bool IsHeld(int botId, long nowMs)
    {
        lock (Sync)
            return HeldUntil.Count > 0 && HeldUntil.TryGetValue(botId, out long until) && nowMs < until;
    }

    /// <summary>Forgets cooldowns, promises and test holds (tests, /dialogue forget).</summary>
    public static void Clear()
    {
        lock (Sync)
        {
            LastCall.Clear();
            Promises.Clear();
            HeldUntil.Clear();
        }
    }

    /// <summary>The state of a bot in the game hearing a player's call.</summary>
    public static GroupCallState StateOf(GameBot bot, GamePlayer player, bool sulking)
    {
        bool sameGroup = bot.Group != null && bot.Group == player.Group;
        bool full = player.Group != null && player.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER;
        return new GroupCallState(sameGroup, full, bot.InCombat, bot.IsOnHorse || bot.IsOnStableMasterRoute,
            !sameGroup && bot.Group != null && bot.IsPlayerLedGroup, bot.Level, player.Level, sulking);
    }

    /// <summary>The bots that answer, in order, with their answers: never Quiet, at most <see cref="MaxAnswers"/>.</summary>
    public static List<(T Bot, eGroupCallAnswer Answer)> Pick<T>(IEnumerable<(T Bot, eGroupCallAnswer Answer)> decided, Random random)
    {
        List<(T Bot, eGroupCallAnswer Answer)> speaking = decided.Where(pair => pair.Answer != eGroupCallAnswer.Quiet).Take(MaxWeighed).ToList();
        return speaking.Take(AnswerCount(speaking.Count, random)).ToList();
    }
}
