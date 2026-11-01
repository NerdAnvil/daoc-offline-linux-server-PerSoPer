using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// Intent Part B3c, a gag: very rarely, a bot that groans at a player's bad joke says "that's it, I'm out"
/// ("~ reply jokequit"), storms off and comes back a few seconds later ("~ reply jokereturn"). A world bot
/// (Shempenstein) really leaves the group and rejoins; a companion (Persoper, /spawn bots) stays in the group
/// and only walks off, since a companion out of its group is cleaned up. It is no insult and no annoyance.
/// Rare: one roll of <see cref="SessionChance"/> per play session (per login), and after it happens, not again
/// for that player for <see cref="Cooldown"/> (saved with the player's answers, so a restart doesn't reset it).
/// Never in or near a fight, casting, riding, resting, stealthed, in a dungeon, or with the player trading;
/// a world bot only when the player leads the group. Rejoining is tried a few times, then a tell asks for an invite.
/// </summary>
public static class PersonalityWalkOff
{
    private static readonly Logger Log = LoggerManager.Create(typeof(PersonalityWalkOff));

    /// <summary>How likely a play session is to get the gag (rolled once, at its first groan).</summary>
    public const double SessionChance = 0.02;
    /// <summary>After the gag, none for this player for this long (real time).</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
    /// <summary>Where the last gag's time is saved (PlayerAnswers): Unix seconds.</summary>
    public const string CooldownKey = "walkoff_gag";
    /// <summary>How long it stays away (the walk out included), and how far out it goes.</summary>
    public const int AwayMs = 14_000;
    public const int AwayDistance = 320;
    /// <summary>Being hit this recently counts as a fight.</summary>
    public const int RecentlyHitMs = 20_000;
    /// <summary>Rejoin tries at most, this far apart, before the bot asks for an invite instead.</summary>
    public const int RejoinTries = 3;
    public const int RejoinGapMs = 3_000;
    /// <summary>Bots walking off tracked at most.</summary>
    public const int MaxTracked = 64;
    /// <summary>How long after the walk-off its return is watched (for server.log and the in-game check).</summary>
    public const int ReturnWatchMs = 15_000;

    /// <summary>What the guards look at, apart from the game objects, so the rules can be unit tested.</summary>
    public readonly record struct Situation(
        bool InGroupWithPlayer, bool Alive, bool InWorld, bool SameRegionNear,
        bool InCombat, bool Attacking, bool Casting, bool HasAggro, long MsSinceHit,
        bool Riding, bool OnStableRoute, bool Resting, bool Stealthed, bool InDungeon,
        bool LeadsGroup, bool GroupFighting, bool PlayerTrading, bool QuitsGroup, bool PlayerLeads);

    private static readonly object Sync = new();
    private static readonly Dictionary<int, long> AwayUntil = new();
    private static readonly Dictionary<int, long> Returning = new();
    // One roll per play session: a GamePlayer object lives for one login.
    private static readonly ConditionalWeakTable<GamePlayer, StrongBox<bool>> SessionRolls = new();

    /// <summary>Test shortcut (GM command "/dialogue walkoff always"): every groan gets it, no session roll or cooldown. The guards stay on.</summary>
    /// <remarks>"/dialogue walkoff reset" forgets the player's cooldown (saves 0).</remarks>
    public static bool Always { get; set; }

    /// <summary>Why the bot must not storm off now, or null when nothing stops it. Pure.</summary>
    public static string Blocked(Situation s) =>
        !s.InGroupWithPlayer ? "not in the player's group"
        : !s.Alive || !s.InWorld ? "not up"
        : !s.SameRegionNear ? "not near the player"
        : s.InCombat || s.Attacking || s.HasAggro ? "in a fight"
        : s.Casting ? "casting"
        : s.MsSinceHit < RecentlyHitMs ? "hit lately"
        : s.GroupFighting ? "the group is fighting"
        : s.Riding || s.OnStableRoute ? "riding"
        : s.Resting ? "resting"
        : s.Stealthed ? "stealthed"
        : s.InDungeon ? "in a dungeon"
        : s.LeadsGroup ? "leading the group"
        : s.PlayerTrading ? "the player is trading"
        : s.QuitsGroup && !s.PlayerLeads ? "the player doesn't lead the group"
        : null;

