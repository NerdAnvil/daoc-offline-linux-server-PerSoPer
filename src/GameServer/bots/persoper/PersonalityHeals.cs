using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DOL.AI.Brain;

namespace DOL.GS;

/// <summary>
/// Heals on request. A player asks a healer in their group ("small heal", "big heal on
/// Bob", "HELP!") and it casts the matching single-target heal: its cheapest for small,
/// its most efficient for medium, its biggest for big, and a reflex heal (instant if it
/// has one) for panic. Persoper answers in her own voice; plain healer companions use
/// neutral lines. The rules are pure and unit tested; the casting lives in
/// BotBrain.RequestedHeal.cs.
/// </summary>
public static partial class PersonalityHeals
{
    public enum eHealSize
    {
        /// <summary>No size given: chosen from the target's health.</summary>
        Auto,
        Small,
        Medium,
        Big,
        /// <summary>A panicked call: the fastest big heal, right now.</summary>
        Panic,
    }

    public enum eHealOutcome
    {
        Cast,
        Approaching,
        NoHeal,
        NoPower,
        Cooldown,
        CantCast,
        Busy,
        OutOfReach,
    }

    /// <summary>A heal request: how big, and who for (null target word = whoever asked).</summary>
    public sealed record HealRequest(eHealSize Size, string TargetWord);

    /// <summary>One heal a bot can cast, as the choice sees it.</summary>
    public sealed record HealOption<T>(T Spell, int Cost, double Amount, bool Instant);

    /// <summary>Requests at full health within this window that try a personality's patience.</summary>
    public const int SpamCount = 3;
    public static readonly TimeSpan SpamWindow = TimeSpan.FromMinutes(1);

    // A heal request has a request shape; the word "heal" alone is not enough ("don't heal",
    // "nice heal", "your heal was late").

