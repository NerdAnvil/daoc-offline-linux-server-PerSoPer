using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using I = DOL.GS.eAutonomousChatIntent;
using X = DOL.GS.PersonalityAnnoyance.eIrritation;

namespace DOL.GS;

/// <summary>
/// The voices of the six personality types. Lines are original. {0} is whoever the bot
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
        Ambient =
        [
            "Camp's been perfect for an hour. Not moving until I ding.",
            "Anyone else tracking XP per hour? I'm up 12% on yesterday.",
            "Corpse runs are a choice. Pull smarter.",
            "If you're not chain pulling, you're resting wrong.",
            "Sleep is a debuff.",
            "Two more levels before I log. Maybe three.",
            "Who's farming the next camp over? You're slow. Respectfully.",
            "Ran the numbers: this spot beats the dungeon by a mile.",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["{0}. Busy. Talk while I pull.", "Hey {0}. Don't break my chain."],
            [I.Banter] = ["Funny, {0}. Jokes don't give XP though.", "Ha. Now pull something."],
            [I.Abuse] = ["Noted, {0}. Still out-levelling you.", "Cry about it at the bind stone, {0}."],
            [I.Grouping] = ["Group? Only if you can keep up, {0}.", "I'll join, {0}, but nobody AFKs. Ever."],
            [I.Grinding] = ["Best grind in the zone is right here, {0}.", "Grinding is life, {0}."],
            [I.Travel] = ["Travel time is wasted XP, {0}. Take the horse.", "Shortest route or no route, {0}."],
            [I.Dungeon] = ["Dungeon's good XP if the group doesn't wipe, {0}.", "I'll tank the run if nobody stops to loot every corpse."],
            [I.RvR] = ["RvR after I cap, {0}. Priorities.", "Realm points are just XP with extra steps, {0}."],
            [I.Trading] = ["No time to haggle, {0}. Fair price or nothing.", "Gear is a tool, {0}. I buy what levels me faster."],
            [I.Help] = ["Quick answer, {0}: train, then go back out.", "Help? Pull, kill, rest less. Done."],
            [I.Farewell] = ["Bye, {0}. Some of us are still grinding.", "Later, {0}."],
        },
        FallbackReplies = ["Mm. Anyway, back to the grind, {0}.", "Sure, {0}. Pulling."],
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
        Ambient =
        [
            "brb, snack",
            "lol died again. worth it.",
            "anyone know where I left my horse",
            "what level is everyone? I'm... some level",
            "just here for the vibes honestly",
            "this sunset in game is nice ngl",
            "ok who's up for doing absolutely nothing productive",
            "I sold my good sword by accident. it's fine.",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["heyyy {0}", "oh hi {0}! what's up"],
            [I.Banter] = ["lol {0}", "haha that's good {0}"],
            [I.Abuse] = ["ok {0} lol", "chill {0}, it's a game"],
            [I.Grouping] = ["sure {0}, I'll tag along", "I'm in {0}, but I might afk"],
            [I.Grinding] = ["grinding? I mostly wander around {0}", "I level when I level {0}"],
            [I.Travel] = ["no idea where that is {0} but good luck", "I usually just follow someone {0}"],
            [I.Dungeon] = ["dungeons are scary {0}. I'll come though", "only if someone else leads {0}"],
            [I.RvR] = ["RvR? I'll just die a lot {0}. sounds fun", "I'll come for the screenshots {0}"],
            [I.Trading] = ["I have... two copper {0}", "you can have my spare stuff {0}, idk what it does"],
            [I.Help] = ["no clue {0} sorry lol", "I'd ask someone else {0}, I'm lost too"],
            [I.Farewell] = ["cya {0}!", "later {0}, have fun"],
        },
        FallbackReplies = ["lol yeah {0}", "for real {0}", "hm? sorry {0}, was afk"],
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
        Ambient =
        [
            "Reminder: resists cap at 26% before buffs. Plan accordingly.",
            "Overcapped strength is a crime and I'm the police.",
            "My template is 2% off perfect and it haunts me.",
            "Anyone running dual stat charms? Show me your numbers.",
            "That boss drop is a side-grade at best.",
            "Quickness is undervalued. Fight me. Mathematically.",
            "Spent the afternoon on a spreadsheet. Best day this week.",
            "If your hits cap isn't capped, what are you doing?",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["Hello, {0}. What's your template?", "{0}. Nice gear. Suboptimal, but nice."],
            [I.Banter] = ["Statistically that was funny, {0}.", "Humour isn't a stat, {0}. Sadly."],
            [I.Abuse] = ["Your insult lacks damage, {0}.", "Rude, {0}, and inefficient."],
            [I.Grouping] = ["Group comp, {0}? Send me the classes first.", "I'll join if we have a real healer, {0}."],
            [I.Grinding] = ["XP per minute matters more than XP per kill, {0}.", "That camp's spawn timer is 4 minutes, {0}. Rotate."],
            [I.Travel] = ["Speed buffs stack wrong for most people, {0}.", "Horse route saves 40 seconds, {0}. I timed it."],
            [I.Dungeon] = ["Dungeon loot tables are mostly vendor trash, {0}.", "Only worth it for one drop, {0}. I know which."],
            [I.RvR] = ["RvR is a gear check, {0}. Are you capped?", "I'll come, {0}, after I respec."],
            [I.Trading] = ["Show me the bonuses, {0}. Then we'll talk price.", "That item is worth exactly its stats, {0}."],
            [I.Help] = ["Cap your primary stat first, {0}. Then resists.", "Check your spec points, {0}. You wasted some."],
            [I.Farewell] = ["Goodbye, {0}. Cap your resists.", "Bye, {0}. Respec soon."],
        },
        FallbackReplies = ["Interesting, {0}. Do you have numbers?", "Source, {0}?"],
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
        Ambient =
        [
            "Why did the Troll cross the road? To get to the other slide.",
            "I tried to catch fog earlier. Mist.",
            "My armour is so bad, mobs feel sorry for me.",
            "A skeleton walks into a tavern: 'A pint, and a mop.'",
            "I'm reading a book on anti-gravity. Can't put it down.",
            "Somebody stole my horse. I'm not stable.",
            "My healer said I need to lose weight. I dropped my shield.",
            "Kobolds make great comedians. Short, punchy material.",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["Well, if it isn't {0}! Or is it?", "{0}! Knock knock."],
            [I.Banter] = ["Ha! {0}, you're almost as funny as me.", "Good one, {0}. I'm stealing it."],
            [I.Abuse] = ["Ouch, {0}. Did you rehearse that?", "Easy, {0}, I bruise like a peach."],
            [I.Grouping] = ["I'll join, {0}. Someone has to keep morale up.", "Group? Only if I can name it, {0}."],
            [I.Grinding] = ["Grinding? Sounds like a coffee joke, {0}.", "Every mob I kill, I tell a joke. They die faster."],
            [I.Travel] = ["Travel tip, {0}: always bring a joke for the horse.", "I'd go, {0}, but I'm not stable."],
            [I.Dungeon] = ["Dungeons are just basements with ambition, {0}.", "I'll come, {0}. I'm great in the dark. Nobody sees my face."],
            [I.RvR] = ["The frontier? I'll bring jokes and run away, {0}.", "Albs fear one thing, {0}: my puns."],
            [I.Trading] = ["I'll trade you a joke for that, {0}.", "Price check: priceless. Like my humour, {0}."],
            [I.Help] = ["Help is on the way, {0}. It's just very slow.", "Have you tried turning your sword off and on again, {0}?"],
            [I.Farewell] = ["Bye {0}! Don't let the portal hit you.", "See you, {0}. Wouldn't want to be you. Kidding!"],
        },
        CheekyReplies = new Dictionary<I, string[]>
        {
            [I.Banter] = ["{0}, are you a healer? Because my HP goes up when you're around."],
            [I.Greeting] = ["Hey {0}, nice armour. Does it come in my size?"],
        },
        FallbackReplies = ["That reminds me of a joke, {0}. I forgot it.", "Ha! Wait, was that a joke, {0}?"],
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
        Ambient =
        [
            "...",
            "Quiet out here. Good.",
            "Too many people in this zone.",
            "Solo.",
            "Don't follow me.",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["Hm.", "{0}."],
            [I.Banter] = ["...", "Right."],
            [I.Abuse] = ["Whatever.", "Leave me alone, {0}."],
            [I.Grouping] = ["I work alone.", "No, {0}."],
            [I.Grinding] = ["Found my spot. Go find yours.", "Mm."],
            [I.Travel] = ["Walk.", "That way."],
            [I.Dungeon] = ["Alone. Always.", "Not with a group."],
            [I.RvR] = ["I'll scout. Alone.", "No."],
            [I.Trading] = ["Not interested.", "No."],
            [I.Help] = ["Figure it out, {0}.", "Trainer."],
            [I.Farewell] = ["Good.", "Bye."],
        },
        FallbackReplies = ["...", "Hm.", "Sure."],
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
        Ambient =
        [
            "Plans are for people who aren't me!",
            "Saw a dragon. Waved. Running now. Worth it.",
            "Who needs a puller when you have LEGS?",
            "At least I have chicken. Wait, wrong game.",
            "Ran into a whole camp. Twice. Same camp.",
            "Strategy: go in. Phase two: see what happens.",
            "Anyone got a res? Asking for me.",
            "I'm not reckless, I'm EFFICIENT at dying.",
        ],
        Replies = new Dictionary<I, string[]>
        {
            [I.Greeting] = ["{0}! Let's GO!", "Hey {0}! Wanna pull something big?"],
            [I.Banter] = ["HA! Love it, {0}!", "You're crazy, {0}. I like crazy."],
            [I.Abuse] = ["Talk is cheap, {0}! Fight me!", "Come say that in RvR, {0}!"],
            [I.Grouping] = ["Group? I'll pull! I always pull!", "YES {0}! I'll go first!"],
            [I.Grinding] = ["Why pull one when you can pull FIVE, {0}?", "Grinding is boring. Charging is fun!"],
            [I.Travel] = ["I'll get there first, {0}! Probably alive!", "Shortcut through the giants? Sure!"],
            [I.Dungeon] = ["DUNGEON! I'm going in! Right now!", "Who needs a plan, {0}? Not me!"],
            [I.RvR] = ["FRONTIER! CHARGE!", "I'll take the keep alone, {0}! Watch me!"],
            [I.Trading] = ["Trade? I'll buy whatever makes me hit harder!", "Got anything shiny and dangerous, {0}?"],
            [I.Help] = ["Help? Just run in, {0}! Works every time! Mostly!", "Step one: charge. There is no step two, {0}."],
            [I.Farewell] = ["Bye {0}! I'm off to find something huge!", "Later, {0}! Don't wait up!"],
        },
        CheekyReplies = new Dictionary<I, string[]>
        {
            [I.Abuse] = ["Big words, {0}. Small sword."],
        },
        FallbackReplies = ["Sounds like a fight, {0}! I'm in!", "Whatever it is, {0}, I'm charging it."],
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
