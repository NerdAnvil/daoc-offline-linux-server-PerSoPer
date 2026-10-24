using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.Logging;

namespace DOL.GS;

/// <summary>What a typed bot can bring up on its own.</summary>
public enum eChatTopic
{
    Xp,
    Gear,
    Zones,
    Classes,
    Dungeons,
    RvR,
    Trading,
    Realm,     // towns, NPCs, lore
    Gripes,
    Fun,       // jokes, banter
    Life,      // food, weather, travel, sleep
    Questions, // asking others something
}

/// <summary>
/// One personality type's conversation content. Placeholders:
/// {0} = the other bot (TopicReplies, Grats, OtherDeath only);
/// optional slots {zone}, {monster}, {level}, {class}, {item}.
/// </summary>
public sealed class TypeLines
{
    public Dictionary<eChatTopic, string[]> Openers { get; init; } = new();
    public Dictionary<eChatTopic, string[]> TopicReplies { get; init; } = new();
    public string[] Ding { get; init; } = [];
    public string[] Grats { get; init; } = [];
    public string[] OwnDeath { get; init; } = [];
    public string[] OtherDeath { get; init; } = [];
    public string[] Drop { get; init; } = [];

    /// <summary>Mild cheeky lines, only when persoper_tone_down is off. Null for most types.</summary>
    public string[] Cheeky { get; init; }
}

/// <summary>
/// What a personality type says outside the topic pools: its short ambient list and its
/// chat replies by intent (see BotPersonality).
/// </summary>
public sealed record TypeTalk(
    string[] Ambient,
    IReadOnlyDictionary<eAutonomousChatIntent, string[]> Replies,
    IReadOnlyDictionary<eAutonomousChatIntent, string[]> CheekyReplies,
    string[] FallbackReplies)
{
    /// <summary>Other names players may call it by ("names:" in its block); a unique bot's only.</summary>
    public IReadOnlyList<string> Nicknames { get; init; } = [];

    /// <summary>How it reacts when Kobaldjovi sings nearby; null uses the plain-bot lines.</summary>
    public string[] SongReactions { get; init; }
}

/// <summary>
/// Conversation content for the six personality types, keyed like PersonalityTypeVoices.
/// The lines live in dialogue/&lt;type&gt;.dlg (see DialogueFile); all are original.
/// Each named bot of these types (NamedBotLineup) has its own block in its type's file
/// ("=== shnacksize"); its lines are mixed into its type's, see <see cref="Mix"/>.
/// PersonalityReactions puts them into chat.
/// </summary>
public static class PersonalityLines
{
    public static readonly IReadOnlyList<string> TypeKeys = ["hardcore", "casual", "minmaxer", "joker", "loner", "leeroy"];

    /// <summary>Types only the named bots have; generated bots never roll them, so they're not in <see cref="All"/>.</summary>
    public static readonly IReadOnlyList<string> NamedOnlyKeys = ["crafter", "roleplayer", "homeowner", "herbaldoctor", "gruffdoc"];

    private const string ToneDownOff = "tone_down off";
    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

    private sealed record Snapshot(
        IReadOnlyDictionary<string, TypeLines> All,
        IReadOnlyDictionary<string, TypeTalk> Talk,
        IReadOnlyDictionary<string, TypeTalk> NamedOnlyTalk,
        IReadOnlyDictionary<string, (TypeLines Lines, TypeTalk Talk)> Own,
        IReadOnlyDictionary<string, TypeLines> Named,
        IReadOnlyDictionary<string, TypeTalk> NamedTalk,
        IReadOnlyList<string> Errors);

    private static readonly DialogueCache<Snapshot> Current = new(Build);

    /// <summary>Problems found while turning the files into lines (unknown sections, topics...).</summary>
    public static IReadOnlyList<string> Errors => Current.Value.Errors;

    public static IReadOnlyDictionary<string, TypeLines> All => Current.Value.All;
    public static IReadOnlyDictionary<string, TypeTalk> Talk => Current.Value.Talk;

    /// <summary>The talk of the named-only types (Crafter, Roleplayer, Homeowner...), by type key.</summary>
    public static IReadOnlyDictionary<string, TypeTalk> NamedOnlyTalk => Current.Value.NamedOnlyTalk;

    /// <summary>A named bot's own lines only, by its memory key ("named:shnacksize").</summary>
    public static IReadOnlyDictionary<string, (TypeLines Lines, TypeTalk Talk)> Own => Current.Value.Own;

    /// <summary>A named bot's type lines with its own mixed in, by its memory key.</summary>
    public static IReadOnlyDictionary<string, TypeLines> Named => Current.Value.Named;
    public static IReadOnlyDictionary<string, TypeTalk> NamedTalk => Current.Value.NamedTalk;

