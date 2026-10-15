using System;
using System.Globalization;
using DOL.Database;
using DOL.Database.Attributes;

namespace DOL.GS;

/// <summary>
/// What Persoper remembers about one player character: when they met, how
/// often she was summoned, how they treat her, her last build with them and a
/// couple of short notes. One row per character, kept between sessions.
/// </summary>
[DataTable(TableName = "persoper_memory")]
public sealed class PersoperMemoryRecord : DataObject
{
    public const int MaxInsultLength = 60;

    /// <summary>The character's database id, so a rename keeps the memory.</summary>
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
}

public static class PersoperMemoryStore
{
    public static PersoperMemoryRecord Load(GamePlayer player)
    {
        if (string.IsNullOrEmpty(player?.InternalID))
            return null;
        return GameServer.Database.FindObjectByKey<PersoperMemoryRecord>(player.InternalID)
            ?? new PersoperMemoryRecord { PlayerId = player.InternalID, PlayerName = player.Name };
    }

    public static bool Save(PersoperMemoryRecord record)
    {
        if (record == null)
            return false;
        record.Dirty = true;
        return record.IsPersisted ? GameServer.Database.SaveObject(record) : GameServer.Database.AddObject(record);
    }
}

/// <summary>Pure rules for Persoper's memory: mood, remembered lines and her build. Unit tested.</summary>
public static class PersoperMemory
{
    public enum eMood
    {
        Neutral,
        Warm,
        Cool,
    }

    /// <summary>How far nice and rude messages must differ before her mood shows.</summary>
    public const int MoodMargin = 3;

    public static eMood MoodOf(PersoperMemoryRecord memory)
    {
        if (memory == null)
            return eMood.Neutral;
        int balance = memory.NiceCount - memory.RudeCount;
        return balance >= MoodMargin ? eMood.Warm : balance <= -MoodMargin ? eMood.Cool : eMood.Neutral;
    }

    public static bool IsNew(PersoperMemoryRecord memory) => memory == null || string.IsNullOrEmpty(memory.FirstMetUtc);

