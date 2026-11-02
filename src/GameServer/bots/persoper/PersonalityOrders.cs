using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// A bot's answer to an order: silence, yes (it obeys), no, busy, "you can't order me", and (B6b) nothing to assist on,
/// full already (nothing to rest off), or stormed off after a bad joke.
/// </summary>
public enum eOrderAnswer
{
    Quiet,
    Yes,
    No,
    Busy,
    NotYours,
    NoTarget,
    Full,
    Away,
}

/// <summary>
/// What a bot's answer to an order is decided from, all read from the game: whether the speaker may order it (see
/// <see cref="PersonalityOrders.MayOrder"/>), whether it is a companion (Persoper, a /spawn helper), its fight or ride,
/// and a sulk at the speaker. B6b: whether it has stormed off after a bad joke (PersonalityWalkOff), whether the leader
/// is fighting something it could assist on, and whether it has anything to rest off.
/// </summary>
public sealed record OrderState(bool Allowed, bool Companion, bool Fighting, bool Riding, bool Sulking,
    bool Away = false, bool HasTarget = false, bool NeedsRest = true);

/// <summary>
/// Orders players give bots in chat (intent Part B6): "follow me", "stay here", "come here"; and B6b: "leave the
/// group", "assist me", "rest up" (ReadOrder in AutonomousChatIntentModel). Who obeys whom, by bot kind:
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
/// <para>B6b. "Leave the group" is taken only by a bot named in it: a world bot says its line and leaves a moment later
/// (as if kicked); a companion says no (out of its group it would be cleaned up). "Assist" works only while the leader
/// already fights something (PlayerLedPullCoordinator.FindLeaderTarget): the bot joins in through
/// BotBrain.AssistPlayerAttack, as its own think does, so it never starts a new pull; with no fight it asks "on what?".
/// "Rest" starts the bot's own recovery rest when it has something to rest off ("full" otherwise); its own AI still
/// ends it as before. A bot that has stormed off after a bad joke (PersonalityWalkOff) refuses every order in
/// character and does nothing.</para>
/// </summary>
public static class PersonalityOrders
{
    private static readonly Logger Log = LoggerManager.Create(typeof(PersonalityOrders));

    /// <summary>At most this many bots take one order.</summary>
    public const int MaxTargets = 8;

    /// <summary>At most this many bots answer an open order ("everyone stay"); a named one gets at most one answer.</summary>
    public const int MaxOpenAnswers = 2;

    /// <summary>A stay holds at most this long.</summary>
    public const long MaxStayMs = 300_000;

    /// <summary>Within this time of the same player's last answered order, bots still obey but say nothing.</summary>
    public const long CooldownMs = 8_000;

    /// <summary>A world bot told to leave the group goes this long after the order, so its answer is still said in the group.</summary>
    public const int LeaveDelayMs = 3_500;

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

    /// <summary>True for an order only a bot named in it takes ("shemp, leave the group"); an open one goes to nobody.</summary>
    public static bool NamedOnly(eBotOrder order) => order == eBotOrder.Leave;