    /// <summary>The memory key of a named bot's personality: "named:shnacksize".</summary>
    public static string NamedKey(NamedBotLineup.Entry entry) => "named:" + BlockName(entry);

    /// <summary>The block holding a named bot's own lines in its type's file: its name in lower case.</summary>
    public static string BlockName(NamedBotLineup.Entry entry) => entry.Name.ToLowerInvariant();

    private static Snapshot Build()
    {
        var errors = new List<string>();
        var all = new Dictionary<string, TypeLines>();
        var talk = new Dictionary<string, TypeTalk>();
        var namedOnlyTalk = new Dictionary<string, TypeTalk>();
        var own = new Dictionary<string, (TypeLines, TypeTalk)>();
        var named = new Dictionary<string, TypeLines>();
        var namedTalk = new Dictionary<string, TypeTalk>();
        foreach (string key in TypeKeys.Concat(NamedOnlyKeys))
        {
            (TypeLines typeLines, TypeTalk typeTalk, Dictionary<string, (TypeLines, TypeTalk)> ownOfType) = ReadFile(DialogueLibrary.For(key), key, errors);
            if (TypeKeys.Contains(key))
                (all[key], talk[key]) = (typeLines, typeTalk);
            else
                namedOnlyTalk[key] = typeTalk;
            foreach ((string namedKey, (TypeLines ownLines, TypeTalk ownTalk)) in ownOfType)
            {
                own[namedKey] = (ownLines, ownTalk);
                named[namedKey] = Merge(typeLines, ownLines);
                namedTalk[namedKey] = Merge(typeTalk, ownTalk);
            }
        }

        foreach (string error in errors)
            if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {error}");
        return new Snapshot(all, talk, namedOnlyTalk, own, named, namedTalk, errors);
    }

    /// <summary>What's wrong with a type's file ("joker", "crafter") beyond the reader's errors.</summary>
    public static List<string> Check(DialogueFile file, string key)
    {
        var errors = new List<string>();
        ReadFile(file, key, errors);
        return errors;
    }

    /// <summary>A type's file: the type's own lines and talk, and each of its named bots' own lines by memory key.</summary>
    private static (TypeLines, TypeTalk, Dictionary<string, (TypeLines, TypeTalk)>) ReadFile(DialogueFile file, string key, List<string> errors)
    {
        var own = new Dictionary<string, (TypeLines, TypeTalk)>();
        if (file == null)
            errors.Add($"{key}.dlg: missing");
        (TypeLines typeLines, TypeTalk typeTalk) = Read(file?.Block(key) ?? [], $"{key}.dlg", errors);
        if (file == null)
            return (typeLines, typeTalk, own);

        NamedBotLineup.Entry[] entries = NamedBotLineup.All.Where(entry => entry.TypeKey == key).ToArray();
        foreach (string block in file.BlockNames)
            if (!string.Equals(block, key, StringComparison.OrdinalIgnoreCase) && !entries.Any(entry => BlockName(entry) == block.ToLowerInvariant()))
                errors.Add($"{key}.dlg: block '{block}' is neither the type nor one of its named bots");
        foreach (NamedBotLineup.Entry entry in entries)
        {
            IReadOnlyList<DialogueSection> sections = file.Block(BlockName(entry));
            if (sections.Count == 0)
                errors.Add($"{key}.dlg: no block '=== {BlockName(entry)}' for {entry.Name}");
            own[NamedKey(entry)] = Read(sections, $"{key}.dlg", errors);
        }
        return (typeLines, typeTalk, own);
    }

    /// <summary>A type's talk, or empty lists when it has none.</summary>
    public static TypeTalk TalkFor(string key) => Talk.GetValueOrDefault(key) ?? NamedOnlyTalk.GetValueOrDefault(key) ?? new TypeTalk([], new Dictionary<eAutonomousChatIntent, string[]>(), null, []);

    /// <summary>A named bot's talk (type plus own), or null when it has none.</summary>
    public static TypeTalk NamedTalkFor(string namedKey) => NamedTalk.GetValueOrDefault(namedKey ?? string.Empty);

    /// <summary>
    /// A type's pool with a named bot's own lines added. The own lines are repeated so that,
    /// in a pool that has both, they come up about half the time; a type pool alone or an own
    /// pool alone is returned as it is.
    /// </summary>
    public static string[] Mix(string[] type, string[] own)
    {
        if (own is not { Length: > 0 })
            return type;
        if (type is not { Length: > 0 })
            return own;
        int copies = Math.Max(1, (int)Math.Round((double)type.Length / own.Length));
        return type.Concat(Enumerable.Repeat(own, copies).SelectMany(lines => lines)).ToArray();
    }

