using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;

namespace DOL.GS;

/// <summary>A bot's answer to an order: silence, yes (it obeys), no, busy, or "you can't order me".</summary>
public enum eOrderAnswer
{
    Quiet,
    Yes,
    No,
    Busy,
    NotYours,
}

/// <summary>
/// What a bot's answer to an order is decided from, all read from the game: whether the speaker may order it (see
/// <see cref="PersonalityOrders.MayOrder"/>), whether it is a companion (Persoper, a /spawn helper), its fight or ride,
/// and a sulk at the speaker.
/// </summary>
public sealed record OrderState(bool Allowed, bool Companion, bool Fighting, bool Riding, bool Sulking);

/// <summary>
/// Orders players give bots in chat (intent Part B6): "follow me", "stay here", "come here" (ReadOrder in
/// AutonomousChatIntentModel). Who obeys whom, by bot kind:
/// <list type="bullet">
/// <item>Companions (Persoper, /spawn helpers): their owner and the leader of their group. They never refuse by
/// type or sulk; only a fight or a ride stops them (busy). /spawn helpers have no personality and obey silently.</item>
/// <item>Autonomous world bots in a player's group (named characters, typed bots, plain bots): the group's leader
/// only. Busy while fighting or riding; "no" when sulking at the leader; "stay" now and then refused by type
/// (<see cref="RefuseChance"/>). Plain bots obey silently.</item>
/// <item>Every other bot (not in the speaker's group, or the speaker isn't its leader or owner): never obeys. A named
/// personality bot says so ("~ reply ordernot"); an open order gets silence.</item>
/// </list>
/// Real players are never ordered. A bot in a player's group runs no trips of its own, so it is never mid town
/// teleport when it could obey. "Stay" holds the bot where it stands for at most <see cref="MaxStayMs"/>, until a
/// "follow" or "come", or until the leader leaves the region (BotBrain.FollowFormation asks <see cref="Holds"/>);
/// it still fights back. "Follow" and "come" end a hold and send it back into formation now.
/// </summary>
public static class PersonalityOrders
{
    /// <summary>At most this many bots take one order.</summary>
    public const int MaxTargets = 8;

    /// <summary>At most this many bots answer an open order ("everyone stay"); a named one gets at most one answer.</summary>
    public const int MaxOpenAnswers = 2;

    /// <summary>A stay holds at most this long.</summary>
    public const long MaxStayMs = 300_000;

    /// <summary>Within this time of the same player's last answered order, bots still obey but say nothing.</summary>
    public const long CooldownMs = 8_000;

    private const int MaxRemembered = 256;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, long> LastAnswered = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, (long Until, ushort Region)> Staying = new();

    /// <summary>Test switch (/dialogue orders always): no refusals by type, and a named order is always answered.</summary>
    public static bool Always { get; set; }

    /// <summary>The chance a type turns down "stay" when it could obey: Leeroy hates waiting, a loner does as it likes.</summary>
    public static double RefuseChance(string typeKey) => typeKey switch
    {
        "leeroy" => 0.35,
        "loner" => 0.15,
        "hardcore" or "minmaxer" => 0.1,
        _ => 0.05,
    };

    /// <summary>
    /// The bot's answer to an order. Not allowed: not yours. Fighting or riding: busy. A world bot sulking at the
    /// speaker: no. A world bot told to stay: no now and then by type. Else yes. Pure.
    /// </summary>
    public static eOrderAnswer Decide(eBotOrder order, OrderState state, string typeKey, Random random)
    {
        if (order == eBotOrder.None || state == null)
            return eOrderAnswer.Quiet;
        if (!state.Allowed)
            return eOrderAnswer.NotYours;
        if (state.Fighting || state.Riding)
            return eOrderAnswer.Busy;
        if (state.Companion)
            return eOrderAnswer.Yes;
        if (state.Sulking)
            return eOrderAnswer.No;
        if (order == eBotOrder.Stay && !Always && (random ?? Random.Shared).NextDouble() < RefuseChance(typeKey))
            return eOrderAnswer.No;
        return eOrderAnswer.Yes;
    }

    /// <summary>The .dlg section an answer is said from: the order's own for a yes; Unknown for Quiet.</summary>
    public static eAutonomousChatIntent KindFor(eBotOrder order, eOrderAnswer answer) => answer switch
    {
        eOrderAnswer.Yes => order switch
        {
            eBotOrder.Follow => eAutonomousChatIntent.OrderFollow,
            eBotOrder.Stay => eAutonomousChatIntent.OrderStay,
            eBotOrder.Come => eAutonomousChatIntent.OrderCome,
            _ => eAutonomousChatIntent.Unknown,
        },
        eOrderAnswer.No => eAutonomousChatIntent.OrderNo,
        eOrderAnswer.Busy => eAutonomousChatIntent.OrderBusy,
        eOrderAnswer.NotYours => eAutonomousChatIntent.OrderNot,
        _ => eAutonomousChatIntent.Unknown,
    };

    /// <summary>
    /// How many of the bots that would answer do. Named: one, now and then none (it just does it). Open: none to
    /// <see cref="MaxOpenAnswers"/>. Not-yours answers only come to a named order (strangers ignore an open one).
    /// </summary>
    public static int AnswerCount(int speaking, bool named, Random random)
    {
        if (speaking <= 0)
            return 0;
        double roll = (random ?? Random.Shared).NextDouble();
        if (named)
            return Always || roll >= 0.1 ? 1 : 0;
        int count = Always ? MaxOpenAnswers : roll < 0.15 ? 0 : roll < 0.75 ? 1 : 2;
        return Math.Min(speaking, count);
    }

