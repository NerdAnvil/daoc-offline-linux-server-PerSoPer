using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DOL.Database;
using DOL.Database.Attributes;

namespace DOL.GS;

/// <summary>
/// Which personality type a generated world bot has, or "plain" for none. Our own table; the
/// roster table is untouched.
/// </summary>
[DataTable(TableName = "persoper_bot_type")]
public sealed class PersonalityTypeRecord : DataObject
{
    [PrimaryKey] public long BotId { get; set; }
    [DataElement(AllowDbNull = false)] public string TypeKey { get; set; } = string.Empty;
}

/// <summary>
/// Personality types (Hardcore, Casual, Min/maxer, Joker, Loner, Leeroy) for any number of
/// generated world bots. Each typed bot gets its own copy of the type's personality, named
/// after the bot and keyed "joker#123", so its memories and patience are its own.
/// A world bot without a saved type is rolled once, the first time it's looked at: about half
/// get a random type, the rest are saved as "plain". That covers existing, admin-made and
/// population-made bots alike; named personalities (Persoper, Kobaldjovi) are never rolled.
/// </summary>
public static partial class PersonalityTypes
{
    public const string Mixed = "mixed";

    /// <summary>Saved for a world bot that was rolled and stays without a type.</summary>
    public const string Plain = "plain";

    /// <summary>Share of world bots that get a type when rolled (the 50/50 split).</summary>
    public const int TypedSharePercent = 50;

    /// <summary>New rolls per second at most, so a big roster's first look doesn't stall a tick.</summary>
    private const int MaxRollsPerSecond = 20;

    /// <summary>Every type, by key.</summary>
    public static IReadOnlyDictionary<string, BotPersonality> Templates => PersonalityTypeVoices.All;

    public static IReadOnlyList<string> Keys => Templates.Keys.OrderBy(key => key).ToArray();

    /// <summary>The memory key of one typed bot: "joker#123".</summary>
    public static string InstanceKey(string typeKey, long botId) => $"{typeKey}#{botId}";

    /// <summary>The type key for what an admin typed ("Min/Maxer", "minmaxer" -> "minmaxer"), or null.</summary>
    public static string Normalise(string typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
            return null;
        string key = NonLetters().Replace(typed.Trim().ToLowerInvariant(), string.Empty);
        return key == Mixed || Templates.ContainsKey(key) ? key : null;
    }

    /// <summary>The type to give the next bot: the one asked for, or a random one for "mixed".</summary>
    public static string Pick(string normalised, Random random)
    {
        if (normalised != Mixed)
            return normalised;
        random ??= Random.Shared;
        IReadOnlyList<string> keys = Keys;
        return keys[random.Next(keys.Count)];
    }

    /// <summary>The roll for a world bot with no saved type: a random type, or <see cref="Plain"/>.</summary>
    public static string Roll(Random random)
    {
        random ??= Random.Shared;
        return random.Next(100) < TypedSharePercent ? Pick(Mixed, random) : Plain;
    }

    [GeneratedRegex("[^a-z]")]
    private static partial Regex NonLetters();

    // --- In the running server ---

    private static readonly object Sync = new();
    private static ConcurrentDictionary<long, string> _types;
    private static readonly ConcurrentDictionary<long, BotPersonality> Instances = new();

    private static ConcurrentDictionary<long, string> Types
    {
        get
        {
            if (_types != null)
                return _types;
            lock (Sync)
            {
                _types ??= new ConcurrentDictionary<long, string>(
                    DOLDB<PersonalityTypeRecord>.SelectAllObjects().ToDictionary(row => row.BotId, row => row.TypeKey));
                return _types;
            }
        }
    }

    public static string TypeOf(long botId) => Types.GetValueOrDefault(botId);

    /// <summary>Saves a bot's type (admin create).</summary>
    public static bool Assign(long botId, string typeKey)
    {
        if (botId <= 0 || !Templates.ContainsKey(typeKey ?? string.Empty))
            return false;
        var row = GameServer.Database.FindObjectByKey<PersonalityTypeRecord>(botId) ?? new PersonalityTypeRecord { BotId = botId };
        row.TypeKey = typeKey;
        row.Dirty = true;
        bool saved = row.IsPersisted ? GameServer.Database.SaveObject(row) : GameServer.Database.AddObject(row);
        if (saved)
        {
            Types[botId] = typeKey;
            Instances.TryRemove(botId, out _);
        }
        return saved;
    }

    /// <summary>This bot's own copy of its type's personality, or null when it has no type.</summary>
    public static BotPersonality InstanceFor(GameBot bot)
    {
        long botId = bot?.DatabaseID ?? 0;
        if (botId <= 0 || (TypeOf(botId) ?? RollOnce(bot, botId)) is not { } typeKey ||
            !Templates.TryGetValue(typeKey, out BotPersonality template))
            return null;
        return Instances.GetOrAdd(botId, _ => template with
        {
            Name = bot.Name,
            Key = InstanceKey(typeKey, botId),
            Matches = other => other?.DatabaseID == botId,
            TypeKey = typeKey,
        });
    }

    private static long _rollWindowStart;
    private static int _rollsInWindow;

    /// <summary>
    /// Rolls and saves the type of a world bot seen for the first time. Null when the bot can't be
    /// rolled (not a world bot, a named personality) or this second's roll budget is spent; it's
    /// then simply rolled on a later look.
    /// </summary>
    private static string RollOnce(GameBot bot, long botId)
    {
        if (!bot.IsAutonomousWorldBot || BotPersonalities.All.Any(named => named.Matches(bot)))
            return null;
        string rolled;
        lock (Sync)
        {
            if (Types.TryGetValue(botId, out string known))
                return known;
            long now = Environment.TickCount64;
            if (now - _rollWindowStart >= 1000)
            {
                _rollWindowStart = now;
                _rollsInWindow = 0;
            }
            if (_rollsInWindow >= MaxRollsPerSecond)
                return null;
            _rollsInWindow++;
            rolled = Roll(Random.Shared);
            Types[botId] = rolled;
        }
        // Saved outside Sync: admin create takes the write lock first, then Sync (via Assign).
        // If saving fails the roll still holds until the next restart, then it's rolled again.
        lock (AutonomousBotStatusPersistence.DatabaseWriteLock)
        {
            if (GameServer.Database.FindObjectByKey<PersonalityTypeRecord>(botId) == null)
                GameServer.Database.AddObject(new PersonalityTypeRecord { BotId = botId, TypeKey = rolled });
        }
        return rolled;
    }

    /// <summary>Typed bots in the world right now.</summary>
    public static IEnumerable<(GameBot Bot, BotPersonality Personality)> Online() =>
        AutonomousBotRegistry.Snapshot()
            .Select(bot => (Bot: bot, Personality: bot.IsAutonomousWorldBot ? InstanceFor(bot) : null))
            .Where(pair => pair.Personality != null);
}
