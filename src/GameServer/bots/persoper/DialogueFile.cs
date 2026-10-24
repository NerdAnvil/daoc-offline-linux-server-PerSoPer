using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>One "~ name argument [if condition]" section of a dialogue file and its lines.</summary>
public sealed record DialogueSection(string Name, string Argument, string Condition, IReadOnlyList<string> Lines, int LineNumber);

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

        void Close()
        {
            if (section != null)
            {
                if (lines.Count == 0)
                    file.Error(sectionRow, $"section '{section}' has no lines");
                else if (section == "fallback" && lines.All(line => file._rules.GetValueOrDefault(line)?.Conditions.Count > 0))
                    file.Error(sectionRow, "every fallback line has [if ...]; keep at least one without");
                block.Add(new DialogueSection(section, argument, condition, lines, sectionRow));
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
                }
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
