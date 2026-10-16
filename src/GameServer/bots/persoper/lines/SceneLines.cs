using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>How long a scene runs: Short up to 4 lines, Medium up to 8, Large up to 12.</summary>
public enum eSceneSize
{
    Short,
    Medium,
    Large,
}

/// <summary>One line of a scripted scene: who says it (0 = A, 1 = B, 2 = C) and what.</summary>
public sealed record SceneLine(int Speaker, string Text);

/// <summary>
/// A scripted scene. Written as "A: text", "B: text", "C: text"; in the text {a}, {b}, {c}
/// are the speakers' names, and the slots {zone}, {monster}, {level}, {class}, {item} come
/// from A (a line whose slot is not known makes the whole scene skip).
/// </summary>
public sealed record SceneScript(eSceneSize Size, IReadOnlyList<SceneLine> Lines, bool Cheeky)
{
    public static SceneScript Of(eSceneSize size, params string[] lines) => new(size, Parse(lines), false);

    /// <summary>Only played when persoper_tone_down is off.</summary>
    public static SceneScript CheekyOf(eSceneSize size, params string[] lines) => new(size, Parse(lines), true);

    /// <summary>How many different speakers it needs (2 or 3).</summary>
    public int Speakers => Lines.Count == 0 ? 0 : Lines.Max(line => line.Speaker) + 1;

    private static SceneLine[] Parse(string[] lines) => lines.Select(raw =>
    {
        // A malformed line becomes speaker -1, which the content tests catch.
        if (raw == null || raw.Length < 3 || raw[1] != ':' || raw[0] is not ('A' or 'B' or 'C'))
            return new SceneLine(-1, raw);
        return new SceneLine(raw[0] - 'A', raw[2..].Trim());
    }).ToArray();
}

/// <summary>
/// One personality type's lines for topic scenes, beyond B1's single reply. {0} = the bot
/// it answers; optional slots as in PersonalityLines.
/// </summary>
public sealed class SceneTypeLines
{
    /// <summary>Its answer back to someone who replied on a topic (used by both sides of a scene).</summary>
    public Dictionary<eChatTopic, string[]> Comebacks { get; init; } = new();

    /// <summary>A short line that ends a scene.</summary>
    public string[] Closers { get; init; } = [];

    /// <summary>Mild cheeky closers, only when persoper_tone_down is off. Null for most types.</summary>
    public string[] CheekyClosers { get; init; }
}

/// <summary>Topic-scene lines for the six personality types, keyed like PersonalityLines. All lines are original.</summary>
public static partial class SceneLines
{
    public static readonly IReadOnlyDictionary<string, SceneTypeLines> All = new Dictionary<string, SceneTypeLines>
    {
        ["hardcore"] = Hardcore(),
        ["casual"] = Casual(),
        ["minmaxer"] = MinMaxer(),
        ["joker"] = Joker(),
        ["loner"] = Loner(),
        ["leeroy"] = Leeroy(),
    };
}

/// <summary>Two types that needle each other, and their scripted scenes. A is always the Lead type.</summary>
public sealed record TypePair(string Lead, string Rival, IReadOnlyList<SceneScript> Scenes);

/// <summary>The light type relationships: Hardcore vs Casual, Min/maxer lectures Leeroy, Joker teases Loner.</summary>
public static partial class PairScenes
{
    public static readonly IReadOnlyList<TypePair> All =
    [
        new("hardcore", "casual", HardcoreCasual()),
        new("minmaxer", "leeroy", MinMaxerLeeroy()),
        new("joker", "loner", JokerLoner()),
    ];

    /// <summary>The pair these two types make (in either order), or null.</summary>
    public static TypePair For(string typeA, string typeB) =>
        All.FirstOrDefault(pair => pair.Lead == typeA && pair.Rival == typeB || pair.Lead == typeB && pair.Rival == typeA);

    /// <summary>The types this one has a relationship with.</summary>
    public static IReadOnlyList<string> RivalsOf(string type) =>
        All.Where(pair => pair.Lead == type || pair.Rival == type)
            .Select(pair => pair.Lead == type ? pair.Rival : pair.Lead)
            .ToArray();
}