    /// <summary>
    /// The bot's line for its answer, filled from its state. A busy reason without lines falls back to its "no"; a
    /// yes without lines is silence (it just does it). Null when it has nothing to say.
    /// </summary>
    public static string Line(BotPersonality personality, eBotOrder order, eOrderAnswer answer, StateSlots slots, DialogueFacts facts,
        string speakerName, Random random, Func<string, DialogueRule> ruleOf = null)
    {
        eAutonomousChatIntent kind = KindFor(order, answer);
        if (personality == null || kind == eAutonomousChatIntent.Unknown)
            return null;
        string said = PersonalityStateAnswers.Pick(personality, kind, slots, facts, speakerName, random, ruleOf);
        if (said != null || answer != eOrderAnswer.Busy)
            return said;
        return PersonalityStateAnswers.Pick(personality, eAutonomousChatIntent.OrderNo, slots, facts, speakerName, random, ruleOf);
    }

    /// <summary>True when this player's order may be answered in chat now (and starts its cooldown).</summary>
    public static bool TryStart(string playerId, long nowMs)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        lock (Sync)
        {
            if (LastAnswered.TryGetValue(playerId, out long last) && nowMs - last >= 0 && nowMs - last < CooldownMs)
                return false;
            if (LastAnswered.Count >= MaxRemembered)
                LastAnswered.Clear();
            LastAnswered[playerId] = nowMs;
            return true;
        }
    }

    /// <summary>Holds a bot where it stands for up to <see cref="MaxStayMs"/>, while its leader stays in this region.</summary>
    public static void Stay(int botId, ushort region, long nowMs)
    {
        lock (Sync)
        {
            if (Staying.Count >= MaxRemembered)
                Staying.Clear();
            Staying[botId] = (nowMs + MaxStayMs, region);
        }
    }

    /// <summary>Ends a bot's stay.</summary>
    public static void Release(int botId)
    {
        lock (Sync)
            Staying.Remove(botId);
    }

    /// <summary>
    /// True while a stay holds the bot: not yet <see cref="MaxStayMs"/> old and the leader still in the region it
    /// was given in. A stay that no longer holds is forgotten.
    /// </summary>
    public static bool Holds(int botId, ushort leaderRegion, long nowMs)
    {
        lock (Sync)
        {
            if (Staying.Count == 0 || !Staying.TryGetValue(botId, out (long Until, ushort Region) stay))
                return false;
            if (nowMs < stay.Until && stay.Region == leaderRegion)
                return true;
            Staying.Remove(botId);
            return false;
        }
    }

    /// <summary>Seconds left on a bot's stay (0 when none), for /dialogue state.</summary>
    public static long StaySecondsLeft(int botId, long nowMs)
    {
        lock (Sync)
            return Staying.TryGetValue(botId, out (long Until, ushort Region) stay) && stay.Until > nowMs ? (stay.Until - nowMs + 999) / 1_000 : 0;
    }

    /// <summary>Forgets cooldowns and stays (tests, /dialogue forget).</summary>
    public static void Clear()
    {
        lock (Sync)
        {
            LastAnswered.Clear();
            Staying.Clear();
        }
    }

    /// <summary>
    /// True when this player may order this bot: a companion's owner, or the player who leads the group the bot is
    /// in (a companion's or a world bot's). Never a bot in another group or none, and never a real player.
    /// </summary>
    public static bool MayOrder(GameBot bot, GamePlayer player)
    {
        if (bot == null || player == null || bot.ObjectState != GameObject.eObjectState.Active)
            return false;
        if (bot.IsTemporaryGroupHelper && bot.Owner == player)
            return true;
        bool sameGroup = bot.Group != null && bot.Group == player.Group;
        return sameGroup && bot.Group.Leader == player && (bot.IsTemporaryGroupHelper || bot.IsAutonomousWorldBot && bot.IsPlayerLedGroup);
    }

    /// <summary>The state of a bot in the game hearing a player's order.</summary>
    public static OrderState StateOf(GameBot bot, GamePlayer player, bool sulking) =>
        new(MayOrder(bot, player), bot.IsTemporaryGroupHelper, bot.InCombat, bot.IsOnHorse || bot.IsOnStableMasterRoute, sulking);

    /// <summary>Carries out an order a bot said yes to: stay holds it where it stands; follow and come end a hold and send it back into formation now.</summary>
    public static void Apply(GameBot bot, GamePlayer player, eBotOrder order)
    {
        if (bot == null || player == null)
            return;
        if (order == eBotOrder.Stay)
        {
            Stay(bot.ObjectID, player.CurrentRegionID, GameLoop.GameLoopTime);
            bot.StopFollowing();
            bot.StopMoving();
            return;
        }
        Release(bot.ObjectID);
        if (bot.Brain is BotBrain brain && !brain.HasAggro)
        {
            brain.FSM.SetCurrentState(eFSMStateType.FOLLOW);
            brain.NextThinkTick = GameLoop.GameLoopTime;
        }
    }

    /// <summary>The bots that answer, in order, with their answers: never Quiet; not-yours only to a named order.</summary>
    public static List<(T Bot, eOrderAnswer Answer)> Pick<T>(IEnumerable<(T Bot, eOrderAnswer Answer)> decided, bool named, Random random)
    {
        List<(T Bot, eOrderAnswer Answer)> speaking = decided
            .Where(pair => pair.Answer != eOrderAnswer.Quiet && (named || pair.Answer != eOrderAnswer.NotYours))
            .Take(MaxTargets).ToList();
        return speaking.Take(AnswerCount(speaking.Count, named, random)).ToList();
    }
}
