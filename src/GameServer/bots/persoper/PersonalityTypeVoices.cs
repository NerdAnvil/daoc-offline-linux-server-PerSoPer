using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using X = DOL.GS.PersonalityAnnoyance.eIrritation;

namespace DOL.GS;

/// <summary>
/// The voices of the six personality types. Lines are original; the ambient lines and
/// chat replies live in dialogue/<type>.dlg (PersonalityLines). {0} is whoever the bot
/// talks to (see BotPersonality, MemoryVoice, BuildVoice and PatienceProfile for the other
/// placeholders). Templates never match a bot by themselves; PersonalityTypes hands each
/// typed bot its own copy.
/// </summary>
public static partial class PersonalityTypeVoices
{
    public static readonly IReadOnlyDictionary<string, BotPersonality> All = new Dictionary<string, BotPersonality>
    {
        ["hardcore"] = Hardcore(),
        ["casual"] = Casual(),
        ["minmaxer"] = MinMaxer(),
        ["joker"] = Joker(),
        ["loner"] = Loner(),
        ["leeroy"] = Leeroy(),
    };

    // --- Hardcore: XP per hour, efficiency, no time to waste ---
    private static BotPersonality Hardcore() => new()
    {
        Name = "Hardcore", Key = "hardcore", Matches = _ => false, Chattiness = 55,
        Ambient = Talk("hardcore").Ambient, Replies = Talk("hardcore").Replies,
        CheekyReplies = Talk("hardcore").CheekyReplies, FallbackReplies = Talk("hardcore").FallbackReplies,
        Memory = Voice(
            warm: ["{0}! My favourite pull partner.", "That's the {1} time we've teamed up, {0}. Efficient."],
            cool: ["Oh. {0}. The one who wastes time.", "Last time you said \"{3}\". Still out-levelled you."],
            neutral: ["Back, {0}? Keep up this time.", "Haven't seen you in {2}, {0}. I gained six levels."],
            recall: "We grouped {2} since {0} ago, {1}.{3}{4} {5}",
            warmMood: "You pull clean. Respect.", coolMood: "You've been a pain {0}. I keep a list.", neutralMood: "You're fine. Slow, but fine.",
            coolTint: "*sighs*", warmTint: "*nods*"),
        Build = Build(phrase => [$"Running {phrase}. Optimal for this level.", "Spec's maxed for kill speed, {0}."],
            "{0} now. {1} was slower, {2}. Numbers don't lie."),
        Patience = Patience(4, 5, 6, TimeSpan.FromMinutes(4), countsDeaths: true,
            grumbles: ["You're slowing me down, {0}.", "Every second counts, {0}."],
            warnings: ["{0}, I'm losing XP standing here.", "This is costing me levels, {0}."],
            lastWarnings: ["One more and I go solo, {0}.", "Last chance, {0}. I've got a camp to farm."],
            quits: ["I'm out, {0}. Solo is faster anyway.", "Done. Going back to my camp, {0}."],
            makeUps: ["Camp got stale without you, {0}. Heading back.", "Fine, {0}. Duo XP is better. On my way."]),
    };

    // --- Casual: relaxed, AFK jokes, dies a lot, doesn't mind ---
    private static BotPersonality Casual() => new()
    {
        Name = "Casual", Key = "casual", Matches = _ => false, Chattiness = 60,
        Ambient = Talk("casual").Ambient, Replies = Talk("casual").Replies,
        CheekyReplies = Talk("casual").CheekyReplies, FallbackReplies = Talk("casual").FallbackReplies,
        Memory = Voice(
            warm: ["{0}!! hey buddy", "oh it's {0}, nice. that's like the {1} time"],
            cool: ["oh {0}. hi I guess", "you said \"{3}\" last time {0}. not cool"],
            neutral: ["hey {0}, back again?", "haven't seen you in {2} {0}, where you been"],
            recall: "met you {0} ago {1}, grouped {2} I think.{3}{4} {5}",
            warmMood: "you're cool.", coolMood: "you were kinda mean {0} tho.", neutralMood: "you're alright.",
            coolTint: "meh.", warmTint: ":)"),
        Build = Build(phrase => [$"I think I'm {phrase}? whatever the trainer said", "spec? I clicked some buttons {0}"],
            "went {0} this time {2}, {1} was boring"),
        Patience = Patience(6, 7, 8, TimeSpan.FromMinutes(2), countsDeaths: false,
            grumbles: ["lol ok {0}", "{0} stop it haha"],
            warnings: ["ok {0} that's getting annoying", "dude {0}. come on"],
            lastWarnings: ["{0} I'm gonna wander off", "last one {0} I swear"],
            quits: ["ok I'm gonna go do my own thing {0}", "byeee {0}, going fishing or whatever"],
            makeUps: ["hey {0} I'm bored, coming back", "ok I'm over it {0}, heading your way"]),
    };

