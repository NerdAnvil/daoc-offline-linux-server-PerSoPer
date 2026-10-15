using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Personality bots' patience. Repeat irritations from one player within a few
/// minutes add up: the bot grumbles, warns twice, then quits on them and sulks.
/// Each personality's PatienceProfile sets the thresholds and lines. A companion
/// (Persoper) despawns; a world bot (Kobaldjovi) storms out of the group and
/// refuses invites while it sulks. Settings: persoper_annoyance, persoper_rage_quit
/// and persoper_sulk_minutes. The rules are pure and unit tested.
/// </summary>
public static class PersonalityAnnoyance
{
    public enum eIrritation
    {
        /// <summary>Summoned or invited again within a couple of minutes.</summary>
        QuickRecall,
        Rude,
        EmoteSpam,
        Death,
        /// <summary>Asking for heals again and again at full health.</summary>
        HealSpam,
    }

    public enum eReaction
    {
        Grumble,
        Warning,
        LastWarning,
        Quit,
    }

    /// <summary>Emotes at it within a minute that it lets pass before they count.</summary>
    public const int FreeEmotes = 2;
    private const int MaxTracked = 500;

    public static readonly TimeSpan QuickRecallWindow = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan EmoteWindow = TimeSpan.FromMinutes(1);

    /// <summary>One player's irritation count with one personality.</summary>
    public sealed class Tally
    {
        public int Points;
        public DateTime LastAt;
        public readonly List<DateTime> Emotes = [];
    }

    /// <summary>Adds one irritation; the count resets after the profile's calm-down time. Pure.</summary>
    public static eReaction Add(Tally tally, PatienceProfile profile, DateTime now, bool canQuit)
    {
        if (now - tally.LastAt > profile.CalmDown)
            tally.Points = 0;
        tally.Points++;
        tally.LastAt = now;
        if (tally.Points >= profile.QuitAt)
            return canQuit ? eReaction.Quit : eReaction.LastWarning;
        return tally.Points >= profile.LastWarnAt ? eReaction.LastWarning
            : tally.Points >= profile.WarnAt ? eReaction.Warning
            : eReaction.Grumble;
    }

    /// <summary>Notes an emote; true once there are more than the free ones within a minute. Pure.</summary>
    public static bool CountEmote(Tally tally, DateTime now)
    {
        tally.Emotes.RemoveAll(at => now - at > EmoteWindow);
        tally.Emotes.Add(now);
        if (tally.Emotes.Count > FreeEmotes + 3)
            tally.Emotes.RemoveAt(0);
        return tally.Emotes.Count > FreeEmotes;
    }

    /// <summary>True when the last meeting was so recent that calling it again is a nuisance.</summary>
    public static bool IsQuickRecall(string lastSeenUtc, DateTime now) =>
        TryParse(lastSeenUtc, out DateTime last) && now - last < QuickRecallWindow;

    /// <summary>Time left on a sulk, or zero.</summary>
    public static TimeSpan SulkLeft(string sulkUntilUtc, DateTime now) =>
        TryParse(sulkUntilUtc, out DateTime until) && until > now ? until - now : TimeSpan.Zero;

    public static string Line(PatienceProfile profile, eIrritation irritation, eReaction reaction, string playerName, Random random)
    {
        random ??= Random.Shared;
        string[] lines = reaction switch
        {
            eReaction.Quit => profile.Quits,
            eReaction.LastWarning => profile.LastWarnings,
            eReaction.Warning => profile.Warnings,
            _ => profile.Grumbles.GetValueOrDefault(irritation) ?? profile.Warnings,
        };
        return string.Format(lines[random.Next(lines.Length)], playerName);
    }

    public static string SulkRefusal(PatienceProfile profile, string playerName, TimeSpan left, Random random)
    {
        random ??= Random.Shared;
        int minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        string time = minutes == 1 ? "a minute" : $"{minutes} minutes";
        return string.Format(profile.SulkRefusals[random.Next(profile.SulkRefusals.Length)], playerName, time);
    }

    /// <summary>When a world bot that stormed off makes up: when its sulk ends, or shortly after with no sulk.</summary>
    public static TimeSpan MakeUpDelay(int sulkMinutes) =>
        sulkMinutes > 0 ? TimeSpan.FromMinutes(Math.Min(sulkMinutes, 100)) : TimeSpan.FromSeconds(30);

