using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>Reads the dialogue facts (see DialogueRule) for a bot talking to a player or another bot, from the game.</summary>
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

        return new DialogueFacts
        {
            BotId = personality?.TypeKey == null ? personality?.Key : bot.InternalID,
            PlayerId = playerId,
            Realm = bot.Realm,
            PlayerDead = !player.IsAlive,
            InCombat = player.InCombat,
            Health = player.HealthPercent,
            GroupSize = player.Group?.MemberCount ?? 1,
            DeathsToday = PlayerEventLog.DeathsToday(playerId, now),
            Mood = mood.ToString().ToLowerInvariant(),
            TimeOfDay = DialogueFacts.TimeOfDayFor(WorldMgr.GetCurrentGameTime()),
            Zone = player.CurrentZone?.Description,
            Recent = PlayerEventLog.Recent(playerId, now),
            Nearby = Nearby(bot, player),
            Likes = Likes(playerId),
            BotDoing = PersonalityStateAnswers.DoingOf(bot),
            BotGrouped = bot.Group?.MemberCount > 1,
        };
    }

    /// <summary>
    /// The facts for a bot answering another bot: what it sees itself (combat, group, zone, who is
    /// nearby, the speaker included). No player, so player facts (mood, health, deaths) stay unknown.
    /// </summary>
    public static DialogueFacts ForBot(GameBot bot)
    {
        if (bot == null)
            return null;
        BotPersonality personality = BotPersonalities.For(bot);
        return new DialogueFacts
        {
            BotId = PersonalityReactions.MemoryKey(bot, personality),
            Realm = bot.Realm,
            InCombat = bot.InCombat,
            GroupSize = bot.Group?.MemberCount ?? 1,
            TimeOfDay = DialogueFacts.TimeOfDayFor(WorldMgr.GetCurrentGameTime()),
            Zone = bot.CurrentZone?.Description,
            Nearby = Nearby(bot, null),
            BotDoing = PersonalityStateAnswers.DoingOf(bot),
            BotGrouped = bot.Group?.MemberCount > 1,
        };
    }

    /// <summary>Bot-to-bot facts from plain values (tests).</summary>
    public static DialogueFacts ForBot(string botId, bool inCombat, int groupSize, string timeOfDay, string zone, IReadOnlySet<string> nearby) =>
        new()
        {
            BotId = botId,
            InCombat = inCombat,
            GroupSize = groupSize,
            TimeOfDay = timeOfDay,
            Zone = zone,
            Nearby = nearby,
        };

    /// <summary>Names of players and NPCs near the bot, plus each named bot's personality key and character; not the bot or the player it talks to.</summary>
    private static HashSet<string> Nearby(GameBot bot, GamePlayer player)
    {
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
            if (CharacterOf(npc.Name) is { } character)
                nearby.Add(character);
        }
        return nearby;
    }

    /// <summary>
    /// A named bot's character by its file name ("shnacksize" for Shnacksizer), or null: what
    /// [if nearby shnacksize] looks for, so it holds in every realm.
    /// </summary>
    public static string CharacterOf(string name) =>
        NamedBotLineup.FindByName(name) is { } entry ? PersonalityLines.CharacterKey(entry) : null;

    /// <summary>The player's saved answers as words; "other" (an answer nothing matched) says nothing about them.</summary>
    private static HashSet<string> Likes(string playerId) =>
        PlayerAnswers.Cache.For(playerId).Values.Where(answer => answer != DialogueFile.OtherAnswer).ToHashSet(StringComparer.Ordinal);
}
