using System;
using System.Collections.Generic;

namespace DOL.GS;

/// <summary>
/// What happened to each player lately, for dialogue facts ("[if recent loot]", "[if deaths_today >= 3]").
/// Kept in memory only: it starts fresh when the server restarts. Pure apart from the hooks in GamePlayer.
/// </summary>
public static class PlayerEventLog
{
    public enum eEvent
    {
        Death,
        Kill,
        Loot,
        LevelUp,
    }

    /// <summary>How long an event counts as recent, and how many players are remembered at most.</summary>
    public static readonly TimeSpan RecentFor = TimeSpan.FromMinutes(5);
    public const int MaxPlayers = 5000;

    private sealed class Entry
    {
        public readonly DateTime[] Last = new DateTime[4];
        public DateTime DeathDay;
        public int DeathsThatDay;
    }

    private static readonly object Lock = new();
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    /// <summary>The dialogue fact name of an event ("level_up").</summary>
    public static string FactName(eEvent what) => what switch
    {
        eEvent.Death => "death",
        eEvent.Kill => "kill",
        eEvent.Loot => "loot",
        _ => "level_up",
    };

    public static void Note(GamePlayer player, eEvent what)
    {
        if (player?.InternalID != null)
            Note(player.InternalID, what, DateTime.Now);
    }

    /// <summary>Counts one event. <paramref name="now"/> is local time, so "today" is the player's day.</summary>
    public static void Note(string playerId, eEvent what, DateTime now)
    {
        lock (Lock)
        {
            if (!Entries.TryGetValue(playerId, out Entry entry))
            {
                // A full log is emptied rather than grown: forgetting only makes a bot less observant.
                if (Entries.Count >= MaxPlayers)
                    Entries.Clear();
                Entries[playerId] = entry = new Entry();
            }
            entry.Last[(int)what] = now;
            if (what == eEvent.Death)
            {
                if (entry.DeathDay != now.Date)
                    (entry.DeathDay, entry.DeathsThatDay) = (now.Date, 0);
                entry.DeathsThatDay++;
            }
        }
    }

    public static int DeathsToday(string playerId, DateTime now)
    {
        lock (Lock)
            return Entries.TryGetValue(playerId ?? string.Empty, out Entry entry) && entry.DeathDay == now.Date ? entry.DeathsThatDay : 0;
    }

    /// <summary>The events of the last <see cref="RecentFor"/>, as fact names.</summary>
    public static IReadOnlySet<string> Recent(string playerId, DateTime now)
    {
        var recent = new HashSet<string>();
        lock (Lock)
        {
            if (!Entries.TryGetValue(playerId ?? string.Empty, out Entry entry))
                return recent;
            foreach (eEvent what in Enum.GetValues<eEvent>())
                if (entry.Last[(int)what] != default && now - entry.Last[(int)what] <= RecentFor)
                    recent.Add(FactName(what));
        }
        return recent;
    }

    /// <summary>Forgets everything (tests).</summary>
    public static void Clear()
    {
        lock (Lock)
            Entries.Clear();
    }
}
