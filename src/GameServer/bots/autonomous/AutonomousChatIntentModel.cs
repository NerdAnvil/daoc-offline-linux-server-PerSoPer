using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

public enum eAutonomousChatIntent
{
    Unknown,
    Greeting,
    Banter,
    Abuse,
    Grouping,
    Grinding,
    Travel,
    Dungeon,
    RvR,
    Trading,
    Help,
    Farewell,
    IgnoreOutOfWorld,
    // Added last so the others keep their numbers. Found only by the words in intents.dlg.
    Thank,
    // Small talk, also found only by intents.dlg (or, bot to bot, by the opener's topic).
    Joke,
    Grumble,
    SmallTalk,
    Question,
    Compliment,
    Agree,
    Quote,
    // Read by Read() only; Predict() turns each into one of the intents above (Collapse) until
    // bots have replies for them. JokeAsk is asking for a joke; Joke is telling one.
    JokeAsk,
    Laugh,
    Sorry,
    Disagree,
    Away,
    Activity,
    HowGoing,
    Mood,
    Flirt,
    OutOfWorldChat,
    Command,
    CombatCall,
    // "back" after a "brb": a different answer ("wb") from going away ("k").
    Back,
    // Reply kinds for the joke flow (PersonalityJokes), never read from chat: a guess at a riddle's
    // punchline, a groan or a laugh at the punchline, and the lead-in to telling one back.
    JokeGuess,
    Groan,
    JokeLaugh,
    JokeBack,
    // The rare walk-off gag after a bad joke (PersonalityWalkOff): "I'm out", then "I'm back". Never read from chat.
    JokeQuit,
    JokeReturn,
    // Questions about the bot itself, answered from its real state (PersonalityStateAnswers): where it is,
    // its level and class, whether it is in a group. "What are you doing" is Activity, above.
    Where,
    Level,
    GroupStatus,
    // An open call to group ("who wants to group?", "lfg"): only a marker for the phrases in intents.dlg. Read() still
    // gives Grouping, Dungeon or RvR for it; IsGroupCall() tells it apart from asking to join ("can I join?").
    GroupCall,
    // A personality bot's answers to a group call (PersonalityGroupCalls), never read from chat: yes, no, busy
    // (fighting or riding), already in a group, and the level gap.
    GroupYes,
    GroupNo,
    GroupBusy,
    GroupHave,
    GroupLevel,
    // Orders (PersonalityOrders): follow me, stay here, come here. Read() gives Command for their phrases in intents.dlg;
    // ReadOrder() tells which. The same names are the sections a bot says yes from, then its refusal, busy and
    // "not yours to order" answers. Never small talk. B6b: leave the group, assist, rest, and the answers when there is
    // nothing to assist on (notarget), it is full already (full) or it has stormed off after a bad joke (away).
    OrderFollow,
    OrderStay,
    OrderCome,
    OrderLeave,
    OrderAssist,
    OrderRest,
    OrderNo,
    OrderBusy,
    OrderNoTarget,
    OrderFull,
    OrderAway,
    OrderNot,
}

/// <summary>An order a player gives bots in chat (intent Part B6, B6b); None when the line isn't one.</summary>
public enum eBotOrder
{
    None,
    Follow,
    Stay,
    Come,
    Leave,
    Assist,
    Rest,
}

/// <summary>
/// Tiny multinomial Naive Bayes classifier trained from embedded DAoC chat
/// examples. It has no network, no generative model and no player-data training.
/// </summary>
public static partial class AutonomousChatIntentModel
{
    private sealed record Example(eAutonomousChatIntent Intent, string Text);
    private sealed record Model(Dictionary<eAutonomousChatIntent, Dictionary<string, int>> Counts,
        Dictionary<eAutonomousChatIntent, int> Totals, Dictionary<eAutonomousChatIntent, int> Documents,
        HashSet<string> Vocabulary, int DocumentCount);

