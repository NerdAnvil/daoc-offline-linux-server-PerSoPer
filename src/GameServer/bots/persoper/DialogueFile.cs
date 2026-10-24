using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>One "-> key: reply" row of an ask section: what the player may answer and what the bot says back.</summary>
public sealed record DialogueAnswer(string Key, string Reply);

/// <summary>
/// One "~ name argument [if condition]" section of a dialogue file and its lines. An ask section
/// has its question as the only line and its "-> key: reply" rows as <paramref name="Answers"/>
/// (null in every other section).
/// </summary>
public sealed record DialogueSection(string Name, string Argument, string Condition, IReadOnlyList<string> Lines, int LineNumber, IReadOnlyList<DialogueAnswer> Answers = null);

/// <summary>
/// A bot dialogue file (*.dlg), a small format borrowed from Dialogue Manager (Nathan Hoad):
/// <code>
/// # a comment
/// === joker                        a block: one speaker (a type, later a named bot)
/// names: Jovi, KJ                  nicknames, right after the block line (optional)
/// ~ reply greeting                 a section: name, optional argument
/// Well, if it isn't {0}!           one line per row; {0} and {zone} etc. are kept as written
/// ~ cheeky [if tone_down off]      a section with a condition
/// \# a line that starts with #     a backslash keeps a leading #, ~, = or \ as text
/// Hold still! [if player_dead] [once]   a line with rules (see DialogueRule), reply sections only
/// ~ ask fav_music                  a question: one line, then "-> answer: reply" rows, "other" required
/// What do you listen to, {0}?
/// -> metal: A person of taste!
/// -> other: Never heard of it.
/// </code>
/// Blank lines are ignored. Problems are collected in <see cref="Errors"/> as "file:line: what".
/// A line's tags are taken off its text; <see cref="RuleFor"/> finds them again by the text.
/// </summary>
public sealed partial class DialogueFile
{
    /// <summary>Rows read per file at most.</summary>
    public const int MaxRows = 20000;

    /// <summary>The conditions a section may carry.</summary>
    public static readonly IReadOnlySet<string> KnownConditions = new HashSet<string> { "tone_down off", "tone_down on" };

    /// <summary>The sections whose lines may carry rules (for now: the chat replies).</summary>
    public static readonly IReadOnlySet<string> RuleSections = new HashSet<string> { "reply", "cheeky_reply", "fallback" };

    /// <summary>Nicknames per block at most.</summary>
    public const int MaxNames = 8;

    /// <summary>The section that holds a question and its branching replies.</summary>
    public const string AskSection = "ask";

    /// <summary>The answer that catches everything the other answers don't.</summary>
    public const string OtherAnswer = "other";

    /// <summary>Answers per question at most (the "other" answer counts).</summary>
    public const int MaxAnswers = 8;

    private readonly Dictionary<string, List<DialogueSection>> _blocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, DialogueRule> _rules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _texts = new(StringComparer.Ordinal);

    public string FileName { get; }
    public IReadOnlyList<string> Errors => _errors;
    public IEnumerable<string> BlockNames => _blocks.Keys;
    public int LineCount => _blocks.Values.Sum(sections => sections.Sum(section => section.Lines.Count));

    /// <summary>The lines that have rules, by their text without the tags.</summary>
    public IReadOnlyDictionary<string, DialogueRule> Rules => _rules;

    /// <summary>The rule of a line by its text, or null when it has none.</summary>
    public DialogueRule RuleFor(string text) => _rules.GetValueOrDefault(text ?? string.Empty);

    private DialogueFile(string fileName) => FileName = fileName;

    /// <summary>The sections of one block, in file order; empty when the block is missing.</summary>
    public IReadOnlyList<DialogueSection> Block(string name) =>
        _blocks.TryGetValue(name ?? string.Empty, out List<DialogueSection> sections) ? sections : [];

    /// <summary>The nicknames of one block ("names: Jovi, KJ"); empty when it has none.</summary>
    public IReadOnlyList<string> Names(string block) =>
        _names.TryGetValue(block ?? string.Empty, out List<string> names) ? names : [];

