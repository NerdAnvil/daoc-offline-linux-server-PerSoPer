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
/// Hark? [allow knightly]          a note for the voice tests: this line may use that word list (any section)
/// ~ part greet                     a swap-in part: one alternative per line, used as {greet} in later lines of the block
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

    /// <summary>The sections whose lines may carry rules (for now: the chat replies and ambient lines).</summary>
    public static readonly IReadOnlySet<string> RuleSections = new HashSet<string> { "reply", "cheeky_reply", "fallback", "ambient" };

    /// <summary>Nicknames per block at most.</summary>
    public const int MaxNames = 8;

    /// <summary>The section that holds a question and its branching replies.</summary>
    public const string AskSection = "ask";

    /// <summary>The answer that catches everything the other answers don't.</summary>
    public const string OtherAnswer = "other";

    /// <summary>Answers per question at most (the "other" answer counts).</summary>
    public const int MaxAnswers = 8;

    /// <summary>The section that defines a swap-in part: its lines are the alternatives.</summary>
    public const string PartSection = "part";

    /// <summary>Parts per block, alternatives per part, parts used in one line, and how deep parts may use parts, at most.</summary>
    public const int MaxParts = 8;
    public const int MaxAlternatives = 20;
    public const int MaxPartsPerLine = 3;
    public const int MaxNesting = 2;

    /// <summary>Lines one written line may turn into once its parts are filled in.</summary>
    public const int MaxVariants = 24;

    /// <summary>Names in {braces} the game fills itself; a part can't be called any of these.</summary>
    public static readonly IReadOnlySet<string> ReservedSlots = new HashSet<string> { "zone", "monster", "level", "class", "item", "a", "b", "c", "destination", "groupsize", "leader" };

    private sealed record Part(IReadOnlyList<string> Alternatives, int Depth);

    private readonly Dictionary<string, List<DialogueSection>> _blocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, Part>> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, DialogueRule> _rules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _texts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _allows = new(StringComparer.Ordinal);

    public string FileName { get; }
    public IReadOnlyList<string> Errors => _errors;
    public IEnumerable<string> BlockNames => _blocks.Keys;
    public int LineCount => _blocks.Values.Sum(sections => sections.Sum(section => section.Lines.Count));

    /// <summary>The lines that have rules, by their text without the tags.</summary>
    public IReadOnlyDictionary<string, DialogueRule> Rules => _rules;

    /// <summary>The rule of a line by its text, or null when it has none.</summary>
    public DialogueRule RuleFor(string text) => _rules.GetValueOrDefault(text ?? string.Empty);

    /// <summary>The "[allow ...]" tags of a line by its text; empty when it has none.</summary>
    public IReadOnlySet<string> AllowsFor(string text) =>
        _allows.TryGetValue(text ?? string.Empty, out HashSet<string> allows) ? allows : new HashSet<string>();

    private DialogueFile(string fileName) => FileName = fileName;

    /// <summary>The sections of one block, in file order; empty when the block is missing.</summary>
    public IReadOnlyList<DialogueSection> Block(string name) =>
        _blocks.TryGetValue(name ?? string.Empty, out List<DialogueSection> sections) ? sections : [];

    /// <summary>The swap-in parts of one block ("~ part greet") with their alternatives; empty when it has none.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Parts(string block) =>
        _parts.TryGetValue(block ?? string.Empty, out Dictionary<string, Part> parts)
            ? parts.ToDictionary(pair => pair.Key, pair => pair.Value.Alternatives)
            : new Dictionary<string, IReadOnlyList<string>>();

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
        Dictionary<string, Part> blockParts = null;
        string blockName = null;
        string section = null, argument = null, condition = null;
        int sectionRow = 0;
        List<string> lines = null;
        List<DialogueAnswer> answers = null;
        int partDepth = 0;

        void Close()
        {
            if (section == PartSection)
                file.ClosePart(blockParts, argument, condition, lines, sectionRow, partDepth);
            else if (section != null)
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
                blockParts = file._parts.TryGetValue(name, out Dictionary<string, Part> existingParts) ? existingParts : file._parts[name] = new(StringComparer.Ordinal);
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
                    partDepth = 0;
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
            (string lineText, DialogueRule rule) = DialogueRule.Split(row, tagErrors, out IReadOnlySet<string> allows);
            foreach (string tagError in tagErrors)
                file.Error(number, tagError);
            if (section == PartSection)
            {
                if (rule != null || allows.Count > 0)
                    file.Error(number, "a part's alternatives can't carry tags; put them on the lines that use the part");
                // The parts used inside a part are filled in now, so a part is stored as plain alternatives.
                lines.AddRange(file.Fill(lineText, blockParts, number, ref partDepth, true));
                continue;
            }
            if (rule != null && !RuleSections.Contains(section))
            {
                file.Error(number, $"rules only work in {string.Join(", ", RuleSections)} sections for now");
                rule = null;
            }
            // A line that uses parts becomes one line per way of filling them in; each keeps the tags.
            // An ask's question and replies can't use parts yet.
            int unusedDepth = 0;
            IReadOnlyList<string> variants = section == AskSection ? file.RejectParts(lineText, blockParts, number) : file.Fill(lineText, blockParts, number, ref unusedDepth, false);
            foreach (string variant in variants)
            {
                // Rules are found by text, so a tagged line's text must be unique in its file.
                bool seen = !file._texts.Add(variant);
                if (seen && (rule != null || file._rules.ContainsKey(variant)))
                    file.Error(number, "a line with tags must not appear twice in a file; change its text");
                else if (rule != null)
                    file._rules[variant] = rule;
                if (allows.Count > 0)
                {
                    if (!file._allows.TryGetValue(variant, out HashSet<string> known))
                        file._allows[variant] = known = new HashSet<string>(StringComparer.Ordinal);
                    known.UnionWith(allows);
                }
                if (variant.Length > 0)
                    lines.Add(variant);
            }
        }
        Close();
        return file;
    }

    /// <summary>Stores a finished "~ part" section in its block.</summary>
    private void ClosePart(Dictionary<string, Part> parts, string name, string condition, List<string> alternatives, int row, int depth)
    {
        if (parts == null)
        {
            Error(row, "section before any '=== block'");
            return;
        }
        if (name == null || !PartName().IsMatch(name))
            Error(row, "a part needs a name of 2 to 24 lower-case letters or _, for example '~ part greet'");
        else if (ReservedSlots.Contains(name))
            Error(row, $"'{{{name}}}' is filled in by the game; call the part something else");
        else if (parts.ContainsKey(name))
            Error(row, $"part '{name}' appears twice in this block");
        else if (parts.Count >= MaxParts)
            Error(row, $"more than {MaxParts} parts in a block; '{name}' is ignored");
        else if (alternatives.Count == 0)
            Error(row, $"part '{name}' has no lines");
        else if (condition != null)
            Error(row, "a part has no condition");
        else if (alternatives.Count > MaxAlternatives)
            Error(row, $"part '{name}' has {alternatives.Count} alternatives (at most {MaxAlternatives}, counting the parts inside it)");
        else if (alternatives.Distinct(StringComparer.Ordinal).Count() != alternatives.Count)
            Error(row, $"part '{name}' has the same alternative twice");
        else if (depth > MaxNesting)
            Error(row, $"part '{name}' uses parts {depth - 1} levels deep (at most {MaxNesting - 1} levels inside a part)");
        else
            parts[name] = new Part(alternatives.ToList(), Math.Max(depth, 1));
    }

    /// <summary>
    /// Every way to fill the {parts} of a line in; the line itself when it uses none. Problems go to the
    /// errors and give no lines. <paramref name="depth"/> becomes how deep the parts used go (a part that
    /// uses a part is deeper than a plain one); for a part's own line that is one more than the parts it uses.
    /// </summary>
    private IReadOnlyList<string> Fill(string text, Dictionary<string, Part> parts, int row, ref int depth, bool inPart)
    {
        MatchCollection matches = Brace().Matches(text);
        var used = new List<(Match Match, Part Part)>();
        foreach (Match match in matches)
        {
            string name = match.Groups["name"].Value;
            if (ReservedSlots.Contains(name))
                continue;
            if (parts == null || !parts.TryGetValue(name, out Part part))
            {
                Error(row, $"unknown part '{{{name}}}' (a part is defined with '~ part {name}' earlier in the same block)");
                return [];
            }
            used.Add((match, part));
        }
        int lineDepth = used.Count == 0 ? 0 : used.Max(use => use.Part.Depth);
        if (inPart)
            lineDepth++;
        depth = Math.Max(depth, lineDepth);
        if (used.Count == 0)
            return [text];
        if (used.Count > MaxPartsPerLine)
        {
            Error(row, $"{used.Count} parts in one line (at most {MaxPartsPerLine})");
            return [];
        }
        long combinations = 1;
        foreach ((_, Part part) in used)
            combinations *= part.Alternatives.Count;
        if (combinations > MaxVariants)
        {
            Error(row, $"this line turns into {combinations} lines once its parts are filled in (at most {MaxVariants}); use fewer or smaller parts");
            return [];
        }

        var results = new List<string> { string.Empty };
        int position = 0;
        foreach ((Match match, Part part) in used)
        {
            string before = text[position..match.Index];
            results = results.SelectMany(prefix => part.Alternatives.Select(alternative => prefix + before + alternative)).ToList();
            position = match.Index + match.Length;
        }
        string tail = text[position..];
        return results.Select(result => result + tail).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>The line as it is, after an error for each part in it.</summary>
    private IReadOnlyList<string> RejectParts(string text, Dictionary<string, Part> parts, int row)
    {
        foreach (Match match in Brace().Matches(text))
            if (parts != null && parts.ContainsKey(match.Groups["name"].Value))
                Error(row, $"part '{{{match.Groups["name"].Value}}}' can't be used in an ask yet");
        return [text];
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
        else if (!AnswerKey().IsMatch(key))
            Error(row, $"bad answer '{key}' (one word of 2 to 32 lower-case letters, so a chat line can match it)");
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

    [GeneratedRegex(@"\{(?<name>[a-z][a-z_]*)\}")]
    private static partial Regex Brace();

    [GeneratedRegex(@"^[a-z_]{2,24}$")]
    private static partial Regex PartName();

    [GeneratedRegex(@"^[a-z0-9_-]+$")]
    private static partial Regex Name();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]{1,23}$")]
    private static partial Regex Nickname();

    [GeneratedRegex(@"^[a-z]{2,32}$")]
    private static partial Regex AnswerKey();

    [GeneratedRegex(@"^~\s*(?<name>[A-Za-z_]+)(?:\s+(?<arg>[A-Za-z0-9_-]+))?(?:\s*\[if\s+(?<cond>[^\]]+)\])?\s*$")]
    private static partial Regex Header();
}
