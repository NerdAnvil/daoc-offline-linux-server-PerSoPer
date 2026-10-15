using System.Collections.Generic;

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
/// Conversation content for the six personality types, keyed like PersonalityTypeVoices.
/// All lines are original. PersonalityReactions puts them into chat.
/// </summary>
public static partial class PersonalityLines
{
    public static readonly IReadOnlyDictionary<string, TypeLines> All = new Dictionary<string, TypeLines>
    {
        ["hardcore"] = Hardcore(),
        ["casual"] = Casual(),
        ["minmaxer"] = MinMaxer(),
        ["joker"] = Joker(),
        ["loner"] = Loner(),
        ["leeroy"] = Leeroy(),
    };
}
