using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Everything wrong with a dialogue file, as "file:line: what": the reader's errors, what the code
/// that uses the file rejects (unknown sections, topics, intents, blocks), and any section the
/// built-in copy has that the file lost (the code asks for those). The server uses a folder file
/// only when this finds nothing; the dialogue checker program reports the same list.
/// </summary>
public static class DialogueCheck
{
    /// <summary>The problems of <paramref name="file"/> loaded as "name.dlg"; <paramref name="builtIn"/> is the built-in copy, or null.</summary>
    public static List<string> Problems(DialogueFile file, string name, DialogueFile builtIn)
    {
        var problems = new List<string>(file.Errors);
        if (PersonalityLines.TypeKeys.Contains(name) || PersonalityLines.NamedOnlyKeys.Contains(name))
            problems.AddRange(PersonalityLines.Check(file, name));
        else if (name == "intents")
        {
            IntentPhrases phrases = IntentPhrases.From(file);
            problems.AddRange(phrases.Errors);
            if (phrases.Count == 0)
                problems.Add($"{file.FileName}: no phrases");
        }
        else if (IdentityDialogue.FileNames.Contains(name))
            problems.AddRange(IdentityDialogue.Check(file, name));

        if (builtIn != null)
        {
            foreach (string block in builtIn.BlockNames)
            {
                IReadOnlyList<DialogueSection> sections = file.Block(block);
                if (sections.Count == 0)
                {
                    problems.Add($"{file.FileName}: block '=== {block}' is missing (the built-in file has it)");
                    continue;
                }
                foreach (DialogueSection wanted in builtIn.Block(block))
                    if (!sections.Any(section => section.Name == wanted.Name && section.Argument == wanted.Argument))
                        problems.Add($"{file.FileName}: '=== {block}' has no '~ {wanted.Name}{(wanted.Argument == null ? "" : " " + wanted.Argument)}' (the built-in file has it)");
            }
        }
        return problems;
    }
}

/// <summary>One line of a preview: its text, its conditions as written, and whether the bot could say it.</summary>
public sealed record DialoguePreviewLine(string Text, string Conditions, string Verdict);

/// <summary>
/// What a bot would answer in a given situation, for the dialogue checker: every line of the pool
/// with its verdict. It follows the conditions and "most specific wins" (DialoguePicker); [once],
/// cooldowns and recent lines are left out, as if the bot had just started.
/// </summary>
public static class DialoguePreview
{
    public const string CanSay = "can say";
    public const string Beaten = "beaten by a more specific line";

