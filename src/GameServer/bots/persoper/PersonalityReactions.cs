using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Events;

namespace DOL.GS;

/// <summary>What a typed bot's line can be filled with. Empty or 0 means "not known": lines that need it are skipped.</summary>
public sealed record LineSlots(string Zone, string Monster, int Level, string ClassName, string Item);

/// <summary>The events a personality-type bot reacts to.</summary>
public enum ePersonalityEvent
{
    Ding,       // it levelled up
    Grats,      // someone near it or in its group levelled up
    OwnDeath,   // it died
    OtherDeath, // someone near it or in its group died
    Drop,       // it looted an upgrade
}

/// <summary>
/// Puts the personality types' conversation content (PersonalityLines) into chat: topic
/// openers for PersonalityChatter, topic replies for bots that answer, and reactions to
/// dings, deaths and good drops. Rules are pure and unit tested; the running-server part
/// below only finds who reacts and when.
/// </summary>
public static class PersonalityReactions
{
    /// <summary>Tries to find a topic line whose slots can all be filled.</summary>
    public const int MaxFillTries = 8;
    /// <summary>Share of a typed bot's ambient lines taken from the topic pools (the rest: its short Ambient list).</summary>
    public const double TopicShare = 0.75;
    /// <summary>Chance an opener is a cheeky line instead (Joker, Leeroy; only with tone-down off).</summary>
    public const double CheekyChance = 0.12;
    /// <summary>Most bots that react to one event (not counting the bot it happened to).</summary>
    public const int MaxReactors = 2;

    /// <summary>The conversation content of a personality-type bot (a named bot's own mixed in), or null for others.</summary>
    public static TypeLines LinesFor(BotPersonality personality) =>
        personality?.TypeKey == null ? null
            : PersonalityLines.Named.GetValueOrDefault(personality.Key) ?? PersonalityLines.All.GetValueOrDefault(personality.TypeKey);

