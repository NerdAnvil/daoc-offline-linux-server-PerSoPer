using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>One check in a line's "[if ...]": a fact, and for most facts a value or a comparison.</summary>
public sealed record DialogueCondition(string Fact, bool Negated, string Operator, int Number, string Value);

/// <summary>
/// The tags a dialogue line can end with:
/// <code>
/// Hold still, {0}! [if player_dead]
/// Again, {0}? [if player_dead, deaths_today >= 3] [once]
/// Nice drop! [if recent loot] [cooldown 11m]
/// </code>
/// Conditions are about the player the bot is talking to (see <see cref="DialogueFacts"/>).
/// </summary>
public sealed partial record DialogueRule(IReadOnlyList<DialogueCondition> Conditions, bool Once, TimeSpan Cooldown)
{
    /// <summary>Conditions per line, comparison size and cooldown length at most.</summary>
    public const int MaxConditions = 8;
    public const int MaxNumber = 100000;
    public static readonly TimeSpan MaxCooldown = TimeSpan.FromHours(24);

    /// <summary>Facts that are true or false: "[if player_dead]", "[if not in_combat]".</summary>
    public static readonly IReadOnlySet<string> YesNoFacts = new HashSet<string> { "player_dead", "in_combat" };

    /// <summary>Facts compared with a number: "[if health < 30]".</summary>
    public static readonly IReadOnlySet<string> NumberFacts = new HashSet<string> { "health", "group_size", "deaths_today" };

    /// <summary>Facts with a fixed set of values: "[if mood warm]".</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ChoiceFacts = new Dictionary<string, IReadOnlySet<string>>
    {
        ["mood"] = new HashSet<string> { "warm", "neutral", "cool" },
        ["time_of_day"] = new HashSet<string> { "morning", "day", "evening", "night" },
        ["recent"] = new HashSet<string> { "death", "kill", "loot", "level_up" },
    };

    /// <summary>Facts with any text value: "[if zone camelot city]", "[if nearby kobaldjovi]".</summary>
    public static readonly IReadOnlySet<string> TextFacts = new HashSet<string> { "zone", "nearby" };

    /// <summary>The most specific line wins: the one with the most conditions.</summary>
    public int Specificity => Conditions.Count;

    /// <summary>
    /// Splits the tags off the end of a line. Returns the plain text and the rule (null when the line
    /// has no tags). Problems go to <paramref name="errors"/> as plain messages.
    /// </summary>
    public static (string Text, DialogueRule Rule) Split(string line, List<string> errors)
    {
        var conditions = new List<DialogueCondition>();
        bool once = false;
        TimeSpan cooldown = TimeSpan.Zero;
        bool tagged = false;
        string text = line;
        for (int i = 0; i < 4; i++)
        {
            Match tag = TrailingTag().Match(text);
            if (!tag.Success)
                break;
            tagged = true;
            text = text[..tag.Index].TrimEnd();
            string body = tag.Groups["body"].Value.Trim();
            string keyword = body.Split(' ', 2)[0].ToLowerInvariant();
            string rest = body.Length > keyword.Length ? body[keyword.Length..].Trim() : string.Empty;
            switch (keyword)
            {
                case "if" when conditions.Count > 0: errors.Add("more than one [if ...]; put all conditions in one, split by commas"); break;
                case "if": conditions.AddRange(ParseConditions(rest, errors)); break;
                case "once" when rest.Length == 0: once = true; break;
                case "cooldown" when ParseDuration(rest) is { } length && length > TimeSpan.Zero && length <= MaxCooldown: cooldown = length; break;
                case "cooldown": errors.Add($"bad cooldown '{rest}' (use for example 30s, 11m or 2h, at most 24h)"); break;
                default: errors.Add($"unknown tag '[{body}]'"); break;
            }
        }
        if (TrailingTag().IsMatch(text))
            errors.Add("more than 4 tags on one line");
        if (tagged && text.Length == 0)
            errors.Add("a line with tags but no text");
        return tagged ? (text, new DialogueRule(conditions, once, cooldown)) : (line, null);
    }

    private static IEnumerable<DialogueCondition> ParseConditions(string text, List<string> errors)
    {
        string[] terms = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            errors.Add("[if] with no condition");
        if (terms.Length > MaxConditions)
            errors.Add($"more than {MaxConditions} conditions on one line");
        foreach (string term in terms.Take(MaxConditions))
        {
            DialogueCondition condition = ParseCondition(term.ToLowerInvariant(), out string error);
            if (error != null)
                errors.Add(error);
            else
                yield return condition;
        }
    }