    /// <summary>Counts one chat message to her as nice, rude or neither.</summary>
    public static void RecordChat(PersoperMemoryRecord memory, eAutonomousChatIntent intent, string message)
    {
        if (memory == null)
            return;
        switch (intent)
        {
            case eAutonomousChatIntent.Abuse:
                memory.RudeCount++;
                // Braces are dropped so the quote can never break a format string.
                string insult = (message ?? string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Trim();
                memory.LastInsult = insult.Length > PersoperMemoryRecord.MaxInsultLength
                    ? insult[..PersoperMemoryRecord.MaxInsultLength]
                    : insult;
                break;
            case eAutonomousChatIntent.Greeting:
            case eAutonomousChatIntent.Farewell:
            case eAutonomousChatIntent.Banter:
                memory.NiceCount++;
                break;
        }
    }

    /// <summary>Updates the memory for a summon; call after the remembered line is chosen.</summary>
    public static void RecordSummon(PersoperMemoryRecord memory, string playerName, eSpecType spec, string zone, DateTime utcNow)
    {
        if (memory == null)
            return;
        string now = utcNow.ToString("O");
        if (IsNew(memory))
            memory.FirstMetUtc = now;
        memory.PlayerName = playerName ?? memory.PlayerName;
        memory.LastSeenUtc = now;
        memory.SummonCount++;
        memory.LastSpec = (int)spec;
        memory.LastZone = zone ?? string.Empty;
    }

    /// <summary>A line about your history with her, or null for a first meeting.</summary>
    public static string RememberedLine(PersoperMemoryRecord memory, string playerName, DateTime utcNow, Random random)
    {
        if (IsNew(memory))
            return null;
        random ??= Random.Shared;
        TimeSpan since = utcNow - Parse(memory.LastSeenUtc, utcNow);
        int times = memory.SummonCount + 1;
        string gap = since.TotalHours < 1 ? null
            : since.TotalDays < 1 ? "Back already, {0}?"
            : since.TotalDays < 7 ? $"Haven't seen you in {(int)since.TotalDays} day{Plural((int)since.TotalDays)}, {{0}}."
            : $"{(int)(since.TotalDays / 7)} week{Plural((int)(since.TotalDays / 7))}, {{0}}! I thought you'd found another healer.";
        string[] lines = MoodOf(memory) switch
        {
            eMood.Warm =>
            [
                gap ?? "Always happy to see you, {0}.",
                $"That's the {Ordinal(times)} time you've called me, {{0}}. I'm counting. Fondly.",
            ],
            eMood.Cool =>
            [
                gap is null ? "Oh. You again, {0}." : gap + " Not that I was counting.",
                string.IsNullOrEmpty(memory.LastInsult)
                    ? "I remember how you talk to me, {0}. Behave."
                    : $"Last time you said \"{memory.LastInsult}\". I remember, {{0}}.",
            ],
            _ =>
            [
                gap ?? "Back again, {0}.",
                $"{Capital(Ordinal(times))} summon, {{0}}. I'm keeping track.",
            ],
        };
        return string.Format(lines[random.Next(lines.Length)], playerName);
    }

    /// <summary>Her answer to "what do you remember?".</summary>
    public static string Recall(PersoperMemoryRecord memory, string playerName, DateTime utcNow)
    {
        if (IsNew(memory))
            return $"Remember you? We've only just met, {playerName}.";
        TimeSpan known = utcNow - Parse(memory.FirstMetUtc, utcNow);
        string spec = SpecPhrase((eSpecType)memory.LastSpec);
        string mood = MoodOf(memory) switch
        {
            eMood.Warm => "You're one of the nice ones.",
            eMood.Cool => $"You've been rude to me {memory.RudeCount} time{Plural(memory.RudeCount)}. I keep score.",
            _ => "You're alright, mostly.",
        };
        string where = string.IsNullOrEmpty(memory.LastZone) ? string.Empty : $" Last time we were in {memory.LastZone}.";
        string build = spec == null ? string.Empty : $" I went {spec}.";
        return $"We met {Age(known)} ago, {playerName}, and you've called me {memory.SummonCount} time{Plural(memory.SummonCount)}.{where}{build} {mood}";
    }

    /// <summary>Her build in a few plain words, or null when it is not a healer build she knows.</summary>
    public static string SpecPhrase(eSpecType spec) => spec switch
    {
        eSpecType.MendHealer or eSpecType.RejuvCleric or eSpecType.RegrowthDruid => "heal-heavy",
        eSpecType.AugHealer or eSpecType.EnhanceCleric or eSpecType.NurtureDruid => "buffs",
        eSpecType.PacHealer => "crowd control",
        eSpecType.SmiteCleric => "smiting",
        eSpecType.NatureDruid => "nature and pets",
        _ => null,
    };

    /// <summary>What she says about her build at summon, mentioning last time's when it differs.</summary>
    public static string SpecAnnouncement(eSpecType spec, eSpecType lastSpec, string playerName, Random random)
    {
        string phrase = SpecPhrase(spec);
        if (phrase == null)
            return null;
        random ??= Random.Shared;
        string last = SpecPhrase(lastSpec);
        if (last != null && last != phrase && random.Next(2) == 0)
            return $"{Capital(phrase)} this time. I was {last} last time and you still nearly died, {playerName}.";
        string[] lines = spec switch
        {
            eSpecType.PacHealer =>
            [
                "I'm feeling crowd control today. Things will sleep.",
                "Crowd control build. If it moves, it naps.",
            ],
            eSpecType.SmiteCleric =>
            [
                "Smiting today. Holy fire first, healing when I feel like it.",
                "I'm specced to smite. Point me at something.",
            ],
            eSpecType.NatureDruid =>
            [
                "Nature build today. I brought a friend with teeth.",
                "Nature and pets today. Be nice to my pet.",
            ],
            _ when phrase == "buffs" =>
            [
                "Buffs today. You'll feel fabulous, {0}.",
                "I'm a buff bot today. You're welcome in advance.",
            ],
            _ =>
            [
                "Heal-heavy today, so stay behind me, {0}.",
                "Full healer build. Try to need me less than last time.",
            ],
        };
        return string.Format(lines[random.Next(lines.Length)], playerName);
    }

    /// <summary>True when a message asks about her build.</summary>
    public static bool AsksSpec(string message) =>
        message != null && (message.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("build", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when a message asks what she remembers.</summary>
    public static bool AsksMemory(string message) =>
        message != null && (message.Contains("remember", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("know me", StringComparison.OrdinalIgnoreCase));

    private static DateTime Parse(string utc, DateTime fallback) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value) ? value : fallback;

    private static string Age(TimeSpan span) =>
        span.TotalHours < 1 ? "less than an hour"
        : span.TotalDays < 1 ? $"{(int)span.TotalHours} hour{Plural((int)span.TotalHours)}"
        : $"{(int)span.TotalDays} day{Plural((int)span.TotalDays)}";

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Ordinal(int n)
    {
        if (n is >= 1 and <= 10)
            return Words[n];
        string suffix = n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    private static readonly string[] Words = ["", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];
}