    public static DialogueFile Parse(string text, string fileName)
    {
        var file = new DialogueFile(fileName);
        string[] rows = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        if (rows.Length > MaxRows)
            file.Error(MaxRows, $"more than {MaxRows} rows; the rest is ignored");

        List<DialogueSection> block = null;
        string blockName = null;
        string section = null, argument = null, condition = null;
        int sectionRow = 0;
        List<string> lines = null;
        List<DialogueAnswer> answers = null;

        void Close()
        {
            if (section != null)
            {
                bool ask = section == AskSection;
                if (lines.Count == 0)
                    file.Error(sectionRow, ask ? "an ask has no question line" : $"section '{section}' has no lines");
                else if (section == "fallback" && lines.All(line => file._rules.GetValueOrDefault(line)?.Conditions.Count > 0))
                    file.Error(sectionRow, "every fallback line has [if ...]; keep at least one without");
                if (ask)
                {
                    if (argument == null)
                        file.Error(sectionRow, "an ask needs a name: '~ ask fav_music'");
                    if (answers.Count == 0)
                        file.Error(sectionRow, "an ask needs '-> answer: reply' rows");
                    else if (!answers.Any(answer => answer.Key == OtherAnswer))
                        file.Error(sectionRow, $"an ask needs a '-> {OtherAnswer}: reply' row for answers it doesn't know");
                }
                block.Add(new DialogueSection(section, argument, condition, lines, sectionRow, ask ? answers : null));
            }
            section = null;
        }

        for (int i = 0; i < rows.Length && i < MaxRows; i++)
        {
            int number = i + 1;
            string row = rows[i].Trim();
            if (row.Length == 0 || row[0] == '#')
                continue;

            if (row.StartsWith("===", StringComparison.Ordinal))
            {
                Close();
                string name = row[3..].Trim();
                if (!Name().IsMatch(name))
                    file.Error(number, $"bad block name '{name}'");
                else if (file._blocks.ContainsKey(name))
                    file.Error(number, $"block '{name}' appears twice");
                block = file._blocks.TryGetValue(name, out List<DialogueSection> existing) ? existing : file._blocks[name] = [];
                blockName = name;
                continue;
            }

            if (section == null && block != null && row.StartsWith("names:", StringComparison.OrdinalIgnoreCase))
            {
                if (block.Count > 0 || file._names.ContainsKey(blockName))
                    file.Error(number, "'names:' comes once, right after the '=== block' line");
                else
                    file._names[blockName] = file.ReadNames(row["names:".Length..], number);
                continue;
            }

            if (row[0] == '~')
            {
                Close();
                Match header = Header().Match(row);
                if (block == null)
                    file.Error(number, "section before any '=== block'");
                else if (!header.Success)
                    file.Error(number, $"bad section header '{row}'");
                else
                {
                    section = header.Groups["name"].Value.ToLowerInvariant();
                    argument = header.Groups["arg"].Success ? header.Groups["arg"].Value.ToLowerInvariant() : null;
                    condition = header.Groups["cond"].Success ? string.Join(' ', header.Groups["cond"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant() : null;
                    if (condition != null && !KnownConditions.Contains(condition))
                        file.Error(number, $"unknown condition '{condition}'");
                    if (block.Any(other => other.Name == section && other.Argument == argument))
                        file.Error(number, $"section '{section} {argument}' appears twice in this block");
                    sectionRow = number;
                    lines = [];
                    answers = [];
                }
                continue;
            }

            if (row.StartsWith("->", StringComparison.Ordinal))
            {
                if (section != AskSection)
                    file.Error(number, $"'->' answers only work in '~ {AskSection}' sections (use \\-> for text)");
                else
                    file.ReadAnswer(row[2..], number, lines.Count, answers);
                continue;
            }

            if (row[0] == '=')
            {
                file.Error(number, $"'=' starts a row but it isn't '=== block' (use \\= for text)");
                continue;
            }
            if (section == null)
            {
                file.Error(number, "line outside a section");
                continue;
            }
            if (row[0] == '\\')
                row = row[1..];
            if (row.Length == 0)
                continue;
            if (section == AskSection && lines.Count > 0)
            {
                file.Error(number, "an ask has one question line; answers start with '->'");
                continue;
            }
            var tagErrors = new List<string>();
            (string lineText, DialogueRule rule) = DialogueRule.Split(row, tagErrors);
            foreach (string tagError in tagErrors)
                file.Error(number, tagError);
            if (rule != null && !RuleSections.Contains(section))
            {
                file.Error(number, $"rules only work in {string.Join(", ", RuleSections)} sections for now");
                rule = null;
            }
            // Rules are found by text, so a tagged line's text must be unique in its file.
            bool seen = !file._texts.Add(lineText);
            if (seen && (rule != null || file._rules.ContainsKey(lineText)))
                file.Error(number, "a line with tags must not appear twice in a file; change its text");
            else if (rule != null)
                file._rules[lineText] = rule;
            if (lineText.Length > 0)
                lines.Add(lineText);
        }
        Close();
        return file;
    }

    private void Error(int row, string message) => _errors.Add($"{FileName}:{row}: {message}");

    /// <summary>One "key: reply" answer of an ask; <paramref name="questions"/> is how many question lines the ask has so far.</summary>
    private void ReadAnswer(string text, int row, int questions, List<DialogueAnswer> answers)
    {
        if (questions == 0)
        {
            Error(row, "an ask's question line comes before its answers");
            return;
        }
        int colon = text.IndexOf(':');
        string key = (colon < 0 ? text : text[..colon]).Trim().ToLowerInvariant();
        string reply = colon < 0 ? string.Empty : text[(colon + 1)..].Trim();
        if (colon < 0)
            Error(row, "an answer reads '-> key: reply'");
        else if (!Name().IsMatch(key))
            Error(row, $"bad answer '{key}' (lower-case letters, digits, _ or -, one word)");
        else if (reply.Length == 0)
            Error(row, $"answer '{key}' has no reply");
        else if (answers.Any(answer => answer.Key == key))
            Error(row, $"answer '{key}' appears twice");
        else if (answers.Count >= MaxAnswers)
            Error(row, $"more than {MaxAnswers} answers; the rest are ignored");
        else
            answers.Add(new DialogueAnswer(key, reply));
    }

    private List<string> ReadNames(string list, int row)
    {
        var names = new List<string>();
        foreach (string part in list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (names.Count >= MaxNames)
            {
                Error(row, $"more than {MaxNames} names; the rest are ignored");
                break;
            }
            if (!Nickname().IsMatch(part))
                Error(row, $"bad name '{part}' (2 to 24 letters or digits, starting with a letter)");
            else if (names.Contains(part, StringComparer.OrdinalIgnoreCase))
                Error(row, $"name '{part}' appears twice");
            else
                names.Add(part);
        }
        if (names.Count == 0)
            Error(row, "'names:' has no names");
        return names;
    }

    [GeneratedRegex(@"^[a-z0-9_-]+$")]
    private static partial Regex Name();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]{1,23}$")]
    private static partial Regex Nickname();

    [GeneratedRegex(@"^~\s*(?<name>[A-Za-z_]+)(?:\s+(?<arg>[A-Za-z0-9_-]+))?(?:\s*\[if\s+(?<cond>[^\]]+)\])?\s*$")]
    private static partial Regex Header();
}
