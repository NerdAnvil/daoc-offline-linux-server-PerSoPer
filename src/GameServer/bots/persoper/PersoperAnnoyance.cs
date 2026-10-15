using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Persoper's patience. Repeat irritations from one player within a few minutes
/// add up: she grumbles, warns twice, then rage-quits (leaves the group) and
/// sulks for a while. Settings: persoper_annoyance, persoper_rage_quit and
/// persoper_sulk_minutes. The rules are pure and unit tested; the tally lives
/// in memory, the sulk in her memory table.
/// </summary>
public static class PersoperAnnoyance
{
    public enum eIrritation
    {
        QuickResummon,
        Rude,
        EmoteSpam,
        Death,
    }

    public enum eReaction
    {
        Grumble,
        Warning,
        LastWarning,
        RageQuit,
    }

    public const int WarnAt = 3;
    public const int LastWarnAt = 4;
    public const int QuitAt = 5;
    /// <summary>Emotes at her within a minute that she lets pass before they count.</summary>
    public const int FreeEmotes = 2;
    private const int MaxTrackedPlayers = 500;

    public static readonly TimeSpan CalmDown = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan QuickResummonWindow = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan EmoteWindow = TimeSpan.FromMinutes(1);

    /// <summary>One player's irritation count.</summary>
    public sealed class Tally
    {
        public int Points;
        public DateTime LastAt;
        public readonly List<DateTime> Emotes = [];
    }

    /// <summary>Adds one irritation; the count resets after a quiet spell. Pure.</summary>
    public static eReaction Add(Tally tally, DateTime now, bool rageQuit)
    {
        if (now - tally.LastAt > CalmDown)
            tally.Points = 0;
        tally.Points++;
        tally.LastAt = now;
        if (tally.Points >= QuitAt)
            return rageQuit ? eReaction.RageQuit : eReaction.LastWarning;
        return tally.Points >= LastWarnAt ? eReaction.LastWarning
            : tally.Points >= WarnAt ? eReaction.Warning
            : eReaction.Grumble;
    }

    /// <summary>Notes an emote at her; true once there are more than the free ones within a minute. Pure.</summary>
    public static bool CountEmote(Tally tally, DateTime now)
    {
        tally.Emotes.RemoveAll(at => now - at > EmoteWindow);
        tally.Emotes.Add(now);
        if (tally.Emotes.Count > FreeEmotes + 3)
            tally.Emotes.RemoveAt(0);
        return tally.Emotes.Count > FreeEmotes;
    }

    /// <summary>True when the last summon was so recent that summoning again is a nuisance.</summary>
    public static bool IsQuickResummon(string lastSeenUtc, DateTime now) =>
        TryParse(lastSeenUtc, out DateTime last) && now - last < QuickResummonWindow;

    /// <summary>Time left on her sulk, or zero when she is not sulking.</summary>
    public static TimeSpan SulkLeft(string sulkUntilUtc, DateTime now) =>
        TryParse(sulkUntilUtc, out DateTime until) && until > now ? until - now : TimeSpan.Zero;

    private static readonly Dictionary<eIrritation, string[]> Grumbles = new()
    {
        [eIrritation.QuickResummon] = ["Again? I just got here.", "Make up your mind, {0}."],
        [eIrritation.Rude] = ["Charming, {0}.", "Noted. And remembered."],
        [eIrritation.EmoteSpam] = ["Stop that, {0}.", "Yes, yes, I see you. Stop."],
        [eIrritation.Death] = ["Dead again, {0}?", "I can only heal the living, {0}."],
    };

    private static readonly string[] Warnings =
    [
        "Careful, {0}. I'm this close to leaving.",
        "I'm getting annoyed, {0}.",
        "Keep this up and you'll be healing yourself.",
    ];

    private static readonly string[] LastWarnings =
    [
        "One more and I'm gone, {0}.",
        "Last warning, {0}. I mean it.",
    ];

    private static readonly string[] RageQuits =
    [
        "That's it, {0}. I'm out. Heal yourself!",
        "Enough! Find another healer, {0}.",
        "I quit. Don't call me, {0}. I'll call you.",
    ];

    private static readonly string[] SulkRefusals =
    [
        "Persoper is still sulking. Try again in {1}.",
        "\"Not talking to you, {0}.\" Persoper is sulking for another {1}.",
        "\"Hmph.\" Persoper won't come for another {1}.",
    ];

    public static string Line(eIrritation irritation, eReaction reaction, string playerName, Random random)
    {
        random ??= Random.Shared;
        string[] lines = reaction switch
        {
            eReaction.RageQuit => RageQuits,
            eReaction.LastWarning => LastWarnings,
            eReaction.Warning => Warnings,
            _ => Grumbles[irritation],
        };
        return string.Format(lines[random.Next(lines.Length)], playerName);
    }

    public static string SulkRefusal(string playerName, TimeSpan left, Random random)
    {
        random ??= Random.Shared;
        int minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        string time = minutes == 1 ? "a minute" : $"{minutes} minutes";
        return string.Format(SulkRefusals[random.Next(SulkRefusals.Length)], playerName, time);
    }

    private static bool TryParse(string utc, out DateTime value) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);

    // --- In the running server ---

    private static readonly object Sync = new();
    private static readonly Dictionary<string, Tally> Tallies = new();

    private static Tally TallyFor(GamePlayer player)
    {
        lock (Sync)
        {
            if (!Tallies.TryGetValue(player.InternalID, out Tally tally))
            {
                if (Tallies.Count >= MaxTrackedPlayers)
                {
                    // Bounded: drop the quietest entries.
                    foreach (string key in Tallies.OrderBy(entry => entry.Value.LastAt).Take(MaxTrackedPlayers / 5).Select(entry => entry.Key).ToList())
                        Tallies.Remove(key);
                }
                tally = new Tally();
                Tallies[player.InternalID] = tally;
            }
            return tally;
        }
    }

    /// <summary>An emote aimed at her; only spam counts as an irritation.</summary>
    public static void OnEmote(GamePlayer player, GameBot persoper)
    {
        if (!PersoperProperties.ANNOYANCE || player == null || persoper == null)
            return;
        bool spam;
        lock (Sync)
            spam = CountEmote(TallyFor(player), DateTime.UtcNow);
        if (spam)
            Irritate(player, persoper, eIrritation.EmoteSpam);
    }

    /// <summary>Counts one irritation from this player; she reacts, and may rage-quit.</summary>
    public static void Irritate(GamePlayer player, GameBot persoper, eIrritation irritation)
    {
        if (!PersoperProperties.ANNOYANCE || player == null || persoper == null ||
            persoper.ObjectState != GameObject.eObjectState.Active)
            return;

        DateTime now = DateTime.UtcNow;
        eReaction reaction;
        lock (Sync)
        {
            Tally tally = TallyFor(player);
            reaction = Add(tally, now, PersoperProperties.RAGE_QUIT);
            if (reaction == eReaction.RageQuit)
                tally.Points = 0;
        }

        persoper.Say(Line(irritation, reaction, player.Name, Random.Shared));
        if (reaction != eReaction.RageQuit)
            return;

        int minutes = Math.Clamp(PersoperProperties.SULK_MINUTES, 0, 100);
        if (minutes > 0)
        {
            PersoperMemoryRecord memory = PersoperMemoryStore.Load(player);
            if (memory != null)
            {
                memory.SulkUntilUtc = now.AddMinutes(minutes).ToString("O");
                PersoperMemoryStore.Save(memory);
            }
        }
        persoper.Delete();
    }
}