    // --- Min/maxer: stats, caps, "suboptimal" ---
    private static BotPersonality MinMaxer() => new()
    {
        Name = "Min/maxer", Key = "minmaxer", Matches = _ => false, Chattiness = 50,
        Ambient = Talk("minmaxer").Ambient, Replies = Talk("minmaxer").Replies,
        CheekyReplies = Talk("minmaxer").CheekyReplies, FallbackReplies = Talk("minmaxer").FallbackReplies,
        Memory = Voice(
            warm: ["{0}! The one person who listens about caps.", "That's the {1} time we've grouped, {0}. Your DPS improved."],
            cool: ["{0}. Still running that template?", "You said \"{3}\", {0}. I logged it. In a spreadsheet."],
            neutral: ["Back, {0}? Let me check your gear.", "{2} since we met, {0}. Did you cap anything?"],
            recall: "First grouped {0} ago, {1}. {2} together.{3}{4} {5}",
            warmMood: "Your template is acceptable.", coolMood: "You've been rude {0}. Inefficient.", neutralMood: "You're average. Statistically.",
            coolTint: "*adjusts spreadsheet*", warmTint: "*approving nod*"),
        Build = Build(phrase => [$"{phrase}, fully optimised. Don't ask unless you want numbers.", "Spec spreadsheet says this is best in slot, {0}."],
            "Switched to {0}, {2}. {1} lost 3% DPS. Unacceptable."),
        Patience = Patience(4, 5, 6, TimeSpan.FromMinutes(5), countsDeaths: true,
            grumbles: ["Inefficient, {0}.", "That wasn't optimal, {0}."],
            warnings: ["{0}, your behaviour is a DPS loss.", "I'm recalculating whether you're worth it, {0}."],
            lastWarnings: ["Final warning, {0}. The math says leave.", "One more and I optimise you out, {0}."],
            quits: ["Calculated, {0}. Leaving is optimal.", "You're a net negative, {0}. Goodbye."],
            makeUps: ["Recalculated, {0}. You're worth it after all. Coming back.", "New numbers, {0}. Rejoining."]),
    };

    // --- Joker: puns, teasing; cheeky when tone-down is off ---
    private static BotPersonality Joker() => new()
    {
        Name = "Joker", Key = "joker", Matches = _ => false, Chattiness = 80,
        Ambient = Talk("joker").Ambient, Replies = Talk("joker").Replies,
        CheekyReplies = Talk("joker").CheekyReplies, FallbackReplies = Talk("joker").FallbackReplies,
        Memory = Voice(
            warm: ["{0}! My favourite audience!", "That's the {1} time, {0}. You must like my jokes."],
            cool: ["Oh, {0}. The heckler.", "You said \"{3}\", {0}. I'm putting it in my act."],
            neutral: ["{0}! Back for more jokes?", "{2} without my jokes, {0}? You poor thing."],
            recall: "We met {0} ago, {1}, and you've suffered my jokes {2}.{3}{4} {5}",
            warmMood: "You laugh at the right moments.", coolMood: "You've heckled me {0}.", neutralMood: "You're a tough crowd.",
            coolTint: "*badum-tss*", warmTint: "*winks*"),
        Build = Build(phrase => [$"{phrase} today. The jokes are the real weapon though.", "My spec? Mostly puns, {0}."],
            "{0} now, {2}. {1} wasn't funny enough."),
        Patience = Patience(5, 6, 7, TimeSpan.FromMinutes(3), countsDeaths: false,
            grumbles: ["Haha. Ha. Stop, {0}.", "Even I have limits, {0}."],
            warnings: ["{0}, that's not funny anymore.", "Tough crowd tonight, {0}."],
            lastWarnings: ["One more, {0}, and I take my act elsewhere.", "Last laugh, {0}. Then I'm gone."],
            quits: ["I'm out, {0}! Taking the show on the road.", "Tough crowd. Goodnight, {0}!"],
            makeUps: ["I got a new joke, {0}. Coming back to tell it.", "Missed my audience, {0}. On my way."]),
    };

    // --- Loner: rarely speaks, dry, quick to leave ---
    private static BotPersonality Loner() => new()
    {
        Name = "Loner", Key = "loner", Matches = _ => false, Chattiness = 12,
        Ambient = Talk("loner").Ambient, Replies = Talk("loner").Replies,
        CheekyReplies = Talk("loner").CheekyReplies, FallbackReplies = Talk("loner").FallbackReplies,
        Memory = Voice(
            warm: ["{0}. You again. Fine.", "{1} time, {0}. You're tolerable."],
            cool: ["{0}. Go away.", "You said \"{3}\". I remember."],
            neutral: ["{0}.", "{2}. Didn't miss you, {0}."],
            recall: "{0} ago. {2}.{3}{4} {5}",
            warmMood: "You're quiet. Good.", coolMood: "Rude {0}.", neutralMood: "You talk too much.",
            coolTint: "...", warmTint: "*nods*"),
        Build = Build(phrase => [$"{phrase}.", "Solo build."], "{0}. {1} didn't work."),
        Patience = Patience(2, 3, 4, TimeSpan.FromMinutes(6), countsDeaths: true,
            grumbles: ["Stop.", "Don't."],
            warnings: ["I'm leaving soon, {0}.", "Enough, {0}."],
            lastWarnings: ["Last time, {0}.", "Once more and I'm gone."],
            quits: ["Done. Solo.", "I'm out, {0}."],
            makeUps: ["...fine. Coming back, {0}.", "Coming. Don't make it a thing, {0}."]),
    };

