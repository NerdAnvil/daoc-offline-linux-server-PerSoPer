using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.Events;

namespace DOL.GS;

/// <summary>
/// The chat rhythm of personality-type bots, separate from the plain bots' rare shared
/// slots. Every few seconds: per realm, one typed bot may speak in faction chat; per
/// region with a player near a typed bot, one may speak nearby. Who speaks is weighted
/// by each type's Chattiness. The volume setting persoper_chatter_percent (0 = off,
/// 50 = calm, 100 = lively, the default) scales the gaps. Rules are pure and unit tested.
/// </summary>
public static class PersonalityChatter
{
    private const int TickMilliseconds = 5_000;
    private const int MaxLocalSpeakersPerTick = 8;
    private const int MaxGroupSpeakersPerTick = 8;

    /// <summary>Seconds between faction lines per realm at a volume: 30-60 at 50, never under 10. Null = off.</summary>
    public static (double Min, double Max)? FactionGap(int volume) => Gap(volume, 30, 60);

    /// <summary>Seconds between nearby lines per region at a volume: 15-30 at 50, never under 10. Null = off.</summary>
    public static (double Min, double Max)? LocalGap(int volume) => Gap(volume, 15, 30);

    /// <summary>Seconds between group-chat conversations per group with a player: 45-90 at 50. Null = off.</summary>
    public static (double Min, double Max)? GroupGap(int volume) => Gap(volume, 45, 90);

    private static (double Min, double Max)? Gap(int volume, double min, double max)
    {
        volume = Math.Clamp(volume, 0, 100);
        if (volume == 0)
            return null;
        double scale = 50.0 / volume;
        return (Math.Max(10, min * scale), Math.Max(10, max * scale));
    }

    /// <summary>Picks a speaker, weighted by Chattiness (at least 1 each). Pure apart from the random.</summary>
    public static T PickWeighted<T>(IReadOnlyList<(T Item, int Weight)> candidates, Random random)
    {
        if (candidates == null || candidates.Count == 0)
            return default;
        random ??= Random.Shared;
        int total = candidates.Sum(c => Math.Max(1, c.Weight));
        int roll = random.Next(total);
        foreach ((T item, int weight) in candidates)
        {
            roll -= Math.Max(1, weight);
            if (roll < 0)
                return item;
        }
        return candidates[^1].Item;
    }

    // --- In the running server ---

    private static Timer _timer;
    private static readonly Dictionary<eRealm, DateTime> NextFaction = new();
    private static readonly Dictionary<ushort, DateTime> NextLocal = new();

    [GameServerStartedEvent]
    public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
    {
        _timer?.Dispose();
        // The timer only hands the work to the game loop; all chat happens there.
        _timer = new Timer(_ => NpcService.Instance.Post<object>(_ => Tick(), null), null, TickMilliseconds, TickMilliseconds);
    }

    [GameServerStoppedEvent]
    public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
    {
        _timer?.Dispose();
        _timer = null;
    }

    private static void Tick()
    {
        int volume = PersoperProperties.CHATTER_PERCENT;
        if (FactionGap(volume) is not { } factionGap || LocalGap(volume) is not { } localGap || GroupGap(volume) is not { } groupGap)
            return;
        DateTime now = DateTime.UtcNow;
        var typed = PersonalityTypes.Online().Where(pair => pair.Personality.Ambient is { Length: > 0 }).ToList();
        if (typed.Count == 0)
            return;

        // Faction chat: one line per realm when it's that realm's turn.
        foreach (IGrouping<eRealm, (GameBot Bot, BotPersonality Personality)> realm in typed.GroupBy(pair => pair.Bot.Realm))
        {
            if (NextFaction.TryGetValue(realm.Key, out DateTime next) && now < next)
                continue;
            if (TrySpeak(realm.ToList(), faction: true))
                NextFaction[realm.Key] = now.AddSeconds(Between(factionGap));
        }

        // Nearby: one line per region that has a player near a typed bot.
        int spoken = 0;
        foreach (IGrouping<ushort, (GameBot Bot, BotPersonality Personality)> region in typed.GroupBy(pair => pair.Bot.CurrentRegionID))
        {
            if (spoken >= MaxLocalSpeakersPerTick)
                break;
            if (NextLocal.TryGetValue(region.Key, out DateTime next) && now < next)
                continue;
            // Bots grouped with a player don't start nearby talk; they only answer.
            var nearPlayers = region.Where(pair => !AutonomousBotChatCoordinator.IsGroupedWithPlayer(pair.Bot) && ClientService.Instance.GetPlayersOfRegion(pair.Bot.CurrentRegion)
                .Any(player => player.ObjectState == GameObject.eObjectState.Active && pair.Bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE)))
                .ToList();
            if (nearPlayers.Count > 0 && TrySpeak(nearPlayers, faction: false))
            {
                NextLocal[region.Key] = now.AddSeconds(Between(localGap));
                spoken++;
            }
        }

        // Group chat: one conversation per group with a player, when it's that group's turn.
        int groupsSpoken = 0;
        foreach (IGrouping<Group, (GameBot Bot, BotPersonality Personality)> party in typed
                     .Where(pair => AutonomousBotChatCoordinator.IsGroupedWithPlayer(pair.Bot))
                     .GroupBy(pair => pair.Bot.Group))
        {
            if (groupsSpoken >= MaxGroupSpeakersPerTick)
                break;
            if (!AutonomousBotChatCoordinator.IsGroupTurn(party.Key) || !TrySpeak(party.ToList(), faction: false, group: true))
                continue;
            AutonomousBotChatCoordinator.MarkGroupTurn(party.Key, (int)(Between(groupGap) * 1000));
            groupsSpoken++;
        }
    }

    private static bool TrySpeak(List<(GameBot Bot, BotPersonality Personality)> candidates, bool faction, bool group = false)
    {
        // A few tries in case the first pick is busy fighting or casting.
        for (int attempt = 0; attempt < 3 && candidates.Count > 0; attempt++)
        {
            var weighted = candidates.Select(pair => (Item: pair, Weight: pair.Personality.Chattiness)).ToList();
            var (bot, personality) = PickWeighted(weighted, Random.Shared);
            string line = PersonalityReactions.AmbientLine(bot, personality, Random.Shared, out eChatTopic? topic);
            if (group ? AutonomousBotChatCoordinator.SpeakInGroup(bot, line, topic)
                    : AutonomousBotChatCoordinator.SpeakAsPersonality(bot, line, faction, topic))
                return true;
            candidates.Remove((bot, personality));
        }
        return false;
    }

    private static double Between((double Min, double Max) gap) => gap.Min + Random.Shared.NextDouble() * (gap.Max - gap.Min);
}