    private static DialogueCondition ParseCondition(string term, out string error)
    {
        error = null;
        Match match = Term().Match(term);
        string fact = match.Groups["fact"].Value;
        bool negated = match.Groups["not"].Success;
        if (!match.Success)
            error = $"can't read condition '{term}'";
        else if (YesNoFacts.Contains(fact))
        {
            if (match.Groups["op"].Success || match.Groups["value"].Success)
                error = $"'{fact}' takes no value: [if {fact}] or [if not {fact}]";
            else
                return new DialogueCondition(fact, negated, null, 0, null);
        }
        else if (negated)
            error = $"'not' only works with {string.Join(", ", YesNoFacts)}";
        else if (NumberFacts.Contains(fact))
        {
            if (!match.Groups["op"].Success || !int.TryParse(match.Groups["number"].Value, out int number) || number > MaxNumber)
                error = $"'{fact}' needs a comparison, for example [if {fact} >= 3]";
            else
                return new DialogueCondition(fact, false, match.Groups["op"].Value, number, null);
        }
        else if (ChoiceFacts.TryGetValue(fact, out IReadOnlySet<string> choices))
        {
            string value = match.Groups["value"].Value.Trim();
            if (!choices.Contains(value))
                error = $"'{fact}' needs one of: {string.Join(", ", choices)}";
            else
                return new DialogueCondition(fact, false, null, 0, value);
        }
        else if (TextFacts.Contains(fact))
        {
            string value = match.Groups["value"].Value.Trim();
            if (value.Length == 0 || match.Groups["op"].Success)
                error = $"'{fact}' needs a name, for example [if {fact} camelot city]";
            else
                return new DialogueCondition(fact, false, null, 0, value);
        }
        else
            error = $"unknown fact '{fact}'";
        return null;
    }

    private static TimeSpan? ParseDuration(string text)
    {
        Match match = Duration().Match(text.ToLowerInvariant());
        if (!match.Success || !int.TryParse(match.Groups["n"].Value, out int n) || n > MaxNumber)
            return null;
        return match.Groups["unit"].Value switch
        {
            "s" => TimeSpan.FromSeconds(n),
            "m" => TimeSpan.FromMinutes(n),
            _ => TimeSpan.FromHours(n),
        };
    }

    [GeneratedRegex(@"\s*\[(?<body>[^\[\]]*)\]\s*$")]
    private static partial Regex TrailingTag();

    [GeneratedRegex(@"^(?:(?<not>not)\s+)?(?<fact>[a-z_]+)(?:\s*(?<op>>=|<=|!=|=|>|<)\s*(?<number>\d+)|\s+(?<value>.+))?$")]
    private static partial Regex Term();

    [GeneratedRegex(@"^(?<n>\d+)\s*(?<unit>[smh])$")]
    private static partial Regex Duration();
}

/// <summary>
/// What a bot knows about the player it is talking to when it picks a line. Null facts are unknown:
/// a condition on an unknown fact is false. Built by <see cref="DialogueFactSource"/> in game; tests
/// fill it in directly.
/// </summary>
public sealed class DialogueFacts
{
    /// <summary>Who is speaking and to whom; keys for [once], cooldowns and recent lines.</summary>
    public string BotId { get; init; }
    public string PlayerId { get; init; }

    public bool? PlayerDead { get; init; }
    public bool? InCombat { get; init; }
    public int? Health { get; init; }
    public int? GroupSize { get; init; }
    public int? DeathsToday { get; init; }
    public string Mood { get; init; }
    public string TimeOfDay { get; init; }
    public string Zone { get; init; }
    public IReadOnlySet<string> Recent { get; init; }
    public IReadOnlySet<string> Nearby { get; init; }

    public bool Holds(DialogueCondition condition) => condition.Fact switch
    {
        "player_dead" => PlayerDead is { } dead && dead != condition.Negated,
        "in_combat" => InCombat is { } fighting && fighting != condition.Negated,
        "health" => Compare(Health, condition),
        "group_size" => Compare(GroupSize, condition),
        "deaths_today" => Compare(DeathsToday, condition),
        "mood" => Mood == condition.Value,
        "time_of_day" => TimeOfDay == condition.Value,
        "zone" => string.Equals(Zone, condition.Value, StringComparison.OrdinalIgnoreCase),
        "recent" => Recent?.Contains(condition.Value) == true,
        "nearby" => Nearby?.Contains(condition.Value) == true,
        _ => false,
    };

    private static bool Compare(int? fact, DialogueCondition condition) => fact is { } value && condition.Operator switch
    {
        ">=" => value >= condition.Number,
        "<=" => value <= condition.Number,
        ">" => value > condition.Number,
        "<" => value < condition.Number,
        "!=" => value != condition.Number,
        _ => value == condition.Number,
    };

