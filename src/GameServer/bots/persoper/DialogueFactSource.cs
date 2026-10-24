using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>Reads the dialogue facts (see DialogueRule) for a bot talking to a player, from the game.</summary>
public static class DialogueFactSource
{
    /// <summary>How far "nearby" reaches, and how many names it collects at most.</summary>
    public const ushort NearbyRadius = 1500;
    public const int MaxNearby = 50;

    public static DialogueFacts For(GameBot bot, GamePlayer player)
    {
        if (bot == null || player == null)
            return null;
        BotPersonality personality = BotPersonalities.For(bot);
        DateTime now = DateTime.Now;
        string playerId = player.InternalID;
        PersonalityMemory.eMood mood = personality?.Memory == null
            ? PersonalityMemory.eMood.Neutral
            : PersonalityMemory.MoodOf(PersonalityMemoryStore.Load(personality, player));

        var nearby = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GamePlayer other in bot.GetPlayersInRadius(NearbyRadius).Take(MaxNearby))
            if (other != player)
                nearby.Add(other.Name);
        foreach (GameNPC npc in bot.GetNPCsInRadius(NearbyRadius).Take(MaxNearby))
        {
            if (npc == bot)
                continue;
            nearby.Add(npc.Name);
            if (npc is GameBot other && BotPersonalities.For(other) is { } named)
                nearby.Add(named.Key);
        }

        return new DialogueFacts
        {
            BotId = personality?.TypeKey == null ? personality?.Key : bot.InternalID,
            PlayerId = playerId,
            PlayerDead = !player.IsAlive,
            InCombat = player.InCombat,
            Health = player.HealthPercent,
            GroupSize = player.Group?.MemberCount ?? 1,
            DeathsToday = PlayerEventLog.DeathsToday(playerId, now),
            Mood = mood.ToString().ToLowerInvariant(),
            TimeOfDay = DialogueFacts.TimeOfDayFor(WorldMgr.GetCurrentGameTime()),
            Zone = player.CurrentZone?.Description,
            Recent = PlayerEventLog.Recent(playerId, now),
            Nearby = nearby,
        };
    }
}