    [GeneratedRegex(@"\b(don[\u2019']?t|do\s+not|doesn[\u2019']?t|didn[\u2019']?t|won[\u2019']?t|no|not|never|stop|quit|without|cancel)\b(\W+\w+){0,3}?\W+(heal|top)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Negated();

    [GeneratedRegex(@"^(\W*(hey|hi|oi|yo|ok|okay)\b)?(\W*(?!heal\b)\w+\s*[,:])?", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingAddress();

    [GeneratedRegex(@"^(\W*(please|pls|plz|quick|quickly|now|ok|okay|and|then)\b)*\W*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingPoliteness();

    [GeneratedRegex(@"^heal\b(?!\s+(was|is|did|went|landed|crit|crits|hit|hits|for)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Imperative();

    [GeneratedRegex(@"^((a|an|one|some)\s+)?(small|little|tiny|quick|medium|normal|regular|decent|big|large|huge|full|massive)\s+heal\b", RegexOptions.IgnoreCase)]
    private static partial Regex SizedHeal();

    [GeneratedRegex(@"\b(give|get|need|want|send|throw|toss|cast|spare|could\s+use|can\s+i\s+(get|have)|may\s+i\s+have)\b(\W+\w+){0,4}?\W+heal\b", RegexOptions.IgnoreCase)]
    private static partial Regex AskForHeal();

    [GeneratedRegex(@"\b(can|could|would|will)\s+(you|u)\s+(please\s+)?heal\b", RegexOptions.IgnoreCase)]
    private static partial Regex CanYouHeal();

    [GeneratedRegex(@"\btop\s+(\w+)\s+off\b", RegexOptions.IgnoreCase)]
    private static partial Regex TopOff();

    [GeneratedRegex(@"\b(help|ack|argh|eek|a+h{2,})\b", RegexOptions.IgnoreCase)]
    private static partial Regex PanicWord();

    [GeneratedRegex(@"\bon me\b", RegexOptions.IgnoreCase)]
    private static partial Regex OnMe();

    [GeneratedRegex(@"\bheal\s+(me\s+)?now\b", RegexOptions.IgnoreCase)]
    private static partial Regex HealNow();

    [GeneratedRegex(@"\bheal\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AfterHeal();

    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "me", "us", "please", "plz", "pls", "a", "an", "the", "now", "on", "for", "to", "quick", "quickly",
        "small", "little", "tiny", "medium", "normal", "regular", "big", "large", "huge", "full", "massive",
        "heal", "real", "really", "some", "up", "persoper", "again", "thanks", "thx", "ty",
    };

    /// <summary>
    /// Reads a chat line as a heal request, or null when it is not one. <paramref name="names"/>
    /// are healer names a line may start with ("Persoper heal me").
    /// </summary>
    public static HealRequest Parse(string message, IEnumerable<string> names = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        string text = message.Trim();
        bool exclaim = text.Contains('!');
        int letters = text.Count(char.IsLetter);
        bool shouting = letters >= 3 && text.Where(char.IsLetter).All(char.IsUpper);
        bool negated = Negated().IsMatch(text);
        bool healRequest = !negated && IsHealRequest(text, names);
        Match topOff = negated ? Match.Empty : TopOff().Match(text);

        bool panic = (PanicWord().IsMatch(text) || OnMe().IsMatch(text)) && (exclaim || shouting) ||
                     healRequest && (HealNow().IsMatch(text) || shouting || text.Contains("!!"));
        if (!panic && !healRequest && !topOff.Success)
            return null;

        string target = topOff.Success ? Normalise(topOff.Groups[1].Value) : TargetAfterHeal(text);
        if (panic)
            return new HealRequest(eHealSize.Panic, target);

        string lower = text.ToLowerInvariant();
        eHealSize size =
            topOff.Success || ContainsWord(lower, "small", "little", "tiny", "quick") ? eHealSize.Small
            : ContainsWord(lower, "big", "large", "huge", "full", "massive") ? eHealSize.Big
            : ContainsWord(lower, "medium", "normal", "regular", "decent") ? eHealSize.Medium
            : eHealSize.Auto;
        return new HealRequest(size, target);
    }

    /// <summary>Turns Auto into a size from the target's health: under 50% big, under 90% medium, else small.</summary>
    public static eHealSize Resolve(eHealSize size, int healthPercent) => size != eHealSize.Auto ? size
        : healthPercent < 50 ? eHealSize.Big
        : healthPercent < 90 ? eHealSize.Medium
        : eHealSize.Small;

    /// <summary>
    /// Picks the heal for a (resolved) size: Small = cheapest, Medium = most healing per
    /// power, Big = most healing, Panic = the biggest instant heal, else the biggest heal.
    /// </summary>
    public static T Choose<T>(IReadOnlyList<HealOption<T>> options, eHealSize size)
    {
        if (options == null || options.Count == 0)
            return default;
        IEnumerable<HealOption<T>> pick = size switch
        {
            eHealSize.Small => options.OrderBy(o => o.Cost).ThenBy(o => o.Amount),
            eHealSize.Medium => options.OrderByDescending(o => o.Amount / Math.Max(1, o.Cost)).ThenByDescending(o => o.Amount),
            eHealSize.Panic when options.Any(o => o.Instant) => options.Where(o => o.Instant).OrderByDescending(o => o.Amount),
            _ => options.OrderByDescending(o => o.Amount).ThenBy(o => o.Cost),
        };
        return pick.First().Spell;
    }

    /// <summary>Notes a request at full health; true (and the count resets) when it reaches the spam limit.</summary>
    public static bool CountFullHealthRequest(List<DateTime> recent, DateTime now)
    {
        recent.RemoveAll(at => now - at > SpamWindow);
        recent.Add(now);
        if (recent.Count < SpamCount)
            return false;
        recent.Clear();
        return true;
    }

    /// <summary>
    /// True for the request shapes: "heal me", "Persoper, heal Bob", "small heal", "give me
    /// a heal", "can you heal me?". Ordinary talk that only mentions a heal is not one.
    /// </summary>
    private static bool IsHealRequest(string text, IEnumerable<string> names)
    {
        if (AskForHeal().IsMatch(text) || CanYouHeal().IsMatch(text))
            return true;
        string body = LeadingAddress().Replace(text, "", 1);
        foreach (string name in names ?? new[] { "persoper" })
        {
            if (!string.IsNullOrEmpty(name) && body.TrimStart().StartsWith(name + " ", StringComparison.OrdinalIgnoreCase))
            {
                body = body.TrimStart()[name.Length..];
                break;
            }
        }
        body = LeadingPoliteness().Replace(body, "", 1);
        return Imperative().IsMatch(body) || SizedHeal().IsMatch(body);
    }

    private static string TargetAfterHeal(string text)
    {
        Match after = AfterHeal().Match(text);
        if (!after.Success)
            return null;
        foreach (string raw in after.Groups[1].Value.Split([' ', ',', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Normalise(raw);
            if (word != null)
                return word;
        }
        return null;
    }

    private static string Normalise(string word)
    {
        string letters = new(word.Where(char.IsLetter).ToArray());
        return letters.Length == 0 || Fillers.Contains(letters) ? null : letters.ToLowerInvariant();
    }

    private static bool ContainsWord(string lower, params string[] words) =>
        words.Any(word => Regex.IsMatch(lower, $@"\b{word}\b"));

    // --- What healers say ---

    /// <summary>The neutral voice of plain healer companions.</summary>
    public static readonly HealVoice Neutral = new()
    {
        Small = ["Small heal on {0}."],
        Medium = ["Healing {0}."],
        Big = ["Big heal on {0}."],
        Panic = ["Hold on, {0}!"],
        Approaching = ["Moving closer to heal {0}."],
        NoPower = ["Out of power."],
        Cooldown = ["My heals are on cooldown."],
        CantCast = ["I can't cast right now."],
        Busy = ["Already healing, one moment."],
        NoHeal = ["I have no heal for that."],
        OutOfReach = ["I can't reach {0}."],
        UnknownTarget = ["Who is {0}?"],
    };

    public static string Line(HealVoice voice, eHealOutcome outcome, eHealSize size, string targetName, Random random)
    {
        voice ??= Neutral;
        random ??= Random.Shared;
        string[] lines = outcome switch
        {
            eHealOutcome.Cast => size switch
            {
                eHealSize.Small => voice.Small,
                eHealSize.Big => voice.Big,
                eHealSize.Panic => voice.Panic,
                _ => voice.Medium,
            },
            eHealOutcome.Approaching => voice.Approaching,
            eHealOutcome.NoPower => voice.NoPower,
            eHealOutcome.Cooldown => voice.Cooldown,
            eHealOutcome.Busy => voice.Busy,
            eHealOutcome.NoHeal => voice.NoHeal,
            eHealOutcome.OutOfReach => voice.OutOfReach,
            _ => voice.CantCast,
        };
        return string.Format(lines[random.Next(lines.Length)], targetName);
    }

    // --- In the running server ---

    private static readonly object Sync = new();
    private static readonly Dictionary<string, List<DateTime>> FullHealthRequests = new();
    private const int MaxTracked = 500;

    /// <summary>
    /// Handles a chat line if it is a heal request for a healer in the player's group.
    /// <paramref name="addressed"/> is the bot a tell or whisper went to (null for group or
    /// /say); <paramref name="channel"/> null means /say. True when it was handled, so the
    /// normal chat reply is skipped.
    /// </summary>
    public static bool TryHandle(GamePlayer player, string message, GameBot addressed, AutonomousBotChatCoordinator.ePrivateChannel? channel)
    {
        if (player?.Group == null || string.IsNullOrWhiteSpace(message))
            return false;
        GameBot[] healers = Healers(player).ToArray();
        if (healers.Length == 0)
            return false;
        HealRequest request = Parse(message, healers.Select(bot => bot.Name.Split(' ')[0]).Append("persoper"));
        if (request == null)
            return false;

        if (channel == null)
            healers = healers.Where(bot => bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE)).ToArray();
        GameBot healer = addressed != null
            ? healers.FirstOrDefault(bot => bot == addressed)
            : healers.FirstOrDefault(bot => AutonomousBotChatCoordinator.IsMentionedPublic(message, bot))
              ?? healers.FirstOrDefault(bot => BotPersonalities.For(bot) != null)
              ?? healers.OrderBy(bot => bot.GetDistanceTo(player)).FirstOrDefault();
        if (healer?.Brain is not BotBrain brain)
            return false;

        BotPersonality personality = BotPersonalities.For(healer);
        HealVoice voice = personality?.Heals ?? Neutral;
        GameLiving target = FindTarget(player, healer, request.TargetWord);
        if (target == null)
        {
            Reply(healer, player, channel, string.Format(voice.UnknownTarget[Random.Shared.Next(voice.UnknownTarget.Length)], request.TargetWord));
            return true;
        }

        eHealSize size = Resolve(request.Size, target.HealthPercent);
        eHealOutcome outcome = brain.CastRequestedHeal(size, target, size == eHealSize.Panic);
        Reply(healer, player, channel, Line(voice, outcome, size, target == player ? player.Name : target.Name, Random.Shared));

        if (target.HealthPercent >= 100 && personality?.Patience != null && TooManyAtFullHealth(personality, player))
            PersonalityAnnoyance.Irritate(player, healer, PersonalityAnnoyance.eIrritation.HealSpam);
        return true;
    }

    /// <summary>Healer-class bots in the player's group that take heal requests: Persoper and healer companions.</summary>
    public static IEnumerable<GameBot> Healers(GamePlayer player) =>
        player?.Group == null ? [] : player.Group.GetMembersInTheGroup().OfType<GameBot>()
            .Where(bot => bot.IsAlive && bot.ObjectState == GameObject.eObjectState.Active && bot.CanCastHealSpells &&
                          bot.CharacterClass != null && BotPartyRoles.IsHealingClass((eCharacterClass)bot.CharacterClass.ID) &&
                          (bot.IsTemporaryGroupHelper || BotPersonalities.For(bot) != null));

    private static GameLiving FindTarget(GamePlayer player, GameBot healer, string word)
    {
        if (word == null)
            return player;
        GameLiving[] members = player.Group.GetMembersInTheGroup().Where(member => member.IsAlive).ToArray();
        if (word == "tank")
        {
            return members.OfType<GameBot>().FirstOrDefault(BotPartyRoles.IsTank)
                ?? members.Where(member => member != healer).OrderByDescending(member => member.MaxHealth).FirstOrDefault();
        }
        if (word is "yourself" or "you" or "self")
            return healer;
        return word.Length < 3 ? null
            : members.FirstOrDefault(member => member.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase) ||
                                               member.Name.Contains(" " + word, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TooManyAtFullHealth(BotPersonality personality, GamePlayer player)
    {
        string key = PersonalityMemoryRecord.KeyFor(personality.Key, player.InternalID);
        lock (Sync)
        {
            if (!FullHealthRequests.TryGetValue(key, out List<DateTime> recent))
            {
                if (FullHealthRequests.Count >= MaxTracked)
                    FullHealthRequests.Clear();
                recent = [];
                FullHealthRequests[key] = recent;
            }
            return CountFullHealthRequest(recent, DateTime.UtcNow);
        }
    }

    private static void Reply(GameBot healer, GamePlayer player, AutonomousBotChatCoordinator.ePrivateChannel? channel, string line)
    {
        if (channel is { } private_)
            AutonomousBotChatCoordinator.Deliver(healer, player, private_, line);
        else
            healer.Say(line);
    }
}

/// <summary>What a healer says when asked for a heal; {0} is who it heals (or the unknown name).</summary>
public sealed class HealVoice
{
    public required string[] Small { get; init; }
    public required string[] Medium { get; init; }
    public required string[] Big { get; init; }
    public required string[] Panic { get; init; }
    public required string[] Approaching { get; init; }
    public required string[] NoPower { get; init; }
    public required string[] Cooldown { get; init; }
    public required string[] CantCast { get; init; }
    public required string[] Busy { get; init; }
    public required string[] NoHeal { get; init; }
    public required string[] OutOfReach { get; init; }
    public required string[] UnknownTarget { get; init; }
}