    private static bool TryParse(string utc, out DateTime value) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);

    // --- In the running server ---

    private static readonly object Sync = new();
    private static readonly Dictionary<string, Tally> Tallies = new();

    private static Tally TallyFor(BotPersonality personality, GamePlayer player)
    {
        string key = PersonalityMemoryRecord.KeyFor(personality.Key, player.InternalID);
        lock (Sync)
        {
            if (!Tallies.TryGetValue(key, out Tally tally))
            {
                if (Tallies.Count >= MaxTracked)
                {
                    // Bounded: drop the quietest entries.
                    foreach (string stale in Tallies.OrderBy(entry => entry.Value.LastAt).Take(MaxTracked / 5).Select(entry => entry.Key).ToList())
                        Tallies.Remove(stale);
                }
                tally = new Tally();
                Tallies[key] = tally;
            }
            return tally;
        }
    }

    /// <summary>Time left on this personality's sulk with this player (zero when not sulking).</summary>
    public static TimeSpan SulkLeft(BotPersonality personality, GamePlayer player) =>
        SulkLeft(PersonalityMemoryStore.Load(personality, player)?.SulkUntilUtc, DateTime.UtcNow);

    /// <summary>An emote aimed at a personality bot; only spam counts.</summary>
    public static void OnEmote(GamePlayer player, GameBot bot)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        if (!PersoperProperties.ANNOYANCE || player == null || personality?.Patience == null)
            return;
        bool spam;
        lock (Sync)
            spam = CountEmote(TallyFor(personality, player), DateTime.UtcNow);
        if (spam)
            Irritate(player, bot, eIrritation.EmoteSpam);
    }

    /// <summary>The player died: every personality bot in their group that minds deaths takes note.</summary>
    public static void OnPlayerDeath(GamePlayer player)
    {
        if (!PersoperProperties.ANNOYANCE || player?.Group == null)
            return;
        foreach (GameBot bot in player.Group.GetMembersInTheGroup().OfType<GameBot>().ToList())
        {
            if (BotPersonalities.For(bot)?.Patience is { CountsDeaths: true })
                Irritate(player, bot, eIrritation.Death);
        }
    }

    /// <summary>Counts one irritation from this player; the bot reacts, and may quit on them.</summary>
    public static void Irritate(GamePlayer player, GameBot bot, eIrritation irritation)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        PatienceProfile profile = personality?.Patience;
        if (!PersoperProperties.ANNOYANCE || player == null || profile == null ||
            bot.ObjectState != GameObject.eObjectState.Active ||
            (irritation == eIrritation.Death && !profile.CountsDeaths))
            return;

        DateTime now = DateTime.UtcNow;
        eReaction reaction;
        lock (Sync)
        {
            Tally tally = TallyFor(personality, player);
            reaction = Add(tally, profile, now, PersoperProperties.RAGE_QUIT);
            if (reaction == eReaction.Quit)
                tally.Points = 0;
        }

        bot.Say(Line(profile, irritation, reaction, player.Name, Random.Shared));
        if (reaction != eReaction.Quit)
            return;

        int minutes = Math.Clamp(PersoperProperties.SULK_MINUTES, 0, 100);
        if (minutes > 0 && PersonalityMemoryStore.Load(personality, player) is { } memory)
        {
            memory.SulkUntilUtc = now.AddMinutes(minutes).ToString("O");
            PersonalityMemoryStore.Save(memory);
        }

        if (bot.IsTemporaryGroupHelper)
        {
            bot.Delete();
            return;
        }

        if (bot.Group != null && bot.Group == player.Group)
            bot.Group.RemoveMember(bot);
        if (profile.MakeUps is { Length: > 0 })
        {
            // A world bot comes round once the sulk is over: a make-up tell, then it rejoins.
            _ = new ECSGameTimer(bot, _ =>
            {
                MakeUp(player, bot, personality);
                return 0;
            }, (int)MakeUpDelay(minutes).TotalMilliseconds);
        }
    }

    /// <summary>The sulk is over: tell the player so, and rejoin their group when it can.</summary>
    private static void MakeUp(GamePlayer player, GameBot bot, BotPersonality personality)
    {
        PatienceProfile profile = personality.Patience;
        if (player?.Client == null || player.ObjectState != GameObject.eObjectState.Active ||
            bot.ObjectState != GameObject.eObjectState.Active || bot.Realm != player.Realm)
            return;
        // Quit on them again since (a newer sulk), or already back in their group.
        if (SulkLeft(personality, player) > TimeSpan.Zero || (bot.Group != null && bot.Group == player.Group))
            return;

        bool canRejoin = bot.PlayerGroupLeader == null &&
            (player.Group == null ||
             (player.Group.Leader == player && player.Group.MemberCount < ServerProperties.Properties.GROUP_MAX_MEMBER));
        string[] lines = canRejoin ? profile.MakeUps : profile.MakeUpInvites ?? profile.MakeUps;
        ChatUtil.SendSendMessage(player.Client, "Social.ReceiveMessage.Private.Sends", bot.Name,
            string.Format(lines[Random.Shared.Next(lines.Length)], player.Name));
        if (canRejoin)
            BotGroupInvite.TryInvite(player, bot, countQuickRecall: false);
    }
}