    /// <summary>A personality by name, nickname or type: "Kobaldjovi", "KJ", "joker", "Shnacksize". Null when none.</summary>
    public static BotPersonality Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        foreach (BotPersonality personality in BotPersonalities.All)
            if (string.Equals(personality.Key, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(personality.Name, name, StringComparison.OrdinalIgnoreCase) ||
                personality.Nicknames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return personality;
        if (PersonalityTypes.Templates.GetValueOrDefault(name.ToLowerInvariant()) is { } template)
            return template;
        NamedBotLineup.Entry entry = NamedBotLineup.All.FirstOrDefault(found => string.Equals(found.Name, name, StringComparison.OrdinalIgnoreCase));
        return entry == null ? null : PersonalityTypes.Named(entry);
    }

    /// <summary>
    /// The pool a reply to <paramref name="intent"/> comes from ("reply thank", its cheeky pool, or
    /// "fallback" when it has no lines for the intent) and each line's verdict.
    /// </summary>
    public static (string Pool, List<DialoguePreviewLine> Lines) Reply(BotPersonality personality, eAutonomousChatIntent intent, DialogueFacts facts, bool cheeky)
    {
        string name = intent.ToString().ToLowerInvariant();
        string[] lines = cheeky ? personality.CheekyReplies?.GetValueOrDefault(intent) : personality.Replies.GetValueOrDefault(intent);
        string pool = cheeky ? $"cheeky_reply {name}" : $"reply {name}";
        if (lines is not { Length: > 0 })
        {
            if (cheeky)
                return (pool, []);
            (lines, pool) = (personality.FallbackReplies, "fallback");
        }

        var rules = lines.Select(DialogueLibrary.RuleFor).ToArray();
        bool[] allowed = rules.Select(rule => rule == null || rule.Conditions.All(facts.Holds)).ToArray();
        int best = Enumerable.Range(0, lines.Length).Where(i => allowed[i]).Select(i => rules[i]?.Specificity ?? 0).DefaultIfEmpty(-1).Max();
        var result = new List<DialoguePreviewLine>();
        for (int i = 0; i < lines.Length && i < DialoguePicker.MaxPool; i++)
        {
            string conditions = rules[i] is { Conditions.Count: > 0 } rule ? string.Join(", ", rule.Conditions.Select(Describe)) : "";
            string verdict = !allowed[i] ? $"not now: [if {conditions}]" : (rules[i]?.Specificity ?? 0) == best ? CanSay : Beaten;
            result.Add(new DialoguePreviewLine(lines[i], conditions, verdict));
        }
        return (pool, result);
    }

    /// <summary>
    /// The situation from words like "player_dead", "health=20", "mood=warm", "recent=loot",
    /// "nearby=kobaldjovi", "zone=Camelot City". Unknown facts or values go to <paramref name="errors"/>.
    /// </summary>
    public static DialogueFacts Facts(IEnumerable<string> words, List<string> errors)
    {
        bool? dead = null, combat = null;
        int? health = null, groupSize = null, deaths = null;
        string mood = null, timeOfDay = null, zone = null;
        var recent = new HashSet<string>();
        var nearby = new HashSet<string>();
        foreach (string word in words.Take(DialogueRule.MaxConditions * 4))
        {
            int equals = word.IndexOf('=');
            string fact = (equals < 0 ? word : word[..equals]).Trim().ToLowerInvariant();
            string value = equals < 0 ? "yes" : word[(equals + 1)..].Trim();
            if (DialogueRule.YesNoFacts.Contains(fact))
            {
                bool? yes = value.ToLowerInvariant() switch { "yes" or "true" => true, "no" or "false" => false, _ => null };
                if (yes == null)
                    errors.Add($"'{word}': {fact} is yes or no");
                else if (fact == "player_dead")
                    dead = yes;
                else
                    combat = yes;
            }
            else if (DialogueRule.NumberFacts.Contains(fact))
            {
                if (!int.TryParse(value, out int number))
                    errors.Add($"'{word}': {fact} needs a number");
                else if (fact == "health")
                    health = number;
                else if (fact == "group_size")
                    groupSize = number;
                else
                    deaths = number;
            }
            else if (DialogueRule.ChoiceFacts.TryGetValue(fact, out IReadOnlySet<string> choices))
            {
                value = value.ToLowerInvariant();
                if (!choices.Contains(value))
                    errors.Add($"'{word}': {fact} is one of {string.Join(", ", choices)}");
                else if (fact == "mood")
                    mood = value;
                else if (fact == "time_of_day")
                    timeOfDay = value;
                else
                    recent.Add(value);
            }
            else if (fact == "zone")
                zone = value;
            else if (fact == "nearby")
                nearby.Add(value.ToLowerInvariant());
            else
                errors.Add($"'{word}': unknown fact '{fact}'");
        }
        return new DialogueFacts
        {
            BotId = "preview", PlayerId = "preview", PlayerDead = dead, InCombat = combat, Health = health, GroupSize = groupSize,
            DeathsToday = deaths, Mood = mood, TimeOfDay = timeOfDay, Zone = zone, Recent = recent, Nearby = nearby,
        };
    }

    private static string Describe(DialogueCondition condition) =>
        condition.Operator != null ? $"{condition.Fact} {condition.Operator} {condition.Number}"
        : condition.Value != null ? $"{condition.Fact} {condition.Value}"
        : (condition.Negated ? "not " : "") + condition.Fact;
}