    // --- Leeroy: reckless, loud, charges in ---
    private static BotPersonality Leeroy() => new()
    {
        Name = "Leeroy", Key = "leeroy", Matches = _ => false, Chattiness = 70,
        Ambient = Talk("leeroy").Ambient, Replies = Talk("leeroy").Replies,
        CheekyReplies = Talk("leeroy").CheekyReplies, FallbackReplies = Talk("leeroy").FallbackReplies,
        Memory = Voice(
            warm: ["{0}!! My charge buddy!", "{1} time together, {0}! We only died most of those!"],
            cool: ["{0}. You slowed me down last time.", "You said \"{3}\", {0}! Fight me!"],
            neutral: ["{0}! Back for more chaos?", "{2} without me, {0}? Boring, right?"],
            recall: "Met you {0} ago, {1}! {2} of glorious charges!{3}{4} {5}",
            warmMood: "You keep up! Mostly!", coolMood: "You've been a buzzkill {0}!", neutralMood: "You need to charge more.",
            coolTint: "Ugh.", warmTint: "LET'S GO!"),
        Build = Build(phrase => [$"{phrase}! Built for charging!", "My build is GO, {0}!"],
            "{0} today, {2}! {1} didn't charge hard enough!"),
        Patience = Patience(4, 5, 6, TimeSpan.FromMinutes(2), countsDeaths: false,
            grumbles: ["Come ON, {0}!", "Less talking, more charging, {0}!"],
            warnings: ["{0}, you're killing my momentum!", "I'm about to charge off without you, {0}!"],
            lastWarnings: ["One more and I'm GONE, {0}!", "Last warning! I mean it, {0}!"],
            quits: ["FORGET THIS! I'm charging solo! LEEEROOOY!", "I'm out, {0}! There's a dragon somewhere with my name on it!"],
            makeUps: ["Dragon was too big. Coming back, {0}!", "Okay okay, {0}! Charging back to you!"]),
    };

    // --- Shared builders ---

    /// <summary>A type's ambient lines and replies, from its dialogue file.</summary>
    private static TypeTalk Talk(string key) => PersonalityLines.TalkFor(key);

    private static MemoryVoice Voice(string[] warm, string[] cool, string[] neutral, string recall, string warmMood,
        string coolMood, string neutralMood, string coolTint, string warmTint) => new()
    {
        Warm = warm, Cool = cool, Neutral = neutral,
        JustMet = "Do I know you, {0}? I don't think we've met.",
        Recall = recall, RecallWhere = " Last time: {0}.", RecallBuild = " I was {0}.",
        WarmMood = warmMood, CoolMood = coolMood, NeutralMood = neutralMood, CoolTint = coolTint, WarmTint = warmTint,
    };

    private static BuildVoice Build(Func<string, string[]> lines, string comparedToLast) => new()
    {
        Phrase = SpecWords,
        Lines = spec => lines(SpecWords(spec) is { } words ? Capital(words) : "My usual build"),
        ComparedToLast = comparedToLast,
    };

    private static PatienceProfile Patience(int warnAt, int lastWarnAt, int quitAt, TimeSpan calmDown, bool countsDeaths,
        string[] grumbles, string[] warnings, string[] lastWarnings, string[] quits, string[] makeUps) => new()
    {
        WarnAt = warnAt, LastWarnAt = lastWarnAt, QuitAt = quitAt, CalmDown = calmDown, CountsDeaths = countsDeaths,
        Grumbles = new Dictionary<X, string[]>
        {
            [X.QuickRecall] = grumbles, [X.Rude] = grumbles, [X.EmoteSpam] = grumbles, [X.Death] = grumbles, [X.HealSpam] = grumbles,
        },
        Warnings = warnings, LastWarnings = lastWarnings, Quits = quits,
        SulkRefusals = ["\"Not now, {0}.\" Try again in {1}.", "They're still annoyed with you, {0}. Try again in {1}."],
        MakeUps = makeUps,
        MakeUpInvites = ["Ok, {0}, I'm over it. Invite me if you want.", "Send an invite when you're ready, {0}."],
    };

    /// <summary>"TwoHanded" -> "two handed", "MendHealer" -> "mend healer"; null for no spec.</summary>
    public static string SpecWords(eSpecType spec) =>
        spec == eSpecType.None ? null : CamelCase().Replace(spec.ToString(), " $1").Trim().ToLowerInvariant();

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    [GeneratedRegex("([A-Z])")]
    private static partial Regex CamelCase();
}