    private static Dictionary<TKey, string[]> MixAll<TKey>(IReadOnlyDictionary<TKey, string[]> type, IReadOnlyDictionary<TKey, string[]> own)
    {
        if (type == null && own == null)
            return null;
        var mixed = new Dictionary<TKey, string[]>();
        foreach (TKey key in (type?.Keys ?? []).Union(own?.Keys ?? []))
            mixed[key] = Mix(type?.GetValueOrDefault(key), own?.GetValueOrDefault(key));
        return mixed;
    }

    private static TypeLines Merge(TypeLines type, TypeLines own) => new()
    {
        Openers = MixAll(type.Openers, own.Openers), TopicReplies = MixAll(type.TopicReplies, own.TopicReplies),
        Ding = Mix(type.Ding, own.Ding), Grats = Mix(type.Grats, own.Grats), OwnDeath = Mix(type.OwnDeath, own.OwnDeath),
        OtherDeath = Mix(type.OtherDeath, own.OtherDeath), Drop = Mix(type.Drop, own.Drop), Cheeky = Mix(type.Cheeky, own.Cheeky),
    };

    private static TypeTalk Merge(TypeTalk type, TypeTalk own) => new(
        Mix(type.Ambient, own.Ambient), MixAll(type.Replies, own.Replies),
        MixAll(type.CheekyReplies, own.CheekyReplies), Mix(type.FallbackReplies, own.FallbackReplies));

    private static (TypeLines, TypeTalk) Read(IReadOnlyList<DialogueSection> sections, string fileName, List<string> errors)
    {
        var openers = new Dictionary<eChatTopic, string[]>();
        var topicReplies = new Dictionary<eChatTopic, string[]>();
        var replies = new Dictionary<eAutonomousChatIntent, string[]>();
        Dictionary<eAutonomousChatIntent, string[]> cheekyReplies = null;
        string[] ambient = [], fallback = [], ding = [], grats = [], ownDeath = [], otherDeath = [], drop = [], cheeky = null;

        foreach (DialogueSection section in sections)
        {
            string where = $"{fileName}:{section.LineNumber}";
            string[] lines = section.Lines.ToArray();
            bool cheekySection = section.Name is "cheeky" or "cheeky_reply";
            if (cheekySection != (section.Condition == ToneDownOff))
            {
                errors.Add(cheekySection ? $"{where}: '{section.Name}' needs [if {ToneDownOff}]" : $"{where}: '{section.Name}' can't have a condition yet");
                continue;
            }
            bool needsArgument = section.Name is "opener" or "topic_reply" or "reply" or "cheeky_reply";
            if (needsArgument != (section.Argument != null))
            {
                errors.Add($"{where}: '{section.Name}' {(needsArgument ? "needs a topic or intent" : "takes no argument")}");
                continue;
            }
            switch (section.Name)
            {
                case "opener" when Topic(section, where, errors) is { } topic: openers[topic] = lines; break;
                case "topic_reply" when Topic(section, where, errors) is { } topic: topicReplies[topic] = lines; break;
                case "reply" when Intent(section, where, errors) is { } intent: replies[intent] = lines; break;
                case "cheeky_reply" when Intent(section, where, errors) is { } intent: (cheekyReplies ??= new())[intent] = lines; break;
                case "opener" or "topic_reply" or "reply" or "cheeky_reply": break; // bad argument, already reported
                case "ambient": ambient = lines; break;
                case "fallback": fallback = lines; break;
                case "ding": ding = lines; break;
                case "grats": grats = lines; break;
                case "own_death": ownDeath = lines; break;
                case "other_death": otherDeath = lines; break;
                case "drop": drop = lines; break;
                case "cheeky": cheeky = lines; break;
                default: errors.Add($"{where}: unknown section '{section.Name}'"); break;
            }
        }

        var typeLines = new TypeLines
        {
            Openers = openers, TopicReplies = topicReplies, Ding = ding, Grats = grats,
            OwnDeath = ownDeath, OtherDeath = otherDeath, Drop = drop, Cheeky = cheeky,
        };
        return (typeLines, new TypeTalk(ambient, replies, cheekyReplies, fallback));
    }

    private static eChatTopic? Topic(DialogueSection section, string where, List<string> errors) =>
        Enum.TryParse(section.Argument, true, out eChatTopic topic) && Enum.IsDefined(topic) ? topic : Bad<eChatTopic>(errors, where, "topic", section.Argument);

    private static eAutonomousChatIntent? Intent(DialogueSection section, string where, List<string> errors) =>
        Enum.TryParse(section.Argument, true, out eAutonomousChatIntent intent) && Enum.IsDefined(intent) ? intent : Bad<eAutonomousChatIntent>(errors, where, "intent", section.Argument);

    private static T? Bad<T>(List<string> errors, string where, string what, string value) where T : struct
    {
        errors.Add($"{where}: unknown {what} '{value}'");
        return null;
    }
}
