using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>What kind of joke a player's line is, for the joke flow.</summary>
public enum eJokeKind
{
    /// <summary>Not one the flow handles: ordinary chat, "just kidding", "want to hear a joke".</summary>
    None,
    /// <summary>A setup waiting for its punchline: "why did the troll cross the road?"</summary>
    Riddle,
    /// <summary>"knock knock": who's there, then "X who?", then the punchline.</summary>
    KnockKnock,
    /// <summary>A joke with its punchline: "why did the astronaut leave Pluto? It had no atmosphere".</summary>
    Told,
    /// <summary>Asking for one: "tell me a joke".</summary>
    Ask,
}

/// <summary>Where an open joke stands: the bot waits for the punchline, or for the knock-knock name.</summary>
public enum eJokeStage
{
    Punchline,
    KnockName,
}

/// <summary>An open joke: the bot that answered its setup, where it stands, and the knock-knock name.</summary>
public sealed record OpenJoke(GameBot Bot, eJokeStage Stage, DateTime Expires);

/// <summary>
/// Players telling bots jokes (intent Part B3a). A riddle gets one bot's guess at the punchline (or a
/// "why?"), and the player's next line is the punchline: a groan or a laugh. "knock knock" gets "Who's
/// there?", the name gets "X who?", and the punchline a groan or laugh. A joke told with its punchline
/// is groaned or laughed at straight away; "tell me a joke" gets one of the bot's own. A player has
/// one open joke at a time; it drops after <see cref="Timeout"/> without a word.
/// </summary>
public static partial class PersonalityJokes
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Words a punchline may have, a knock-knock name may have, and open jokes kept at most.</summary>
    public const int MaxPunchlineWords = 20;
    public const int MaxNameWords = 4;
    public const int MaxOpen = 200;

    /// <summary>How often a punchline gets a groan rather than a laugh.</summary>
    public const double GroanChance = 0.6;

    private static readonly Dictionary<string, OpenJoke> Open = new(StringComparer.Ordinal);
    private static readonly object Lock = new();

    // Lines that set up a riddle without a question mark ("why did the chicken cross the road").
    private static readonly string[][] RiddleStarts =
    [
        ["why", "did"], ["why", "do"], ["why", "don't"], ["why", "does"], ["why", "is"], ["why", "are"],
        ["what", "do", "you", "call"], ["what", "did", "the"], ["what's", "the", "difference"],
        ["what", "is", "the", "difference"], ["how", "do", "you", "make"], ["want", "to", "know", "why"],
        ["know", "why", "the"], ["guess", "why"],
    ];

    // Words a joke line needs, without a question mark, to be a joke told in full ("a dwarf walks into a bar...").
    public const int MinToldWords = 6;

    // A request, which a punchline never is ("anyone want to group?"). Game words alone are fine:
    // "to get to the alb!" reads as travel and is a punchline.
    private static readonly HashSet<eAutonomousChatIntent> NotAPunchline =
    [
        eAutonomousChatIntent.Grouping, eAutonomousChatIntent.Help, eAutonomousChatIntent.Command,
        eAutonomousChatIntent.CombatCall, eAutonomousChatIntent.Abuse, eAutonomousChatIntent.JokeAsk,
    ];

    /// <summary>The kind of joke a line is. <paramref name="read"/> is AutonomousChatIntentModel.Read of it.</summary>
    public static eJokeKind Kind(string message, eAutonomousChatIntent read)
    {
        if (read == eAutonomousChatIntent.JokeAsk)
            return eJokeKind.Ask;
        if (read != eAutonomousChatIntent.Joke || string.IsNullOrWhiteSpace(message))
            return eJokeKind.None;
        string[] tokens = AutonomousChatIntentModel.Tokens(message).ToArray();
        if (StartsAt(tokens, ["knock", "knock"]) >= 0)
            return eJokeKind.KnockKnock;
        int question = message.LastIndexOf('?');
        if (question >= 0)
            return AutonomousChatIntentModel.Tokens(message[(question + 1)..]).Count() >= 2 ? eJokeKind.Told : eJokeKind.Riddle;
        // No question mark: a riddle when it starts like one ("why did the troll cross the road"), a told
        // joke when it is long enough ("a dwarf walks into a bar..."), else talk about jokes ("jk").
        if (RiddleStarts.Any(start => StartsAt(tokens, start) is >= 0 and <= 2))
            return eJokeKind.Riddle;
        return tokens.Length >= MinToldWords && !tokens.Contains("kidding") ? eJokeKind.Told : eJokeKind.None;
    }

    /// <summary>
    /// The bot's first line to a joke, or null when it has none: a guess for a riddle, "Who's there?" for a
    /// knock-knock, a groan or laugh for a told joke, one of its jokes when asked. <paramref name="tellsJoke"/>
    /// gives the bot's own jokes from its type (PersonalityReactions.TopicOpener) when it has no "~ reply jokeask".
    /// </summary>
    public static string Opening(BotPersonality personality, eJokeKind kind, string speakerName, Random random, DialogueFacts facts = null, Func<string> tellsJoke = null)
    {
        if (personality == null)
            return null;
        random ??= Random.Shared;
        return kind switch
        {
            eJokeKind.Riddle => personality.OwnReply(eAutonomousChatIntent.JokeGuess, speakerName, random, facts),
            eJokeKind.KnockKnock => "Who's there?",
            eJokeKind.Told => Punchline(personality, speakerName, random, facts),
            eJokeKind.Ask => personality.OwnReply(eAutonomousChatIntent.JokeAsk, speakerName, random, facts) ?? tellsJoke?.Invoke(),
            _ => null,
        };
    }

    /// <summary>A groan (more often) or a laugh at a punchline, from the bot's own lines; its joke replies when it has neither.</summary>
    public static string Punchline(BotPersonality personality, string speakerName, Random random, DialogueFacts facts = null)
    {
        if (personality == null)
            return null;
        random ??= Random.Shared;
        (eAutonomousChatIntent first, eAutonomousChatIntent second) = random.NextDouble() < GroanChance
            ? (eAutonomousChatIntent.Groan, eAutonomousChatIntent.JokeLaugh)
            : (eAutonomousChatIntent.JokeLaugh, eAutonomousChatIntent.Groan);
        return personality.OwnReply(first, speakerName, random, facts)
            ?? personality.OwnReply(second, speakerName, random, facts)
            ?? personality.OwnReply(eAutonomousChatIntent.Joke, speakerName, random, facts);
    }

    /// <summary>"Boo who?": the knock-knock name with a capital, its punctuation off. Null for a line that isn't a short name.</summary>
    public static string KnockWho(string name)
    {
        string trimmed = Trailing().Replace(name?.Trim() ?? string.Empty, string.Empty);
        if (trimmed.Length == 0 || trimmed.Length > 40 || AutonomousChatIntentModel.Tokens(trimmed).Count() > MaxNameWords)
            return null;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..] + " who?";
    }

    /// <summary>True when a line can be the answer the open joke waits for: short, and not a game request or a new joke.</summary>
    public static bool CanAnswer(eJokeStage stage, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        int words = AutonomousChatIntentModel.Tokens(message).Count();
        if (stage == eJokeStage.KnockName)
            return KnockWho(message) != null;
        eAutonomousChatIntent read = AutonomousChatIntentModel.Read(message);
        return words <= MaxPunchlineWords && !NotAPunchline.Contains(read) && Kind(message, read) is not (eJokeKind.Riddle or eJokeKind.KnockKnock);
    }

    /// <summary>Opens a joke for a player (a riddle waiting for its punchline, a knock-knock for its name). False when the store is full.</summary>
    public static bool Start(string playerId, GameBot bot, eJokeStage stage, DateTime now)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        lock (Lock)
        {
            foreach (string expired in Open.Where(entry => now >= entry.Value.Expires).Select(entry => entry.Key).ToList())
                Open.Remove(expired);
            if (!Open.ContainsKey(playerId) && Open.Count >= MaxOpen)
                return false;
            Open[playerId] = new OpenJoke(bot, stage, now + Timeout);
            return true;
        }
    }

    /// <summary>
    /// The player's open joke when <paramref name="message"/> answers it (it is then closed); else null.
    /// With <paramref name="toBot"/> (a tell or whisper), only that bot's joke.
    /// </summary>
    public static OpenJoke TryTake(string playerId, string message, DateTime now, GameBot toBot = null)
    {
        if (string.IsNullOrEmpty(playerId))
            return null;
        lock (Lock)
        {
            if (!Open.TryGetValue(playerId, out OpenJoke open))
                return null;
            if (now >= open.Expires)
            {
                Open.Remove(playerId);
                return null;
            }
            if (toBot != null && open.Bot != toBot || !CanAnswer(open.Stage, message))
                return null;
            Open.Remove(playerId);
            return open;
        }
    }

    /// <summary>Forgets every open joke (tests).</summary>
    public static void Clear()
    {
        lock (Lock)
            Open.Clear();
    }

    private static int StartsAt(string[] tokens, string[] words)
    {
        for (int start = 0; start + words.Length <= tokens.Length; start++)
            if (words.Select((word, index) => tokens[start + index] == word).All(match => match))
                return start;
        return -1;
    }

    [GeneratedRegex(@"[\s.!?,]+$")]
    private static partial Regex Trailing();
}