    /// <summary>The time of day for a game clock in milliseconds since game midnight.</summary>
    public static string TimeOfDayFor(uint gameTime) => (gameTime / 3_600_000 % 24) switch
    {
        >= 6 and < 12 => "morning",
        >= 12 and < 18 => "day",
        >= 18 and < 22 => "evening",
        _ => "night",
    };
}

/// <summary>
/// Picks a line from a pool by the rules (docs/DIALOGUE-DESIGN.md, "How a line is picked"):
/// drop lines whose conditions are false, [once] lines already said to this player and lines on
/// cooldown; keep the most specific; skip this bot's recent lines where possible; pick at random.
/// What it remembers lives in memory only, so it starts fresh when the server restarts.
/// </summary>
public sealed class DialoguePicker
{
    /// <summary>Lines looked at per pick, lines remembered as recent per bot, and entries remembered at most.</summary>
    public const int MaxPool = 2000;
    public const int RecentPerBot = 4;
    public const int MaxRemembered = 20000;

    public static readonly DialoguePicker Shared = new();

    private readonly object _lock = new();
    private readonly HashSet<string> _saidOnce = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _cooldownUntil = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _recent = new(StringComparer.Ordinal);

    /// <summary>A line from the pool, or null when none is allowed. Remembers it as said.</summary>
    public string Pick(IReadOnlyList<string> pool, Func<string, DialogueRule> ruleOf, DialogueFacts facts, string botKey, DateTime now, Random random)
    {
        if (pool is not { Count: > 0 })
            return null;
        random ??= Random.Shared;
        botKey ??= string.Empty;
        string playerKey = facts?.PlayerId ?? string.Empty;
        lock (_lock)
        {
            var allowed = new List<(string Line, DialogueRule Rule)>();
            int best = -1;
            foreach (string line in pool.Take(MaxPool))
            {
                DialogueRule rule = ruleOf?.Invoke(line);
                if (rule != null && !Allowed(rule, line, facts, botKey, playerKey, now))
                    continue;
                int specificity = rule?.Specificity ?? 0;
                if (specificity > best)
                {
                    best = specificity;
                    allowed.Clear();
                }
                if (specificity == best)
                    allowed.Add((line, rule));
            }
            if (allowed.Count == 0)
                return null;

            List<string> recent = _recent.GetValueOrDefault(botKey);
            var fresh = recent == null ? allowed : allowed.Where(candidate => !recent.Contains(candidate.Line)).ToList();
            (string line, DialogueRule rule) picked = (fresh.Count > 0 ? fresh : allowed)[random.Next(fresh.Count > 0 ? fresh.Count : allowed.Count)];
            Remember(picked.line, picked.rule, botKey, playerKey, now);
            return picked.line;
        }
    }

    /// <summary>Forgets everything said (tests).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _saidOnce.Clear();
            _cooldownUntil.Clear();
            _recent.Clear();
        }
    }

    private bool Allowed(DialogueRule rule, string line, DialogueFacts facts, string botKey, string playerKey, DateTime now)
    {
        if (rule.Conditions.Count > 0 && (facts == null || !rule.Conditions.All(facts.Holds)))
            return false;
        if (rule.Once && _saidOnce.Contains(OnceKey(botKey, playerKey, line)))
            return false;
        return rule.Cooldown <= TimeSpan.Zero || !_cooldownUntil.TryGetValue(CooldownKey(botKey, line), out DateTime until) || now >= until;
    }

    private void Remember(string line, DialogueRule rule, string botKey, string playerKey, DateTime now)
    {
        // A full store is emptied rather than grown: forgetting only lets a line come back early.
        if (_saidOnce.Count >= MaxRemembered)
            _saidOnce.Clear();
        if (_cooldownUntil.Count >= MaxRemembered)
            _cooldownUntil.Clear();
        if (_recent.Count >= MaxRemembered)
            _recent.Clear();

        if (rule?.Once == true)
            _saidOnce.Add(OnceKey(botKey, playerKey, line));
        if (rule?.Cooldown > TimeSpan.Zero)
            _cooldownUntil[CooldownKey(botKey, line)] = now + rule.Cooldown;
        if (!_recent.TryGetValue(botKey, out List<string> recent))
            _recent[botKey] = recent = [];
        recent.Remove(line);
        recent.Add(line);
        if (recent.Count > RecentPerBot)
            recent.RemoveAt(0);
    }

    private static string OnceKey(string botKey, string playerKey, string line) => botKey + "\n" + playerKey + "\n" + line;

    private static string CooldownKey(string botKey, string line) => botKey + "\n" + line;
}