    private static readonly Example[] Training =
    [
        new(eAutonomousChatIntent.Greeting, "hello hi hey greetings hail whats up how goes it good evening"),
        new(eAutonomousChatIntent.Greeting, "morning everyone hello realm hey folks how are you"),
        new(eAutonomousChatIntent.Greeting, "yo there hi friends good to see you"),
        new(eAutonomousChatIntent.Banter, "you guys suck terrible group bad pull weaklings"),
        new(eAutonomousChatIntent.Banter, "nice try you hit like a kobold that was rough"),
        new(eAutonomousChatIntent.Banter, "come on slowpokes is that all you have"),
        new(eAutonomousChatIntent.Abuse, "you are idiots morons garbage trash"),
        new(eAutonomousChatIntent.Abuse, "stupid loser shut up worthless terrible player"),
        new(eAutonomousChatIntent.Abuse, "everyone here is awful you are all pathetic"),
        new(eAutonomousChatIntent.Grouping, "looking for group need healer tank damage invite join party"),
        new(eAutonomousChatIntent.Grouping, "anybody grouping want to team up group has room"),
        new(eAutonomousChatIntent.Grouping, "forming party need help with harder mobs"),
        new(eAutonomousChatIntent.Grinding, "where are you grinding hunting mobs camp experience xp leveling"),
        new(eAutonomousChatIntent.Grinding, "good monster camp killing creatures gain levels"),
        new(eAutonomousChatIntent.Grinding, "anyone farming skeletons wolves bandits loot drops"),
        new(eAutonomousChatIntent.Travel, "where are you going how do I get there road route stable master"),
        new(eAutonomousChatIntent.Travel, "which way to town trainer capital horse ticket"),
        new(eAutonomousChatIntent.Travel, "help me find npc monster location travel"),
        new(eAutonomousChatIntent.Dungeon, "dungeon entrance cave tomb deeper inside careful pulls"),
        new(eAutonomousChatIntent.Dungeon, "want to run a dungeon clear the corridor"),
        new(eAutonomousChatIntent.Dungeon, "group for spraggon den cursed tomb vendo caves"),
        new(eAutonomousChatIntent.RvR, "rvr frontier keep raid enemies relic defense attack realm points"),
        new(eAutonomousChatIntent.RvR, "albion midgard hibernia fighting at the keep"),
        new(eAutonomousChatIntent.RvR, "any action in emain hadrians odins gate"),
        new(eAutonomousChatIntent.Trading, "buy sell price gear weapon armor auction exchange materials"),
        new(eAutonomousChatIntent.Trading, "does anyone have equipment for sale coins merchant"),
        new(eAutonomousChatIntent.Trading, "realm exchange listing item crafting material"),
        new(eAutonomousChatIntent.Help, "can anyone help me question confused how does this work"),
        new(eAutonomousChatIntent.Help, "need advice can somebody explain please help"),
        new(eAutonomousChatIntent.Help, "what should I do trainer skills equipment"),
        new(eAutonomousChatIntent.Farewell, "bye goodbye later see you safe travels good night"),
        new(eAutonomousChatIntent.Farewell, "got to go farewell heading off take care"),
        new(eAutonomousChatIntent.IgnoreOutOfWorld, "politics president election congress democrat republican government"),
        new(eAutonomousChatIntent.IgnoreOutOfWorld, "current news real world country politician vote campaign"),
        new(eAutonomousChatIntent.IgnoreOutOfWorld, "stocks crypto bitcoin market taxes"),
    ];

    private static readonly HashSet<string> HardOutOfWorld = new(StringComparer.OrdinalIgnoreCase)
    {
        "politics", "political", "president", "election", "congress", "democrat", "republican", "government",
        "trump", "biden", "bitcoin", "crypto", "stock", "stocks", "twitter", "facebook", "instagram", "tiktok",
        "reddit", "headline", "headlines", "news", "taxes",
    };

    // Insults are abuse wherever they stand ("thanks, idiot"); swearing alone is not ("holy shit, nice pull").
    private static readonly HashSet<string> Insults = new(StringComparer.OrdinalIgnoreCase)
    {
        "idiot", "idiots", "moron", "morons", "garbage", "worthless", "pathetic", "loser", "losers", "useless",
    };

