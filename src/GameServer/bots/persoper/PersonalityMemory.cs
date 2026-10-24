using System;
using System.Globalization;
using System.Linq;
using DOL.Database;
using DOL.Database.Attributes;
using DOL.GS.PacketHandler;

namespace DOL.GS;

/// <summary>
/// What one personality bot remembers about one player character: when they met,
/// how often, how they treat it, its last build with them, a couple of short
/// notes and any sulk. One row per personality per character.
/// </summary>
[DataTable(TableName = "persoper_bot_memory")]
public sealed class PersonalityMemoryRecord : DataObject
{
    public const int MaxInsultLength = 60;

    /// <summary>"{personality}|{character id}", so a rename keeps the memory.</summary>
    [PrimaryKey] public string MemoryKey { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string Personality { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string PlayerId { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string PlayerName { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string FirstMetUtc { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string LastSeenUtc { get; set; } = string.Empty;
    /// <summary>Summons for Persoper, group joins for world bots.</summary>
    [DataElement(AllowDbNull = false)] public int MeetCount { get; set; }
    [DataElement(AllowDbNull = false)] public int NiceCount { get; set; }
    [DataElement(AllowDbNull = false)] public int RudeCount { get; set; }
    [DataElement(AllowDbNull = false)] public int LastSpec { get; set; } = (int)eSpecType.None;
    [DataElement(AllowDbNull = false)] public string LastZone { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string LastInsult { get; set; } = string.Empty;
    /// <summary>Until when it refuses this player after quitting on them (empty: not sulking).</summary>
    [DataElement(AllowDbNull = false)] public string SulkUntilUtc { get; set; } = string.Empty;

    public static string KeyFor(string personality, string playerId) => $"{personality}|{playerId}";
}

/// <summary>
/// Persoper's memories from before memory was shared (Stage 3). Read only, to move
/// them into persoper_bot_memory the first time she meets that player again.
/// </summary>
[DataTable(TableName = "persoper_memory")]
public sealed class PersoperMemoryRecord : DataObject
{
    [PrimaryKey] public string PlayerId { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string PlayerName { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string FirstMetUtc { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string LastSeenUtc { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public int SummonCount { get; set; }
    [DataElement(AllowDbNull = false)] public int NiceCount { get; set; }
    [DataElement(AllowDbNull = false)] public int RudeCount { get; set; }
    [DataElement(AllowDbNull = false)] public int LastSpec { get; set; } = (int)eSpecType.None;
    [DataElement(AllowDbNull = false)] public string LastZone { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string LastInsult { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string SulkUntilUtc { get; set; } = string.Empty;
}

public static class PersonalityMemoryStore
{
    public static PersonalityMemoryRecord Load(BotPersonality personality, GamePlayer player)
    {
        if (personality == null || string.IsNullOrEmpty(player?.InternalID))
            return null;
        string key = PersonalityMemoryRecord.KeyFor(personality.Key, player.InternalID);
        PersonalityMemoryRecord record = GameServer.Database.FindObjectByKey<PersonalityMemoryRecord>(key);
        if (record != null)
            return record;

        // First time since memory was shared: bring Persoper's old row across.
        if (personality == PersoperIdentity.Personality &&
            GameServer.Database.FindObjectByKey<PersoperMemoryRecord>(player.InternalID) is { } legacy)
        {
            record = PersonalityMemory.FromLegacy(legacy, personality.Key);
            Save(record);
            return record;
        }

        return new PersonalityMemoryRecord
        {
            MemoryKey = key,
            Personality = personality.Key,
            PlayerId = player.InternalID,
            PlayerName = player.Name,
        };
    }

    public static bool Save(PersonalityMemoryRecord record)
    {
        if (record == null)
            return false;
        record.Dirty = true;
        return record.IsPersisted ? GameServer.Database.SaveObject(record) : GameServer.Database.AddObject(record);
    }
}

/// <summary>Shared rules for what personality bots remember: mood, remembered lines, recall and builds. Pure; unit tested.</summary>
public static class PersonalityMemory
{
    public enum eMood
    {
        Neutral,
        Warm,
        Cool,
    }

    /// <summary>How far nice and rude messages must differ before the mood shows.</summary>
    public const int MoodMargin = 3;

    public static PersonalityMemoryRecord FromLegacy(PersoperMemoryRecord legacy, string personalityKey) => new()
    {
        MemoryKey = PersonalityMemoryRecord.KeyFor(personalityKey, legacy.PlayerId),
        Personality = personalityKey,
        PlayerId = legacy.PlayerId,
        PlayerName = legacy.PlayerName,
        FirstMetUtc = legacy.FirstMetUtc,
        LastSeenUtc = legacy.LastSeenUtc,
        MeetCount = legacy.SummonCount,
        NiceCount = legacy.NiceCount,
        RudeCount = legacy.RudeCount,
        LastSpec = legacy.LastSpec,
        LastZone = legacy.LastZone,
        LastInsult = legacy.LastInsult,
        SulkUntilUtc = legacy.SulkUntilUtc,
    };

    public static eMood MoodOf(PersonalityMemoryRecord memory)
    {
        if (memory == null)
            return eMood.Neutral;
        int balance = memory.NiceCount - memory.RudeCount;
        return balance >= MoodMargin ? eMood.Warm : balance <= -MoodMargin ? eMood.Cool : eMood.Neutral;
    }

    public static bool IsNew(PersonalityMemoryRecord memory) => memory == null || string.IsNullOrEmpty(memory.FirstMetUtc);

    /// <summary>Counts one chat message as nice, rude or neither.</summary>
    public static void RecordChat(PersonalityMemoryRecord memory, eAutonomousChatIntent intent, string message)
    {
        if (memory == null)
            return;
        switch (intent)
        {
            case eAutonomousChatIntent.Abuse:
                memory.RudeCount++;
                // Braces are dropped so the quote can never break a format string.
                string insult = (message ?? string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Trim();
                memory.LastInsult = insult.Length > PersonalityMemoryRecord.MaxInsultLength
                    ? insult[..PersonalityMemoryRecord.MaxInsultLength]
                    : insult;
                break;
            case eAutonomousChatIntent.Greeting:
            case eAutonomousChatIntent.Farewell:
            case eAutonomousChatIntent.Banter:
            case eAutonomousChatIntent.Thank:
                memory.NiceCount++;
                break;
        }
    }

    /// <summary>Updates the memory for a meeting (summon or group join); call after the remembered line is chosen.</summary>
    public static void RecordMeeting(PersonalityMemoryRecord memory, string playerName, eSpecType spec, string zone, DateTime utcNow)
    {
        if (memory == null)
            return;
        string now = utcNow.ToString("O");
        if (IsNew(memory))
            memory.FirstMetUtc = now;
        memory.PlayerName = playerName ?? memory.PlayerName;
        memory.LastSeenUtc = now;
        memory.MeetCount++;
        memory.LastSpec = (int)spec;
        memory.LastZone = zone ?? string.Empty;
    }

    /// <summary>A line about your history together, or null for a first meeting.</summary>
    public static string RememberedLine(BotPersonality personality, PersonalityMemoryRecord memory, string playerName, DateTime utcNow, Random random)
    {
        MemoryVoice voice = personality?.Memory;
        if (voice == null || IsNew(memory))
            return null;
        random ??= Random.Shared;
        string away = TimeAway(utcNow - Parse(memory.LastSeenUtc, utcNow));
        string[] templates = MoodOf(memory) switch
        {
            eMood.Warm => voice.Warm,
            eMood.Cool => voice.Cool,
            _ => voice.Neutral,
        };
        string[] usable = templates
            .Where(line => away != null || !line.Contains("{2}"))
            .Where(line => !string.IsNullOrEmpty(memory.LastInsult) || !line.Contains("{3}"))
            .ToArray();
        if (usable.Length == 0)
            return null;
        return string.Format(usable[random.Next(usable.Length)], playerName, Ordinal(memory.MeetCount + 1), away, memory.LastInsult);
    }

    /// <summary>Its answer to "what do you remember?".</summary>
    public static string Recall(BotPersonality personality, PersonalityMemoryRecord memory, string playerName, DateTime utcNow)
    {
        MemoryVoice voice = personality?.Memory;
        if (voice == null)
            return null;
        if (IsNew(memory))
            return string.Format(voice.JustMet, playerName);
        TimeSpan known = utcNow - Parse(memory.FirstMetUtc, utcNow);
        string build = personality.Build?.Phrase((eSpecType)memory.LastSpec);
        string mood = MoodOf(memory) switch
        {
            eMood.Warm => voice.WarmMood,
            eMood.Cool => string.Format(voice.CoolMood, Times(memory.RudeCount)),
            _ => voice.NeutralMood,
        };
        string where = string.IsNullOrEmpty(memory.LastZone) ? string.Empty : string.Format(voice.RecallWhere, memory.LastZone);
        string buildText = build == null ? string.Empty : string.Format(voice.RecallBuild, build);
        return string.Format(voice.Recall, Age(known), playerName, Times(memory.MeetCount), where, buildText, mood);
    }

    /// <summary>What it says about its build, sometimes comparing it with last time's.</summary>
    public static string SpecAnnouncement(BotPersonality personality, eSpecType spec, eSpecType lastSpec, string playerName, Random random)
    {
        BuildVoice voice = personality?.Build;
        string phrase = voice?.Phrase(spec);
        if (phrase == null)
            return null;
        random ??= Random.Shared;
        string last = voice.Phrase(lastSpec);
        if (last != null && last != phrase && random.Next(2) == 0)
            return string.Format(voice.ComparedToLast, Capital(phrase), last, playerName);
        string[] lines = voice.Lines(spec);
        return lines is { Length: > 0 } ? string.Format(lines[random.Next(lines.Length)], playerName) : null;
    }

    /// <summary>A reply, sometimes coloured by its mood toward this player.</summary>
    public static string Tint(BotPersonality personality, PersonalityMemoryRecord memory, string reply, Random random)
    {
        MemoryVoice voice = personality?.Memory;
        if (voice == null || (random ?? Random.Shared).Next(3) != 0)
            return reply;
        return MoodOf(memory) switch
        {
            eMood.Cool => voice.CoolTint + " " + reply,
            eMood.Warm => reply + " " + voice.WarmTint,
            _ => reply,
        };
    }

    /// <summary>True when a message asks about its build.</summary>
    public static bool AsksSpec(string message) =>
        message != null && (message.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("build", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when a message asks what it remembers.</summary>
    public static bool AsksMemory(string message) =>
        message != null && (message.Contains("remember", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("know me", StringComparison.OrdinalIgnoreCase));

    /// <summary>"3 days", "2 weeks", "a few hours", or null when it was less than an hour.</summary>
    public static string TimeAway(TimeSpan span) =>
        span.TotalHours < 1 ? null
        : span.TotalDays < 1 ? "a few hours"
        : span.TotalDays < 7 ? Count((int)span.TotalDays, "day")
        : Count((int)(span.TotalDays / 7), "week");

    private static DateTime Parse(string utc, DateTime fallback) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value) ? value : fallback;

    private static string Age(TimeSpan span) =>
        span.TotalHours < 1 ? "less than an hour"
        : span.TotalDays < 1 ? Count((int)span.TotalHours, "hour")
        : Count((int)span.TotalDays, "day");

    private static string Times(int count) => Count(count, "time");

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static readonly string[] Words = ["", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];

    private static string Ordinal(int n)
    {
        if (n is >= 1 and <= 10)
            return Words[n];
        string suffix = n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }
}

/// <summary>What a personality bot says and remembers when it meets a player (summon or group join).</summary>
public static class PersonalityMeetings
{
    /// <summary>
    /// Says its remembered line and build line, then records the meeting. A caller's plain
    /// "welcome back" (greeting) is said only when there's no remembered line, so it isn't
    /// greeted twice. Lines go to group chat when it's in a group with a player. Returns
    /// true when this was a quick re-call, which the caller counts as an irritation once
    /// its own messages are out (it may make the bot quit on the spot).
    /// </summary>
    public static bool Greet(GameBot bot, GamePlayer player, Random random, string greeting = null)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        if (personality?.Memory == null || player == null)
        {
            Speak(bot, greeting);
            return false;
        }
        random ??= Random.Shared;
        DateTime now = DateTime.UtcNow;
        PersonalityMemoryRecord memory = PersonalityMemoryStore.Load(personality, player);
        bool quickRecall = PersonalityAnnoyance.IsQuickRecall(memory?.LastSeenUtc, now);

        Speak(bot, PersonalityMemory.RememberedLine(personality, memory, player.Name, now, random) ?? greeting);
        eSpecType spec = bot.BotSpec?.SpecType ?? eSpecType.None;
        string specLine = PersonalityMemory.SpecAnnouncement(personality, spec, (eSpecType)(memory?.LastSpec ?? (int)eSpecType.None), player.Name, random);
        Speak(bot, specLine);

        PersonalityMemory.RecordMeeting(memory, player.Name, spec, player.CurrentZone?.Description, now);
        PersonalityMemoryStore.Save(memory);
        return quickRecall;
    }

    /// <summary>To group chat when it's grouped with a player, else nearby. Nothing for an empty line.</summary>
    public static void Speak(GameBot bot, string line)
    {
        if (bot == null || string.IsNullOrWhiteSpace(line))
            return;
        if (AutonomousBotChatCoordinator.IsGroupedWithPlayer(bot))
            bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
        else
            bot.Say(line);
    }
}
