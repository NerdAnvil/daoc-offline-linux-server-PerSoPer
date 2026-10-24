using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DOL.Logging;

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

    /// <summary>Its name in scenes.dlg ("knock-knock"); null for scripts built in code.</summary>
    public string Name { get; init; }

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
public sealed record TypePair(string Lead, string Rival, IReadOnlyList<SceneScript> Scenes)
{
    /// <summary>Its block name in scenes.dlg: "joker-loner".</summary>
    public string Name => $"{Lead}-{Rival}";
}

/// <summary>
/// The light type relationships (Hardcore vs Casual, Min/maxer lectures Leeroy, Joker teases
/// Loner) and their scripts, read from dialogue/scenes.dlg: one "=== lead-rival" block per pair,
/// one "~ short|medium|large name [if tone_down off]" section per scene, lines "A: text".
/// <see cref="Check"/> lists what a scenes file breaks; the scenes it breaks are left out.
/// </summary>
public static class PairScenes
{
    public const string FileName = "scenes";

    /// <summary>Scenes read per pair at most.</summary>
    public const int MaxScenesPerPair = 100;

    private static readonly HashSet<string> Tokens = ["{a}", "{b}", "{c}", "{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");
    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

    private static readonly DialogueCache<IReadOnlyList<TypePair>> Current = new(() =>
    {
        var errors = new List<string>();
        IReadOnlyList<TypePair> pairs = Read(DialogueLibrary.For(FileName), errors);
        foreach (string error in errors)
            if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {error}");
        return pairs;
    });

    public static IReadOnlyList<TypePair> All => Current.Value;

    /// <summary>A pair by its block name, in either order ("loner-joker"), or null.</summary>
    public static TypePair Find(string name)
    {
        string[] types = (name ?? string.Empty).ToLowerInvariant().Split('-');
        return types.Length == 2 ? For(types[0], types[1]) : null;
    }

    /// <summary>Everything wrong with a scenes file, as "file:line: what".</summary>
    public static List<string> Check(DialogueFile file)
    {
        var errors = new List<string>();
        Read(file, errors);
        return errors;
    }

    private static IReadOnlyList<TypePair> Read(DialogueFile file, List<string> errors)
    {
        var pairs = new List<TypePair>();
        if (file == null)
        {
            errors.Add($"{FileName}.dlg: missing");
            return pairs;
        }
        foreach (string block in file.BlockNames)
        {
            IReadOnlyList<DialogueSection> sections = file.Block(block);
            string where = sections.Count > 0 ? $"{file.FileName}:{sections[0].LineNumber}: '=== {block}'" : $"{file.FileName}: '=== {block}'";
            string[] types = block.Split('-');
            if (types.Length != 2 || !PersonalityLines.TypeKeys.Contains(types[0]) || !PersonalityLines.TypeKeys.Contains(types[1]) || types[0] == types[1])
            {
                errors.Add($"{where}: a pair is two different types, lead first, like 'joker-loner' (types: {string.Join(", ", PersonalityLines.TypeKeys)})");
                continue;
            }
            if (pairs.Any(pair => pair.Lead == types[1] && pair.Rival == types[0]))
            {
                errors.Add($"{where}: this pair is already in the file the other way round");
                continue;
            }
            if (file.Names(block).Count > 0)
                errors.Add($"{where}: 'names:' doesn't belong in a scenes file");

            var scenes = new List<SceneScript>();
            foreach (DialogueSection section in sections.Take(MaxScenesPerPair))
                if (ReadScene(file, section, scenes, errors) is { } scene)
                    scenes.Add(scene);
            if (sections.Count > MaxScenesPerPair)
                errors.Add($"{where}: more than {MaxScenesPerPair} scenes; the rest are ignored");
            if (scenes.Count == 0)
                errors.Add($"{where}: no scenes that work");
            else
                pairs.Add(new TypePair(types[0], types[1], scenes));
        }
        if (pairs.Count == 0 && file.Errors.Count == 0)
            errors.Add($"{file.FileName}: no pairs");
        return pairs;
    }

    /// <summary>One "~ short name" section as a scene, or null (with the reason in errors) when it breaks a rule.</summary>
    private static SceneScript ReadScene(DialogueFile file, DialogueSection section, List<SceneScript> earlier, List<string> errors)
    {
        string where = $"{file.FileName}:{section.LineNumber}: '~ {section.Name}{(section.Argument == null ? "" : " " + section.Argument)}'";
        if (!Enum.TryParse(section.Name, true, out eSceneSize size))
        {
            errors.Add($"{where}: a scene is '~ short', '~ medium' or '~ large', then its name");
            return null;
        }
        if (section.Argument == null)
        {
            errors.Add($"{where}: the scene needs a name, like '~ {section.Name} knock-knock'");
            return null;
        }
        if (earlier.Any(scene => scene.Name == section.Argument))
        {
            errors.Add($"{where}: another scene in this pair has the name '{section.Argument}'");
            return null;
        }
        if (section.Condition is not (null or "tone_down off"))
        {
            errors.Add($"{where}: scenes only take [if tone_down off] (a cheeky scene)");
            return null;
        }

        SceneScript scene = SceneScript.Of(size, section.Lines.ToArray()) with { Name = section.Argument, Cheeky = section.Condition != null };
        int problems = errors.Count;
        int min = PersonalityScenes.MinLines(size), max = PersonalityScenes.MaxLines(size);
        if (scene.Lines.Count < min || scene.Lines.Count > max)
            errors.Add($"{where}: has {scene.Lines.Count} lines; a {section.Name} scene has {min} to {max}");
        for (int i = 0; i < scene.Lines.Count; i++)
        {
            SceneLine line = scene.Lines[i];
            if (line.Speaker < 0)
                errors.Add($"{where}: line {i + 1} doesn't start with 'A:', 'B:' or 'C:' ({line.Text})");
            else if (i == 0 && line.Speaker != 0)
                errors.Add($"{where}: A says the first line");
            else if (i > 0 && line.Speaker == scene.Lines[i - 1].Speaker)
                errors.Add($"{where}: line {i + 1}: the same speaker twice in a row");
            foreach (Match match in Placeholder.Matches(line.Text ?? string.Empty))
                if (!Tokens.Contains(match.Value))
                    errors.Add($"{where}: line {i + 1}: unknown {match.Value} (use {string.Join(", ", Tokens)})");
        }
        if (!scene.Lines.Any(line => line.Speaker == 1))
            errors.Add($"{where}: B never speaks");
        return errors.Count == problems ? scene : null;
    }

    /// <summary>The pair these two types make (in either order), or null.</summary>
    public static TypePair For(string typeA, string typeB) =>
        All.FirstOrDefault(pair => pair.Lead == typeA && pair.Rival == typeB || pair.Lead == typeB && pair.Rival == typeA);

    /// <summary>The types this one has a relationship with.</summary>
    public static IReadOnlyList<string> RivalsOf(string type) =>
        All.Where(pair => pair.Lead == type || pair.Rival == type)
            .Select(pair => pair.Lead == type ? pair.Rival : pair.Lead)
            .ToArray();
}