    // These are abuse only when aimed at someone: "you're dumb", not "this quest is dumb".
    private static readonly HashSet<string> AimedInsults = new(StringComparer.OrdinalIgnoreCase)
    {
        "dumb", "stupid", "trash",
    };

    private static readonly HashSet<string> Aimed = new(StringComparer.OrdinalIgnoreCase)
    {
        "you", "you're", "your", "yourself",
    };

    // Words that say nothing about the intent; the word scores skip them, so "how old are you" isn't a greeting.
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "you", "you're", "your", "are", "is", "am", "be", "was", "it", "it's", "that", "that's", "this", "there",
        "here", "how", "how's", "what", "what's", "where", "where's", "why", "who", "when", "do", "does", "did", "have",
        "has", "to", "of", "and", "or", "in", "on", "at", "for", "with", "so", "all", "my", "me", "i'm", "we", "us", "our",
        "can", "will", "just", "up", "some", "any", "one", "get", "got", "go", "going", "want", "need", "on", "an", "if",
        "too", "way", "much", "many", "again", "now", "then", "they", "them", "he", "she", "his", "her", "doing", "later",
    };

    // Chat shorthand and dropped letters, read as the words they stand for. Matched before one-letter words are dropped.
    private static readonly Dictionary<string, string[]> Shorthand = new(StringComparer.OrdinalIgnoreCase)
    {
        ["u"] = ["you"], ["ya"] = ["you"], ["yer"] = ["your"], ["ur"] = ["your"], ["r"] = ["are"], ["k"] = ["ok"],
        ["kk"] = ["ok"], ["n"] = ["and"], ["im"] = ["i'm"], ["ive"] = ["i've"], ["dont"] = ["don't"],
        ["wont"] = ["won't"], ["cant"] = ["can't"], ["youre"] = ["you're"], ["thats"] = ["that's"],
        ["whats"] = ["what's"], ["hows"] = ["how's"], ["wheres"] = ["where's"], ["whos"] = ["who's"],
        ["wanna"] = ["want", "to"], ["gonna"] = ["going", "to"], ["gotta"] = ["got", "to"], ["lemme"] = ["let", "me"],
        ["watcha"] = ["what", "are", "you"], ["whatcha"] = ["what", "are", "you"],
        ["doin"] = ["doing"], ["goin"] = ["going"], ["headin"] = ["heading"], ["nothin"] = ["nothing"],
        ["sup"] = ["what's", "up"], ["wassup"] = ["what's", "up"], ["cya"] = ["see", "you"], ["cu"] = ["see", "you"],
        ["gn"] = ["good", "night"], ["nn"] = ["good", "night"], ["gg"] = ["good", "game"], ["wp"] = ["well", "played"],
        ["np"] = ["no", "problem"], ["omw"] = ["on", "my", "way"], ["mb"] = ["my", "bad"], ["sry"] = ["sorry"],
        ["pls"] = ["please"], ["plz"] = ["please"], ["plox"] = ["please"],
        ["grp"] = ["group"], ["grup"] = ["group"], ["inv"] = ["invite"], ["lf"] = ["looking", "for"],
        ["pl"] = ["power", "level"], ["lvl"] = ["level"], ["lvls"] = ["levels"], ["lvld"] = ["leveled"],
        ["lvled"] = ["leveled"], ["lvling"] = ["leveling"], ["exp"] = ["xp"], ["char"] = ["character"],
        ["toon"] = ["character"], ["skellies"] = ["skeletons"], ["skellys"] = ["skeletons"],
        ["wts"] = ["want", "to", "sell"], ["wtb"] = ["want", "to", "buy"], ["wtt"] = ["want", "to", "trade"],
        ["rps"] = ["realm", "points"], ["albs"] = ["albion"], ["alb"] = ["albion"], ["mids"] = ["midgard"],
        ["hibs"] = ["hibernia"], ["hib"] = ["hibernia"],
    };

    private static readonly IReadOnlyDictionary<eAutonomousChatIntent, HashSet<string>> StrongGameSignals =
        new Dictionary<eAutonomousChatIntent, HashSet<string>>
        {
            [eAutonomousChatIntent.Trading] = new(StringComparer.OrdinalIgnoreCase) { "buy", "sell", "price", "auction", "exchange", "merchant" },
            [eAutonomousChatIntent.Dungeon] = new(StringComparer.OrdinalIgnoreCase) { "dungeon", "tomb", "caves", "cave" },
            [eAutonomousChatIntent.RvR] = new(StringComparer.OrdinalIgnoreCase) { "rvr", "frontier", "relic", "emain" },
            [eAutonomousChatIntent.Grouping] = new(StringComparer.OrdinalIgnoreCase) { "group", "party", "invite", "lfg" },
        };

    // Inside an invite ("who wants to ..."), these say it's a dungeon or a frontier trip.
    private static readonly HashSet<string> InviteDungeon = new(StringComparer.OrdinalIgnoreCase) { "dungeon", "dungeons", "tomb", "caves", "cave", "lair", "den" };
    private static readonly HashSet<string> InviteRvR = new(StringComparer.OrdinalIgnoreCase)
    {
        "rvr", "frontier", "relic", "relics", "emain", "keep", "keeps", "albion", "midgard", "hibernia", "realm",
    };

    private static readonly Model Trained = Train();

    /// <summary>The intent as today's replies know it: Read(), with the newer intents turned into their nearest older one.</summary>
    public static eAutonomousChatIntent Predict(string text) => Collapse(Read(text));

    /// <summary>The nearest intent bots already have replies for; the older intents stay as they are.</summary>
    public static eAutonomousChatIntent Collapse(eAutonomousChatIntent intent) => intent switch
    {
        eAutonomousChatIntent.JokeAsk or eAutonomousChatIntent.Laugh => eAutonomousChatIntent.Joke,
        eAutonomousChatIntent.Activity or eAutonomousChatIntent.Mood => eAutonomousChatIntent.Question,
        eAutonomousChatIntent.HowGoing => eAutonomousChatIntent.Greeting,
        eAutonomousChatIntent.Flirt => eAutonomousChatIntent.Compliment,
        eAutonomousChatIntent.OutOfWorldChat => eAutonomousChatIntent.IgnoreOutOfWorld,
        eAutonomousChatIntent.CombatCall => eAutonomousChatIntent.Help,
        eAutonomousChatIntent.Sorry or eAutonomousChatIntent.Disagree or eAutonomousChatIntent.Away or
            eAutonomousChatIntent.Back or eAutonomousChatIntent.Command => eAutonomousChatIntent.Unknown,
        // Plain bots answer these from their task (BuildLocalReply); personality bots from their state lines.
        eAutonomousChatIntent.Where => eAutonomousChatIntent.Travel,
        eAutonomousChatIntent.GroupStatus => eAutonomousChatIntent.Grouping,
        eAutonomousChatIntent.Level => eAutonomousChatIntent.Question,
        _ => intent,
    };

    /// <summary>What a chat line means, with every intent the game knows (see Collapse for what bots answer today).</summary>
    public static eAutonomousChatIntent Read(string text)
    {
        string[] tokens = Tokens(text).ToArray();
        if (tokens.Length == 0)
            return eAutonomousChatIntent.Unknown;
        // "back" on its own is coming back; inside a line it's "back home" or "fall back".
        if (tokens is ["back"])
            return eAutonomousChatIntent.Back;
        if (tokens.Any(HardOutOfWorld.Contains))
            return eAutonomousChatIntent.IgnoreOutOfWorld;
        if (tokens.Any(Insults.Contains) || (tokens.Any(AimedInsults.Contains) && tokens.Any(Aimed.Contains)))
            return eAutonomousChatIntent.Abuse;
        // "stay", "wait", "follow", "come" on their own are orders.
        if (tokens.Length == 1 && LoneOrders.ContainsKey(tokens[0]))
            return eAutonomousChatIntent.Command;
        eAutonomousChatIntent? phrased = IntentPhrases.Shared.Match(tokens);
        if (phrased is { } ordered && IsOrderPhrase(ordered))
            return eAutonomousChatIntent.Command;
        // "Who wants to ..." is an invite; a dungeon or frontier word says which kind.
        if (phrased is eAutonomousChatIntent.Grouping or eAutonomousChatIntent.GroupCall)
            return tokens.Any(InviteDungeon.Contains) ? eAutonomousChatIntent.Dungeon
                : tokens.Any(InviteRvR.Contains) ? eAutonomousChatIntent.RvR
                : eAutonomousChatIntent.Grouping;
        if (phrased is { } found && !GameWordsWin(found))
            return found;
        // A game word beats small talk: "yeah, want to group?" is about grouping.
        foreach ((eAutonomousChatIntent intent, HashSet<string> signals) in StrongGameSignals)
        {
            if (tokens.Any(signals.Contains))
                return intent;
        }
        if (phrased is { } talk)
            return talk;
        string[] scored = tokens.Where(token => !Stopwords.Contains(token)).ToArray();
        // No word it was trained on: the scores would only compare example counts.
        if (!scored.Any(Trained.Vocabulary.Contains))
            return eAutonomousChatIntent.Unknown;

        eAutonomousChatIntent bestIntent = eAutonomousChatIntent.Unknown;
        double best = double.NegativeInfinity;
        double runnerUp = double.NegativeInfinity;
        foreach (eAutonomousChatIntent intent in Trained.Documents.Keys)
        {
            double score = Math.Log((Trained.Documents[intent] + 1d) /
                                    (Trained.DocumentCount + Trained.Documents.Count));
            Dictionary<string, int> counts = Trained.Counts[intent];
            double denominator = Trained.Totals[intent] + Trained.Vocabulary.Count;
            foreach (string token in scored)
                score += Math.Log((counts.GetValueOrDefault(token) + 1d) / denominator);
            if (score > best)
            {
                runnerUp = best;
                best = score;
                bestIntent = intent;
            }
            else if (score > runnerUp)
            {
                runnerUp = score;
            }
        }
        return best - runnerUp < 0.18 ? eAutonomousChatIntent.Unknown : bestIntent;
    }

    /// <summary>
    /// True for an open call to group ("who wants to group?", "anyone up for a dungeon?", "lfg"), which bots answer
    /// yes or no (PersonalityGroupCalls); false for asking to join ("can I join?", "inv pls") and everything else.
    /// </summary>
    public static bool IsGroupCall(string text)
    {
        if (Read(text) is not (eAutonomousChatIntent.Grouping or eAutonomousChatIntent.Dungeon or eAutonomousChatIntent.RvR))
            return false;
        return IntentPhrases.Shared.Has(Tokens(text).ToArray(), eAutonomousChatIntent.GroupCall);
    }

    // A line that is only one of these words (after the bot's name is taken off) is that order.
    private static readonly Dictionary<string, eBotOrder> LoneOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stay"] = eBotOrder.Stay, ["wait"] = eBotOrder.Stay, ["stop"] = eBotOrder.Stay, ["halt"] = eBotOrder.Stay,
        ["follow"] = eBotOrder.Follow, ["come"] = eBotOrder.Come,
        ["assist"] = eBotOrder.Assist, ["rest"] = eBotOrder.Rest, ["sit"] = eBotOrder.Rest, ["med"] = eBotOrder.Rest,
    };

    /// <summary>The intents.dlg sections that hold order phrases (orderfollow ... orderrest), each read as Command.</summary>
    public static bool IsOrderPhrase(eAutonomousChatIntent intent) =>
        intent is >= eAutonomousChatIntent.OrderFollow and <= eAutonomousChatIntent.OrderRest;

    /// <summary>
    /// The order in a chat line ("follow me", "shemp, stay here", "come here", "leave the group", "assist me", "rest up",
    /// or one of stay, wait, stop, follow, come, assist, rest, sit, med on its own); None for anything else. <paramref name="names"/> are the words that name the bots (full names,
    /// personality names, nicknames): taken off first, so "shemp stay" is a lone "stay". A rude line is never an order.
    /// </summary>
    public static eBotOrder ReadOrder(string text, IEnumerable<string> names = null)
    {
        if (Read(text) is eAutonomousChatIntent.Abuse or eAutonomousChatIntent.IgnoreOutOfWorld)
            return eBotOrder.None;
        HashSet<string> named = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in (names ?? []).Take(64))
            named.UnionWith(Tokens(name));
        string[] tokens = Tokens(text).Where(token => !named.Contains(token)).ToArray();
        if (tokens.Length == 1 && LoneOrders.TryGetValue(tokens[0], out eBotOrder lone))
            return lone;
        return IntentPhrases.Shared.Match(tokens) switch
        {
            eAutonomousChatIntent.OrderFollow => eBotOrder.Follow,
            eAutonomousChatIntent.OrderStay => eBotOrder.Stay,
            eAutonomousChatIntent.OrderCome => eBotOrder.Come,
            eAutonomousChatIntent.OrderLeave => eBotOrder.Leave,
            eAutonomousChatIntent.OrderAssist => eBotOrder.Assist,
            eAutonomousChatIntent.OrderRest => eBotOrder.Rest,
            _ => eBotOrder.None,
        };
    }

    // Words that may stand around an order without naming anyone: "ok guys, stay here", "everyone come here please".
    private static readonly HashSet<string> OrderFiller = new(StringComparer.OrdinalIgnoreCase)
    {
        "ok", "okay", "alright", "hey", "yo", "oi", "so", "now", "then", "just", "again", "too", "quick", "quickly", "please",
        "pls", "plz", "thanks", "ty", "lol", "yeah", "guys", "everyone", "everybody", "all", "folks", "team", "group", "party",
        "people", "you", "sec", "second", "moment", "minute", "bit", "here", "there", "me", "us", "up", "and", "right",
    };

    /// <summary>
    /// A word that looks like someone the order is for but isn't one of <paramref name="names"/>: the first word
    /// ("shnacki, stay here") or a lone word after the last comma ("stay here, shnacki") that is neither an order word nor
    /// filler. Null when there is none. An open order with such a word goes to nobody (it was meant for someone the game
    /// doesn't know by that name), not to everyone.
    /// </summary>
    public static string OrderStranger(string text, IEnumerable<string> names = null)
    {
        HashSet<string> named = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in (names ?? []).Take(64))
            named.UnionWith(Tokens(name));
        bool Stranger(string word) => word != null && !named.Contains(word) && !OrderFiller.Contains(word) &&
            !LoneOrders.ContainsKey(word) && !IntentPhrases.Shared.IsWordOf(word, eAutonomousChatIntent.OrderFollow,
                eAutonomousChatIntent.OrderStay, eAutonomousChatIntent.OrderCome, eAutonomousChatIntent.OrderLeave,
                eAutonomousChatIntent.OrderAssist, eAutonomousChatIntent.OrderRest);
        string first = Tokens(text).FirstOrDefault();
        if (Stranger(first))
            return first;
        string[] parts = (text ?? string.Empty).Split(',');
        string[] last = parts.Length > 1 ? Tokens(parts[^1]).ToArray() : [];
        return last.Length == 1 && Stranger(last[0]) ? last[0] : null;
    }

    /// <summary>The order sections and answers (OrderFollow ... OrderNot): sections in .dlg files, never small talk.</summary>
    public static bool IsOrderKind(eAutonomousChatIntent intent) =>
        intent is >= eAutonomousChatIntent.OrderFollow and <= eAutonomousChatIntent.OrderNot;

    /// <summary>The small-talk intents: game words beat them, and a bot without lines for one answers with Banter.</summary>
    public static bool IsSmallTalk(eAutonomousChatIntent intent) =>
        intent >= eAutonomousChatIntent.Joke && intent is not (eAutonomousChatIntent.Command or eAutonomousChatIntent.CombatCall) &&
        !IsJokeReplyKind(intent) && !IsStateQuestion(intent) && !IsGroupCallKind(intent) && !IsOrderKind(intent);

    /// <summary>The group-call marker and its answer kinds (GroupYes ... GroupLevel): sections in .dlg files, never small talk.</summary>
    public static bool IsGroupCallKind(eAutonomousChatIntent intent) =>
        intent is >= eAutonomousChatIntent.GroupCall and <= eAutonomousChatIntent.GroupLevel;

    /// <summary>
    /// Questions about the bot's own state (where, level, group), answered from the game, not small talk: a game
    /// word doesn't beat them ("are you in a group?" is asking, not inviting). Activity is answered the same way
    /// but stays small talk, so "what are you doing, want to group?" is still about grouping.
    /// </summary>
    public static bool IsStateQuestion(eAutonomousChatIntent intent) =>
        intent is eAutonomousChatIntent.Where or eAutonomousChatIntent.Level or eAutonomousChatIntent.GroupStatus;

    /// <summary>The joke flow's reply kinds (JokeGuess, Groan, JokeLaugh, JokeBack, JokeQuit, JokeReturn): sections in .dlg files, never an intent read from chat.</summary>
    public static bool IsJokeReplyKind(eAutonomousChatIntent intent) =>
        intent is eAutonomousChatIntent.JokeGuess or eAutonomousChatIntent.Groan or eAutonomousChatIntent.JokeLaugh or
            eAutonomousChatIntent.JokeBack or eAutonomousChatIntent.JokeQuit or
            eAutonomousChatIntent.JokeReturn;

    // Greetings and farewells give way to a game word too: "hey, anyone want to group?" is about grouping.
    private static bool GameWordsWin(eAutonomousChatIntent intent) =>
        IsSmallTalk(intent) || intent is eAutonomousChatIntent.Greeting or eAutonomousChatIntent.Farewell;

    /// <summary>The intent a bot's opener on this topic is answered with; null for the game topics (then its words decide).</summary>
    public static eAutonomousChatIntent? ForTopic(eChatTopic topic) => topic switch
    {
        eChatTopic.Fun => eAutonomousChatIntent.Joke,
        eChatTopic.Gripes => eAutonomousChatIntent.Grumble,
        eChatTopic.Life or eChatTopic.Realm or eChatTopic.Classes => eAutonomousChatIntent.SmallTalk,
        eChatTopic.Questions => eAutonomousChatIntent.Question,
        _ => null,
    };

    private static Model Train()
    {
        var counts = new Dictionary<eAutonomousChatIntent, Dictionary<string, int>>();
        var totals = new Dictionary<eAutonomousChatIntent, int>();
        var documents = new Dictionary<eAutonomousChatIntent, int>();
        var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Example example in Training)
        {
            Dictionary<string, int> bag = counts.GetValueOrDefault(example.Intent);
            if (bag == null)
                counts[example.Intent] = bag = new(StringComparer.OrdinalIgnoreCase);
            documents[example.Intent] = documents.GetValueOrDefault(example.Intent) + 1;
            foreach (string token in Tokens(example.Text).Where(token => !Stopwords.Contains(token)))
            {
                bag[token] = bag.GetValueOrDefault(token) + 1;
                totals[example.Intent] = totals.GetValueOrDefault(example.Intent) + 1;
                vocabulary.Add(token);
            }
        }
        return new(counts, totals, documents, vocabulary, Training.Length);
    }

    internal static IEnumerable<string> Tokens(string text) => TokenRegex().Matches(text?.ToLowerInvariant() ?? string.Empty).Cast<Match>()
        .SelectMany(match => Shorthand.TryGetValue(match.Value, out string[] words) ? words : [match.Value])
        .Where(token => token.Length > 1);

    [GeneratedRegex("[a-z']+")]
    private static partial Regex TokenRegex();
}