    /// <summary>
    /// The bot's answer to an order. Stormed off after a bad joke: away. Not allowed: not yours. Riding, or fighting
    /// (except for "assist"): busy. Leave: a companion says no, a world bot yes (a sulk only makes it gladder to go).
    /// Assist with no fight to join: no target. Rest with nothing to rest off: full. A world bot sulking at the
    /// speaker: no. A world bot told to stay: no now and then by type. Else yes. Pure.
    /// </summary>
    public static eOrderAnswer Decide(eBotOrder order, OrderState state, string typeKey, Random random)
    {
        if (order == eBotOrder.None || state == null)
            return eOrderAnswer.Quiet;
        if (state.Away)
            return eOrderAnswer.Away;
        if (!state.Allowed)
            return eOrderAnswer.NotYours;
        if (state.Riding || state.Fighting && order != eBotOrder.Assist)
            return eOrderAnswer.Busy;
        if (order == eBotOrder.Leave)
            return state.Companion ? eOrderAnswer.No : eOrderAnswer.Yes;
        if (order == eBotOrder.Assist && !state.HasTarget)
            return eOrderAnswer.NoTarget;
        if (order == eBotOrder.Rest && !state.NeedsRest)
            return eOrderAnswer.Full;
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
            eBotOrder.Leave => eAutonomousChatIntent.OrderLeave,
            eBotOrder.Assist => eAutonomousChatIntent.OrderAssist,
            eBotOrder.Rest => eAutonomousChatIntent.OrderRest,
            _ => eAutonomousChatIntent.Unknown,
        },
        eOrderAnswer.No => eAutonomousChatIntent.OrderNo,
        eOrderAnswer.Busy => eAutonomousChatIntent.OrderBusy,
        eOrderAnswer.NoTarget => eAutonomousChatIntent.OrderNoTarget,
        eOrderAnswer.Full => eAutonomousChatIntent.OrderFull,
        eOrderAnswer.Away => eAutonomousChatIntent.OrderAway,
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
    /// The bot's line for its answer, filled from its state. A busy, no-target, full or away answer without lines
    /// falls back to its "no"; a yes without lines is silence (it just does it). Null when it has nothing to say.
    /// </summary>
    public static string Line(BotPersonality personality, eBotOrder order, eOrderAnswer answer, StateSlots slots, DialogueFacts facts,
        string speakerName, Random random, Func<string, DialogueRule> ruleOf = null)
    {
        eAutonomousChatIntent kind = KindFor(order, answer);
        if (personality == null || kind == eAutonomousChatIntent.Unknown)
            return null;
        string said = PersonalityStateAnswers.Pick(personality, kind, slots, facts, speakerName, random, ruleOf);
        if (said != null || answer is not (eOrderAnswer.Busy or eOrderAnswer.NoTarget or eOrderAnswer.Full or eOrderAnswer.Away))
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

    /// <summary>
    /// The state of a bot in the game hearing a player's order. For "rest", anything that keeps it from resting (a
    /// fight just now, its pet fighting) counts as fighting; the fight to assist on is the one the player is in.
    /// </summary>
    public static OrderState StateOf(GameBot bot, GamePlayer player, bool sulking, eBotOrder order = eBotOrder.None)
    {
        bool fighting = bot.InCombat || order == eBotOrder.Rest && BotRestRecovery.BlocksRest(bot);
        bool hasTarget = order == eBotOrder.Assist && AssistTarget(bot, player) != null;
        bool needsRest = order != eBotOrder.Rest || BotRestRecovery.NeedsOrContinuesRest(bot);
        return new(MayOrder(bot, player), bot.IsTemporaryGroupHelper, fighting, bot.IsOnHorse || bot.IsOnStableMasterRoute,
            sulking, PersonalityWalkOff.IsAway(bot, GameLoop.GameLoopTime), hasTarget, needsRest);
    }

    /// <summary>What the player is fighting, when the bot is near enough to join in; null when nothing.</summary>
    public static GameLiving AssistTarget(GameBot bot, GamePlayer player)
    {
        GameLiving target = PlayerLedPullCoordinator.FindLeaderTarget(player);
        return target != null && PlayerLedPullCoordinator.Available(bot, player) &&
            bot.IsWithinRadius(target, BotBrain.GROUP_DEFENSE_ASSIST_RADIUS) ? target : null;
    }

    /// <summary>
    /// Carries out an order a bot said yes to: stay holds it where it stands; follow and come end a hold and send it back
    /// into formation now; leave takes a world bot out of the group after <see cref="LeaveDelayMs"/>; assist joins the
    /// player's fight; rest starts its recovery rest.
    /// </summary>
    public static void Apply(GameBot bot, GamePlayer player, eBotOrder order)
    {
        if (bot == null || player == null)
            return;
        switch (order)
        {
            case eBotOrder.Leave:
                Release(bot.ObjectID);
                _ = new ECSGameTimer(bot, _ =>
                {
                    Leave(bot, player);
                    return 0;
                }, LeaveDelayMs);
                return;
            case eBotOrder.Assist:
                if (AssistTarget(bot, player) is { } target && bot.Brain is BotBrain assisting)
                {
                    Release(bot.ObjectID);
                    assisting.AssistPlayerAttack(target);
                }
                return;
            case eBotOrder.Rest:
                bot.BeginRecoveryRest();
                return;
        }
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

    // Only a world bot still in the player's group and out of a fight leaves; the same way as being kicked.
    private static void Leave(GameBot bot, GamePlayer player)
    {
        if (bot.Group == null || bot.Group != player.Group || bot.IsTemporaryGroupHelper || !bot.IsAutonomousWorldBot ||
            !bot.IsAlive || bot.InCombat || bot.IsOnStableMasterRoute)
        {
            Log.Info($"PERSONALITY_ORDER stayed bot=\"{bot.Name}\" player=\"{player.Name}\" (no longer free to leave)");
            return;
        }
        bot.Group.RemoveMember(bot);
        Log.Info($"PERSONALITY_ORDER left bot=\"{bot.Name}\" player=\"{player.Name}\"");
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
