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
    string[] FallbackReplies);

/// <summary>
/// Conversation content for the six personality types, keyed like PersonalityTypeVoices.
/// The lines live in dialogue/&lt;type&gt;.dlg (see DialogueFile); all are original.
/// PersonalityReactions puts them into chat.
/// </summary>
public static class PersonalityLines
{
    public static readonly IReadOnlyList<string> TypeKeys = ["hardcore", "casual", "minmaxer", "joker", "loner", "leeroy"];

    private const string ToneDownOff = "tone_down off";
    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly List<string> _errors = [];

    /// <summary>Problems found while turning the files into lines (unknown sections, topics...).</summary>
    public static IReadOnlyList<string> Errors => _errors;

    public static readonly IReadOnlyDictionary<string, TypeLines> All;
    public static readonly IReadOnlyDictionary<string, TypeTalk> Talk;

    static PersonalityLines()
    {
        var all = new Dictionary<string, TypeLines>();
        var talk = new Dictionary<string, TypeTalk>();
        foreach (string key in TypeKeys)
        {
            DialogueFile file = DialogueLibrary.For(key);
            if (file == null)
                _errors.Add($"{key}.dlg: missing");
            (all[key], talk[key]) = Read(file?.Block(key) ?? [], $"{key}.dlg");
        }
        All = all;
        Talk = talk;
        foreach (string error in _errors)
            if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {error}");
    }

    /// <summary>A type's talk, or empty lists when it has none.</summary>
    public static TypeTalk TalkFor(string key) => Talk.GetValueOrDefault(key) ?? new TypeTalk([], new Dictionary<eAutonomousChatIntent, string[]>(), null, []);

    private static (TypeLines, TypeTalk) Read(IReadOnlyList<DialogueSection> sections, string fileName)
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
                _errors.Add(cheekySection ? $"{where}: '{section.Name}' needs [if {ToneDownOff}]" : $"{where}: '{section.Name}' can't have a condition yet");
                continue;
            }
            bool needsArgument = section.Name is "opener" or "topic_reply" or "reply" or "cheeky_reply";
            if (needsArgument != (section.Argument != null))
            {
                _errors.Add($"{where}: '{section.Name}' {(needsArgument ? "needs a topic or intent" : "takes no argument")}");
                continue;
            }
            switch (section.Name)
            {
                case "opener" when Topic(section, where) is { } topic: openers[topic] = lines; break;
                case "topic_reply" when Topic(section, where) is { } topic: topicReplies[topic] = lines; break;
                case "reply" when Intent(section, where) is { } intent: replies[intent] = lines; break;
                case "cheeky_reply" when Intent(section, where) is { } intent: (cheekyReplies ??= new())[intent] = lines; break;
                case "opener" or "topic_reply" or "reply" or "cheeky_reply": break; // bad argument, already reported
                case "ambient": ambient = lines; break;
                case "fallback": fallback = lines; break;
                case "ding": ding = lines; break;
                case "grats": grats = lines; break;
                case "own_death": ownDeath = lines; break;
                case "other_death": otherDeath = lines; break;
                case "drop": drop = lines; break;
                case "cheeky": cheeky = lines; break;
                default: _errors.Add($"{where}: unknown section '{section.Name}'"); break;
            }
        }

        var typeLines = new TypeLines
        {
            Openers = openers, TopicReplies = topicReplies, Ding = ding, Grats = grats,
            OwnDeath = ownDeath, OtherDeath = otherDeath, Drop = drop, Cheeky = cheeky,
        };
        return (typeLines, new TypeTalk(ambient, replies, cheekyReplies, fallback));
    }

    private static eChatTopic? Topic(DialogueSection section, string where) =>
        Enum.TryParse(section.Argument, true, out eChatTopic topic) && Enum.IsDefined(topic) ? topic : Bad<eChatTopic>(where, "topic", section.Argument);

    private static eAutonomousChatIntent? Intent(DialogueSection section, string where) =>
        Enum.TryParse(section.Argument, true, out eAutonomousChatIntent intent) && Enum.IsDefined(intent) ? intent : Bad<eAutonomousChatIntent>(where, "intent", section.Argument);

    private static T? Bad<T>(string where, string what, string value) where T : struct
    {
        _errors.Add($"{where}: unknown {what} '{value}'");
        return null;
    }
}