    /// <summary>
    /// Fills {0} (the other one's name) and the slots. Null when the line needs a slot that
    /// is not known, so the caller picks another line.
    /// </summary>
    public static string Fill(string line, LineSlots slots, string otherName = null)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        slots ??= new LineSlots(null, null, 0, null, null);
        string level = slots.Level > 0 ? slots.Level.ToString() : null;
        foreach ((string token, string value) in new[]
        {
            ("{zone}", slots.Zone), ("{monster}", slots.Monster), ("{level}", level),
            ("{class}", slots.ClassName), ("{item}", slots.Item),
        })
        {
            if (!line.Contains(token, StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                return null;
            line = line.Replace(token, value.Trim(), StringComparison.Ordinal);
        }
        return line.Replace("{0}", string.IsNullOrWhiteSpace(otherName) ? "friend" : otherName, StringComparison.Ordinal);
    }

    /// <summary>A random line from the pool whose slots can be filled, or null.</summary>
    public static string PickFilled(IReadOnlyList<string> pool, LineSlots slots, string otherName, Random random)
    {
        if (pool == null || pool.Count == 0)
            return null;
        random ??= Random.Shared;
        for (int attempt = 0; attempt < MaxFillTries; attempt++)
        {
            if (Fill(pool[random.Next(pool.Count)], slots, otherName) is { } line)
                return line;
        }
        return null;
    }

    /// <summary>
    /// A conversation starter: a random topic's opener, or now and then a cheeky line when
    /// tone-down is off. Topic is null for a cheeky line. Null when nothing could be filled.
    /// </summary>
    public static string Opener(TypeLines lines, LineSlots slots, bool toneDown, Random random, out eChatTopic? topic)
    {
        topic = null;
        if (lines == null)
            return null;
        random ??= Random.Shared;
        if (!toneDown && lines.Cheeky is { Length: > 0 } cheeky && random.NextDouble() < CheekyChance)
            return cheeky[random.Next(cheeky.Length)];
        eChatTopic[] topics = lines.Openers.Where(pair => pair.Value is { Length: > 0 }).Select(pair => pair.Key).ToArray();
        for (int attempt = 0; attempt < MaxFillTries && topics.Length > 0; attempt++)
        {
            eChatTopic pick = topics[random.Next(topics.Length)];
            if (Fill(lines.Openers[pick][random.Next(lines.Openers[pick].Length)], slots) is { } line)
            {
                topic = pick;
                return line;
            }
        }
        return null;
    }

    /// <summary>An answer on the topic someone just opened; {0} is the one who opened it. Null: none.</summary>
    public static string TopicReply(TypeLines lines, eChatTopic topic, string otherName, LineSlots slots, Random random) =>
        lines?.TopicReplies.GetValueOrDefault(topic) is { } pool ? PickFilled(pool, slots, otherName, random) : null;

    /// <summary>What it says about an event; {0} is the one it happened to (Grats, OtherDeath). Null: none.</summary>
    public static string Reaction(TypeLines lines, ePersonalityEvent kind, string otherName, LineSlots slots, Random random)
    {
        if (lines == null)
            return null;
        string[] pool = kind switch
        {
            ePersonalityEvent.Ding => lines.Ding,
            ePersonalityEvent.Grats => lines.Grats,
            ePersonalityEvent.OwnDeath => lines.OwnDeath,
            ePersonalityEvent.OtherDeath => lines.OtherDeath,
            ePersonalityEvent.Drop => lines.Drop,
            _ => null,
        };
        return PickFilled(pool, slots, otherName, random);
    }

    /// <summary>
    /// Chance (0-1) it speaks up about an event: higher for its own events, scaled by its
    /// Chattiness and the persoper_chatter_percent volume. 0 when the volume is 0.
    /// </summary>
    public static double ReactChance(int chattiness, int volume, bool ownEvent)
    {
        volume = Math.Clamp(volume, 0, 100);
        double talk = Math.Clamp(chattiness, 1, 100) / 100.0;
        double chance = ownEvent ? 0.4 + 0.6 * talk : 0.15 + 0.6 * talk;
        return Math.Clamp(chance * volume / 100.0, 0, 1);
    }

    // --- In the running server ---

    private const string LastLootKey = "persoper.lastLoot";
    private const string NextDropLineKey = "persoper.nextDropLine";
    private const int DropLineCooldownMs = 60_000;
    private const int RegionCooldownMs = 6_000;
    private const int FactionDingCooldownMs = 60_000;
    private const double FactionDingChance = 0.3;
    /// <summary>Most typed bots looked at as possible reactors per event.</summary>
    private const int MaxCandidates = 24;

    private static readonly object Sync = new();
    private static readonly Dictionary<ushort, long> NextRegionReaction = new();
    private static readonly Dictionary<eRealm, long> NextFactionDing = new();

    /// <summary>A typed bot's ambient line: from its topic pools mostly, else its short Ambient list.</summary>
    public static string AmbientLine(GameBot bot, BotPersonality personality, Random random, out eChatTopic? topic)
    {
        topic = null;
        random ??= Random.Shared;
        if (LinesFor(personality) is { } lines && random.NextDouble() < TopicShare &&
            Opener(lines, SlotsFor(bot, NextLevel(bot)), PersoperProperties.TONE_DOWN, random, out topic) is { } opener)
            return opener;
        topic = null;
        return personality?.Ambient is { Length: > 0 } ambient ? ambient[random.Next(ambient.Length)] : null;
    }

    /// <summary>A typed bot's answer to a topic another bot opened, or null (then it answers as usual).</summary>
    public static string TopicReplyFor(GameBot responder, BotPersonality personality, eChatTopic topic, string starterName) =>
        TopicReply(LinesFor(personality), topic, starterName, SlotsFor(responder, NextLevel(responder)), Random.Shared);

    public static LineSlots SlotsFor(GameBot bot, int level, string item = null) => new(
        bot?.CurrentZone?.Description,
        bot?.PersistentRecord?.TargetName,
        level,
        bot?.ClassName,
        item ?? bot?.TempProperties.GetProperty<string>(LastLootKey));

    private static int NextLevel(GameBot bot) => bot != null && bot.Level < bot.MaxLevel ? bot.Level + 1 : 0;

    [GameServerStartedEvent]
    public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
    {
        GameEventMgr.AddHandlerUnique(GamePlayerEvent.LevelUp, OnPlayerLevelUp);
        GameEventMgr.AddHandlerUnique(GameLivingEvent.Dying, OnDying);
    }

    [GameServerStoppedEvent]
    public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
    {
        GameEventMgr.RemoveHandler(GamePlayerEvent.LevelUp, OnPlayerLevelUp);
        GameEventMgr.RemoveHandler(GameLivingEvent.Dying, OnDying);
    }

    /// <summary>Called by GameBot when it levels up.</summary>
    public static void OnBotLevelUp(GameBot bot)
    {
        if (bot == null || !bot.IsAutonomousWorldBot || !TakeRegionTurn(bot.CurrentRegionID))
            return;
        BotPersonality personality = PersonalityTypes.InstanceFor(bot);
        if (personality != null && Rolls(personality, ownEvent: true) &&
            Reaction(LinesFor(personality), ePersonalityEvent.Ding, null, SlotsFor(bot, bot.Level), Random.Shared) is { } ding)
        {
            // Mostly to the group or nearby; now and then to the whole realm, and a realm-mate cheers.
            if (TakeFactionDingTurn(bot.Realm))
            {
                Later(bot, 800, () => AutonomousBotChatCoordinator.SpeakFactionReaction(bot, ding));
                FactionGrats(bot);
                return;
            }
            Later(bot, 800, () => AutonomousBotChatCoordinator.SpeakReaction(bot, ding));
        }
        Witnesses(bot, ePersonalityEvent.Grats);
    }

    /// <summary>Called by GameBot when it picks up loot; equipped = it was an upgrade it now wears.</summary>
    public static void OnBotLoot(GameBot bot, string itemName, bool equipped)
    {
        if (bot == null || string.IsNullOrWhiteSpace(itemName))
            return;
        bot.TempProperties.SetProperty(LastLootKey, itemName);
        if (!equipped || PersonalityTypes.InstanceFor(bot) is not { } personality)
            return;
        long now = GameLoop.GameLoopTime;
        if (bot.TempProperties.GetProperty<long>(NextDropLineKey) > now || !Rolls(personality, ownEvent: true))
            return;
        bot.TempProperties.SetProperty(NextDropLineKey, now + DropLineCooldownMs);
        if (Reaction(LinesFor(personality), ePersonalityEvent.Drop, null, SlotsFor(bot, NextLevel(bot), itemName), Random.Shared) is { } line)
            Later(bot, 1_200 + Random.Shared.Next(1_000), () => AutonomousBotChatCoordinator.SpeakReaction(bot, line));
    }

    private static void OnPlayerLevelUp(DOLEvent e, object sender, EventArgs args)
    {
        if (sender is GamePlayer player && TakeRegionTurn(player.CurrentRegionID))
            Witnesses(player, ePersonalityEvent.Grats);
    }

    private static void OnDying(DOLEvent e, object sender, EventArgs args)
    {
        // Every mob's death passes here: leave at once unless a player or a world bot died.
        if (sender is not GamePlayer && sender is not GameBot { IsAutonomousWorldBot: true })
            return;
        var victim = (GameLiving)sender;
        if (!TakeRegionTurn(victim.CurrentRegionID))
            return;
        if (victim is GameBot bot && PersonalityTypes.InstanceFor(bot) is { } personality && Rolls(personality, ownEvent: true) &&
            Reaction(LinesFor(personality), ePersonalityEvent.OwnDeath, null, SlotsFor(bot, NextLevel(bot)), Random.Shared) is { } line)
            Later(bot, 1_000 + Random.Shared.Next(800), () => AutonomousBotChatCoordinator.SpeakReaction(bot, line, allowDead: true));
        Witnesses(victim, ePersonalityEvent.OtherDeath);
    }

    /// <summary>Up to MaxReactors typed bots in the subject's group or near it react, each by its own chance.</summary>
    private static void Witnesses(GameLiving subject, ePersonalityEvent kind)
    {
        if (PersoperProperties.CHATTER_PERCENT <= 0 || subject?.CurrentRegion == null)
            return;
        var candidates = PersonalityTypes.Online()
            .Where(pair => pair.Bot != subject && pair.Bot.Realm == subject.Realm && pair.Bot.IsAlive &&
                (pair.Bot.Group != null && pair.Bot.Group == subject.Group ||
                 pair.Bot.CurrentRegion == subject.CurrentRegion && pair.Bot.IsWithinRadius(subject, WorldMgr.SAY_DISTANCE)))
            .Take(MaxCandidates)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        int reacted = 0;
        foreach ((GameBot bot, BotPersonality personality) in candidates)
        {
            if (reacted >= MaxReactors)
                break;
            if (!Rolls(personality, ownEvent: false) ||
                Reaction(LinesFor(personality), kind, subject.Name, SlotsFor(bot, NextLevel(bot)), Random.Shared) is not { } line)
                continue;
            Later(bot, 2_000 + reacted * 1_800 + Random.Shared.Next(1_200), () => AutonomousBotChatCoordinator.SpeakReaction(bot, line));
            reacted++;
        }
    }

    /// <summary>One typed realm-mate (anywhere) cheers a ding in faction chat.</summary>
    private static void FactionGrats(GameBot dinger)
    {
        var mates = PersonalityTypes.Online().Where(pair => pair.Bot != dinger && pair.Bot.Realm == dinger.Realm && pair.Bot.IsAlive)
            .Take(MaxCandidates).ToList();
        if (mates.Count == 0)
            return;
        var (bot, personality) = PersonalityChatter.PickWeighted(mates.Select(pair => (Item: pair, Weight: pair.Personality.Chattiness)).ToList(), Random.Shared);
        if (Rolls(personality, ownEvent: false) &&
            Reaction(LinesFor(personality), ePersonalityEvent.Grats, dinger.Name, SlotsFor(bot, NextLevel(bot)), Random.Shared) is { } line)
            Later(bot, 3_000 + Random.Shared.Next(2_000), () => AutonomousBotChatCoordinator.SpeakFactionReaction(bot, line));
    }

    private static bool Rolls(BotPersonality personality, bool ownEvent) =>
        Random.Shared.NextDouble() < ReactChance(personality.Chattiness, PersoperProperties.CHATTER_PERCENT, ownEvent);

    /// <summary>One event burst per region every few seconds, so a wipe doesn't flood chat.</summary>
    private static bool TakeRegionTurn(ushort regionId)
    {
        if (PersoperProperties.CHATTER_PERCENT <= 0)
            return false;
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            if (NextRegionReaction.TryGetValue(regionId, out long next) && now < next)
                return false;
            NextRegionReaction[regionId] = now + RegionCooldownMs;
            return true;
        }
    }

    private static bool TakeFactionDingTurn(eRealm realm)
    {
        if (Random.Shared.NextDouble() >= FactionDingChance)
            return false;
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            if (NextFactionDing.TryGetValue(realm, out long next) && now < next)
                return false;
            NextFactionDing[realm] = now + FactionDingCooldownMs;
            return true;
        }
    }

    private static void Later(GameBot bot, int delay, Action action)
    {
        _ = new ECSGameTimer(bot, _ =>
        {
            if (bot.ObjectState == GameObject.eObjectState.Active)
                action();
            return 0;
        }, delay);
    }
}