    /// <summary>True while the last gag for this player is less than <see cref="Cooldown"/> ago. Pure.</summary>
    public static bool CoolingDown(string savedUnixSeconds, DateTime nowUtc) =>
        long.TryParse(savedUnixSeconds, out long seconds) &&
        nowUtc - DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(seconds, 0, 253_402_300_799)).UtcDateTime < Cooldown;

    /// <summary>The session's one roll, made at its first groan. Pure.</summary>
    public static bool RollSession(Random random) => (random ?? Random.Shared).NextDouble() < SessionChance;

    /// <summary>The formation spot while a companion walks off: same direction from the leader, much farther out. Pure.</summary>
    public static AutonomousFormation.Offset Away(AutonomousFormation.Offset normal) =>
        new(Math.Max(normal.Distance, AwayDistance), normal.AngleDegrees);

    /// <summary>A world bot leaves the group and rejoins; a companion only walks off.</summary>
    public static bool QuitsGroup(GameBot bot) => bot is { IsAutonomousWorldBot: true, IsTemporaryGroupHelper: false };

    /// <summary>Why the bot can't rejoin the player's group now, or null. Pure.</summary>
    public static string RejoinBlocked(bool playerHere, bool sameRealm, bool botUp, bool botInAGroup, bool playerLeadsOrSolo, bool groupFull) =>
        !playerHere ? "the player left" : !sameRealm ? "another realm" : !botUp ? "the bot is down"
        : botInAGroup ? "the bot is in a group" : !playerLeadsOrSolo ? "the player doesn't lead the group"
        : groupFull ? "the group is full" : null;

    /// <summary>The guards on the real objects (null: it may storm off).</summary>
    public static string BlockedNow(GameBot bot, GamePlayer player, long now)
    {
        if (bot == null || player == null)
            return "no bot or player";
        bool together = bot.Group != null && bot.Group == player.Group;
        bool fighting = together && bot.Group.GetMembersInTheGroup().Any(member => member.InCombat || member.IsAttacking);
        return Blocked(new Situation(
            together, bot.IsAlive, bot.ObjectState == GameObject.eObjectState.Active,
            bot.CurrentRegion != null && bot.CurrentRegion == player.CurrentRegion && bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE),
            bot.InCombat, bot.IsAttacking, bot.IsCasting, bot.Brain is BotBrain { HasAggro: true },
            bot.LastAttackedByEnemyTick == 0 ? long.MaxValue : now - bot.LastAttackedByEnemyTick,
            bot.IsOnHorse, bot.IsOnStableMasterRoute, bot.IsRecoveryResting, bot.IsStealthed,
            bot.CurrentRegion?.IsDungeon == true, bot.Group?.LivingLeader == bot, fighting || player.InCombat || player.IsAttacking,
            player.TradeWindow != null, QuitsGroup(bot), bot.Group?.LivingLeader == player));
    }

    /// <summary>
    /// After a groan: true when this one gets the gag. Nothing changes but the session's roll; the gag
    /// starts with <see cref="Begin"/> when the line is said.
    /// </summary>
    public static bool Roll(GameBot bot, GamePlayer player, long now, Random random)
    {
        if (bot == null || player == null || BlockedNow(bot, player, now) != null)
            return false;
        if (Always)
            return true;
        if (CoolingDown(PlayerAnswers.Cache.Get(player.InternalID, CooldownKey), DateTime.UtcNow))
            return false;
        StrongBox<bool> roll;
        lock (Sync)
        {
            // Rolled once per session; a session that won stays won until the gag happens.
            if (SessionRolls.TryGetValue(player, out roll))
                return roll.Value;
            roll = new StrongBox<bool>(RollSession(random));
            SessionRolls.Add(player, roll);
        }
        Log.Info($"PERSONALITY_WALKOFF session player=\"{player.Name}\" won={roll.Value}");
        return roll.Value;
    }

    /// <summary>
    /// The gag itself: <paramref name="sayQuit"/> says the "I'm out" line, then a world bot leaves the group and
    /// walks off, or a companion walks off; after <see cref="AwayMs"/> it is back with <paramref name="back"/>.
    /// False (say nothing more) when the guards say no by now or nothing was said.
    /// </summary>
    public static bool Begin(GameBot bot, GamePlayer player, long now, Func<bool> sayQuit, string back)
    {
        if (BlockedNow(bot, player, now) is { } reason)
        {
            Log.Info($"PERSONALITY_WALKOFF blocked bot=\"{bot?.Name}\" player=\"{player?.Name}\" reason=\"{reason}\"");
            return false;
        }
        lock (Sync)
        {
            Prune(now);
            if (AwayUntil.Count >= MaxTracked)
                return false;
        }
        if (!sayQuit())
            return false;
        // Saved even for the test shortcut, so a check can see the cooldown hold ("/dialogue walkoff reset" clears it).
        PlayerAnswers.Cache.Set(player.InternalID, CooldownKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        bool quits = QuitsGroup(bot);
        if (quits)
        {
            bot.Group.RemoveMember(bot);
            bot.StopFollowing();
            bot.WalkTo(AwaySpot(bot, player), bot.MaxSpeed);
        }
        lock (Sync)
        {
            // A companion walks out from its formation spot (Adjust); a world bot's return is watched the same way.
            AwayUntil[bot.ObjectID] = now + AwayMs;
            SessionRolls.Remove(player);
        }
        Log.Info($"PERSONALITY_WALKOFF start bot=\"{bot.Name}\" player=\"{player.Name}\" mode={(quits ? "quit" : "walk")} seconds={AwayMs / 1000}");
        _ = new ECSGameTimer(bot, _ =>
        {
            Return(bot, player, quits, back, RejoinTries);
            return 0;
        }, AwayMs);
        return true;
    }

    // Straight out from the player, through the bot, AwayDistance from the player.
    private static Vector3 AwaySpot(GameBot bot, GamePlayer player)
    {
        float dx = bot.X - player.X, dy = bot.Y - player.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1f)
            (dx, dy, length) = (1f, 0f, 1f);
        return new(player.X + dx / length * AwayDistance, player.Y + dy / length * AwayDistance, bot.Z);
    }

    // The walk-off is over: a companion is back in formation by now (Adjust); a world bot rejoins, tried a few times.
    private static void Return(GameBot bot, GamePlayer player, bool quits, string back, int triesLeft)
    {
        if (!quits)
        {
            if (bot.Group != null && bot.Group == player.Group)
                Say(bot, back);
            return;
        }
        bool playerHere = player.ObjectState == GameObject.eObjectState.Active && player.Client != null;
        string blocked = RejoinBlocked(playerHere, bot.Realm == player.Realm,
            bot.IsAlive && bot.ObjectState == GameObject.eObjectState.Active, bot.Group != null,
            player.Group == null || player.Group.Leader == player,
            player.Group != null && player.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER);
        if (blocked == null && Rejoin(bot, player))
        {
            Log.Info($"PERSONALITY_WALKOFF rejoined bot=\"{bot.Name}\" player=\"{player.Name}\"");
            Say(bot, back);
            return;
        }
        if (triesLeft > 1 && playerHere)
        {
            _ = new ECSGameTimer(bot, _ =>
            {
                Return(bot, player, true, back, triesLeft - 1);
                return 0;
            }, RejoinGapMs);
            return;
        }
        lock (Sync)
            AwayUntil.Remove(bot.ObjectID);
        Log.Info($"PERSONALITY_WALKOFF norejoin bot=\"{bot.Name}\" player=\"{player.Name}\" reason=\"{blocked ?? "the join failed"}\"");
        if (playerHere)
            ChatUtil.SendSendMessage(player.Client, "Social.ReceiveMessage.Private.Sends", bot.Name,
                $"ok, I'm over it, {player.Name}. Invite me back when you can.");
    }

    // As BotGroupInvite.TryInvite, without its greeting and spec lines (it's only been a few seconds).
    private static bool Rejoin(GameBot bot, GamePlayer player)
    {
        if (player.Group == null)
        {
            var group = new Group(player);
            GroupMgr.AddGroup(group);
            group.AddMember(player);
        }
        if (!player.Group.AddMember(bot) || !bot.EnterPlayerLedGroup(player))
            return false;
        bot.PathTo(player, bot.MaxSpeed);
        return true;
    }

    private static void Say(GameBot bot, string line)
    {
        if (!string.IsNullOrWhiteSpace(line) && bot.Group != null)
            bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
    }

    /// <summary>
    /// The formation spot BotBrain.FollowFormation should use: the normal one, or the far one while a companion
    /// walks off. A fight or the time running out ends the walk; the return is logged once.
    /// </summary>
    public static AutonomousFormation.Offset Adjust(GameBot bot, GamePlayer leader, AutonomousFormation.Offset normal, long now)
    {
        long until;
        lock (Sync)
        {
            if (AwayUntil.Count == 0 && Returning.Count == 0)
                return normal;
            if (!AwayUntil.TryGetValue(bot.ObjectID, out until))
            {
                if (Returning.TryGetValue(bot.ObjectID, out long watch))
                    WatchReturn(bot, leader, normal, now, watch);
                return normal;
            }
        }
        // A world bot following again has rejoined; a companion ends on time or when a fight starts.
        bool timeUp = now >= until || QuitsGroup(bot);
        string stop = timeUp ? "time is up" : FightReason(bot, leader);
        if (stop == null)
            return Away(normal);
        lock (Sync)
        {
            AwayUntil.Remove(bot.ObjectID);
            Returning[bot.ObjectID] = now + ReturnWatchMs;
        }
        Log.Info($"PERSONALITY_WALKOFF end bot=\"{bot.Name}\" player=\"{leader.Name}\" reason=\"{stop}\" distance={(int)bot.GetDistanceTo(leader)}");
        return normal;
    }

    // Under Sync: logs once that the bot is back near its formation spot, or still away when the watch runs out.
    private static void WatchReturn(GameBot bot, GamePlayer leader, AutonomousFormation.Offset normal, long now, long watch)
    {
        int distance = (int)bot.GetDistanceTo(leader);
        bool back = distance <= normal.Distance + 80;
        if (!back && now < watch)
            return;
        Returning.Remove(bot.ObjectID);
        Log.Info($"PERSONALITY_WALKOFF {(back ? "returned" : "notback")} bot=\"{bot.Name}\" player=\"{leader.Name}\" distance={distance}");
    }

    // The fight guards alone: a walk-off already under way ends on these.
    private static string FightReason(GameBot bot, GamePlayer leader) =>
        !bot.IsAlive || bot.Group == null || bot.Group != leader.Group ? "left the group"
        : bot.InCombat || bot.IsAttacking || bot.Brain is BotBrain { HasAggro: true } || leader.InCombat ? "a fight started"
        : null;

    /// <summary>Forgets every walk-off (for tests).</summary>
    public static void Clear()
    {
        lock (Sync)
        {
            AwayUntil.Clear();
            Returning.Clear();
        }
        Always = false;
    }

    /// <summary>True while the bot is storming off (B4: "where are you?" could use it).</summary>
    public static bool IsAway(GameBot bot, long now)
    {
        lock (Sync)
            return bot != null && AwayUntil.TryGetValue(bot.ObjectID, out long until) && now < until;
    }

    // Under Sync. Entries left by a bot that never came back through Adjust go once they are well past due.
    private static void Prune(long now)
    {
        long stale = AwayMs + RejoinTries * RejoinGapMs + ReturnWatchMs;
        foreach (int id in AwayUntil.Where(entry => now - entry.Value > stale).Select(entry => entry.Key).ToList())
            AwayUntil.Remove(id);
        foreach (int id in Returning.Where(entry => now >= entry.Value).Select(entry => entry.Key).ToList())
            Returning.Remove(id);
    }
}
