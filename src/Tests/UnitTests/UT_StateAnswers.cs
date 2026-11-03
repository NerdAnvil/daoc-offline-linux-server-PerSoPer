using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B4: "where are you?", "what level are you?", "are you in a group?" and "what are you doing?"
/// are read as questions about the bot, and a personality bot answers them from its real state (zone,
/// level, class, group, task) in its own lines, or shrugs when it doesn't know. It never says a fact it
/// wasn't given.
/// </summary>
[TestFixture]
public class UT_StateAnswers
{
    private static readonly eAutonomousChatIntent[] Asked =
        [eAutonomousChatIntent.Where, eAutonomousChatIntent.Level, eAutonomousChatIntent.GroupStatus, eAutonomousChatIntent.Activity];

    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    [SetUp]
    public void Forget() => DialoguePicker.Shared.Clear();

    [TestCase("shemp, where are you?", eAutonomousChatIntent.Where)]
    [TestCase("where you at", eAutonomousChatIntent.Where)]
    [TestCase("what zone are you in", eAutonomousChatIntent.Where)]
    [TestCase("what level are you", eAutonomousChatIntent.Level)]
    [TestCase("what lvl r u", eAutonomousChatIntent.Level)]
    [TestCase("persoper, what class are you?", eAutonomousChatIntent.Level)]
    [TestCase("are you in a group", eAutonomousChatIntent.GroupStatus)]
    [TestCase("jovi, are you grouped?", eAutonomousChatIntent.GroupStatus)]
    [TestCase("you solo?", eAutonomousChatIntent.GroupStatus)]
    [TestCase("what are you doing", eAutonomousChatIntent.Activity)]
    [TestCase("whatcha doin", eAutonomousChatIntent.Activity)]
    [TestCase("are you busy?", eAutonomousChatIntent.Activity)]
    [TestCase("where are you headed", eAutonomousChatIntent.Activity)]
    [TestCase("where are you from jovie", eAutonomousChatIntent.Question)]
    [TestCase("anyone want to group?", eAutonomousChatIntent.Grouping)]
    [TestCase("are you a red head?", eAutonomousChatIntent.Banter)]
    public void Questions_AreReadAsAboutTheBot(string line, eAutonomousChatIntent expected)
    {
        Assert.That(AutonomousChatIntentModel.Read(line), Is.EqualTo(expected));
    }

    [Test]
    public void PlainBots_GetTheirNearestOldIntent()
    {
        Assert.That(AutonomousChatIntentModel.Predict("where are you?"), Is.EqualTo(eAutonomousChatIntent.Travel));
        Assert.That(AutonomousChatIntentModel.Predict("are you in a group?"), Is.EqualTo(eAutonomousChatIntent.Grouping));
        Assert.That(AutonomousChatIntentModel.Predict("what level are you?"), Is.EqualTo(eAutonomousChatIntent.Question));
        Assert.That(Asked.Where(AutonomousChatIntentModel.IsSmallTalk), Is.EqualTo(new[] { eAutonomousChatIntent.Activity }),
            "a game word doesn't beat where, level or group questions");
    }

    [TestCase("shemp, where are you?")]
    [TestCase("what level are you?")]
    [TestCase("are you in a group?")]
    [TestCase("what are you doing?")]
    public void Questions_AreNotJokes(string line)
    {
        Assert.That(PersonalityJokes.Kind(line, AutonomousChatIntentModel.Read(line)), Is.EqualTo(eJokeKind.None));
    }

    [TestCase(true, true, true, true, "fighting")]
    [TestCase(false, true, true, true, "riding")]
    [TestCase(false, false, true, true, "following")]
    [TestCase(false, false, false, true, "travelling")]
    [TestCase(false, false, false, false, "resting")]
    public void Doing_IsTheBusiestThingThatIsTrue(bool fighting, bool riding, bool following, bool moving, string expected)
    {
        Assert.That(PersonalityStateAnswers.Doing(fighting, riding, following, moving), Is.EqualTo(expected));
        Assert.That(PersonalityStateAnswers.DoingValues, Does.Contain(expected));
        Assert.That(DialogueRule.ChoiceFacts["bot_doing"], Is.EquivalentTo(PersonalityStateAnswers.DoingValues));
    }

    [Test]
    public void Fill_LeavesOutALineWithAnUnknownSlot()
    {
        var slots = new StateSlots("Vale of Mularn", 12, "Thane", null, null, 1, "Tester");
        Assert.That(PersonalityStateAnswers.Fill("{zone}, {0}.", slots, "Bob"), Is.EqualTo("Vale of Mularn, Bob."));
        Assert.That(PersonalityStateAnswers.Fill("{level} {class}", slots, "Bob"), Is.EqualTo("12 Thane"));
        Assert.That(PersonalityStateAnswers.Fill("Killing {monster}", slots, "Bob"), Is.Null);
        Assert.That(PersonalityStateAnswers.Fill("To {destination}", slots, "Bob"), Is.Null);
        Assert.That(PersonalityStateAnswers.Fill("{groupsize} of us", slots, "Bob"), Is.Null, "a group of one isn't a group");
        Assert.That(PersonalityStateAnswers.Fill("Following {leader}", slots, "Bob"), Is.EqualTo("Following Tester"));
        Assert.That(PersonalityStateAnswers.Fill("Following {leader}", slots, "tester"), Is.EqualTo("Following you"), "the one who asked leads");
        Assert.That(PersonalityStateAnswers.Fill("Level {level}", new StateSlots(null, 0, null, null, null, 0, null), "Bob"), Is.Null);
    }

    [Test]
    public void BotFacts_AreReadByTheirConditions()
    {
        var errors = new List<string>();
        DialogueRule riding = DialogueRule.Split("Riding. [if bot_doing riding]", errors).Rule;
        DialogueRule solo = DialogueRule.Split("Solo. [if not bot_grouped]", errors).Rule;
        Assert.That(errors, Is.Empty);
        DialogueRule.Split("Dancing. [if bot_doing dancing]", errors);
        Assert.That(errors, Has.Count.EqualTo(1), "only the known bot_doing values");

        var facts = new DialogueFacts { BotDoing = "riding", BotGrouped = false };
        Assert.That(riding.Conditions.All(facts.Holds), Is.True);
        Assert.That(solo.Conditions.All(facts.Holds), Is.True);
        Assert.That(solo.Conditions.All(new DialogueFacts { BotGrouped = true }.Holds), Is.False);
        Assert.That(riding.Conditions.All(new DialogueFacts().Holds), Is.False, "unknown is not riding");
    }

    // A bot with test lines; the rules come from the lines' own tags.
    private static (BotPersonality Personality, Func<string, DialogueRule> RuleOf) Bot(eAutonomousChatIntent intent, params string[] written)
    {
        var rules = new Dictionary<string, DialogueRule>(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (string line in written)
        {
            (string text, DialogueRule rule) = DialogueRule.Split(line, []);
            lines.Add(text);
            if (rule != null)
                rules[text] = rule;
        }
        var personality = new BotPersonality
        {
            Name = "Testy",
            Key = "test-state",
            Matches = _ => false,
            Replies = new Dictionary<eAutonomousChatIntent, string[]> { [intent] = lines.ToArray() },
        };
        return (personality, rules.GetValueOrDefault);
    }

    [Test]
    public void Answer_SaysTheRealZone_OrShrugsWhenItDoesntKnow()
    {
        var (bot, ruleOf) = Bot(eAutonomousChatIntent.Where,
            "{zone}, {0}.", "Off to {destination}. [if bot_doing travelling]", "No idea, {0}.");
        var resting = new DialogueFacts { BotDoing = "resting", BotGrouped = false };
        var known = new StateSlots("Vale of Mularn", 5, "Thane", null, null, 0, null);
        for (int i = 0; i < 10; i++)
            Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Where, known, resting, "Bob", new Random(i), ruleOf),
                Is.EqualTo("Vale of Mularn, Bob."), "the shrug only when nothing else fits");
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Where, new StateSlots(null, 5, "Thane", null, null, 0, null), resting, "Bob", null, ruleOf),
            Is.EqualTo("No idea, Bob."));

        var travelling = new DialogueFacts { BotDoing = "travelling" };
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Where, known with { Destination = "Mularn" }, travelling, "Bob", null, ruleOf),
            Is.EqualTo("Off to Mularn."), "the line whose condition holds wins");
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Where, known, travelling, "Bob", null, ruleOf),
            Is.EqualTo("Vale of Mularn, Bob."), "no destination known: not that line");
    }

    [Test]
    public void Answer_FightingWithoutAKnownMonster_UsesTheLineWithoutOne()
    {
        var (bot, ruleOf) = Bot(eAutonomousChatIntent.Activity,
            "Killing {monster}. [if bot_doing fighting]", "Busy! [if bot_doing fighting]", "Resting in {zone}. [if bot_doing resting]", "Stuff.");
        var fighting = new DialogueFacts { BotDoing = "fighting" };
        var noMonster = new StateSlots("Vale of Mularn", 5, "Thane", null, null, 0, null);
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Activity, noMonster, fighting, "Bob", null, ruleOf), Is.EqualTo("Busy!"));
        string answer = PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Activity, noMonster with { Monster = "rage wolf" }, fighting, "Bob", new Random(1), ruleOf);
        Assert.That(answer, Is.AnyOf("Killing rage wolf.", "Busy!"));
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Activity, noMonster, new DialogueFacts { BotDoing = "resting" }, "Bob", null, ruleOf),
            Is.EqualTo("Resting in Vale of Mularn."));
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Activity, noMonster, null, "Bob", null, ruleOf), Is.EqualTo("Stuff."),
            "no facts: no line with a condition");
    }

    [Test]
    public void Answer_IsNullForOtherIntentsAndBotsWithoutLines()
    {
        var (bot, ruleOf) = Bot(eAutonomousChatIntent.Where, "{zone}.");
        var slots = new StateSlots("Vale of Mularn", 5, "Thane", null, null, 0, null);
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Banter, slots, null, "Bob", null, ruleOf), Is.Null);
        Assert.That(PersonalityStateAnswers.Answer(bot, eAutonomousChatIntent.Level, slots, null, "Bob", null, ruleOf), Is.Null);
        Assert.That(PersonalityStateAnswers.Answer(null, eAutonomousChatIntent.Where, slots, null, "Bob", null, ruleOf), Is.Null);
    }

    private static IEnumerable<BotPersonality> Voices() =>
        PersonalityTypes.Templates.Values
            .Concat(NamedBotLineup.All.Select(PersonalityTypes.Named))
            .Append(PersoperIdentity.Personality)
            .Append(KobaldjoviIdentity.Personality);

    [Test]
    public void EveryVoice_AnswersAllFour_WithOnlyKnownSlots()
    {
        var allowed = PersonalityStateAnswers.Slots.Append("{0}").ToHashSet();
        foreach (BotPersonality voice in Voices())
        foreach (eAutonomousChatIntent intent in Asked)
        {
            string where = $"{voice.Key} ~ reply {intent.ToString().ToLowerInvariant()}";
            string[] lines = voice.Replies.GetValueOrDefault(intent);
            Assert.That(lines, Is.Not.Null.And.Not.Empty, where);
            foreach (string line in lines)
            {
                foreach (Match slot in Placeholder.Matches(line))
                    Assert.That(allowed, Does.Contain(slot.Value), $"{where}: {line}");
                Assert.That(line, Does.Not.StartWith("{leader}").And.Not.Contain("{leader}'s"), $"{where}: {{leader}} can be \"you\": {line}");
                Assert.That(line, Does.Not.Match(@"\ban? \{(class|monster)\}"), $"{where}: a/an before a name: {line}");
            }
            Assert.That(lines.Any(line => !PersonalityStateAnswers.HasSlot(line) && DialogueLibrary.RuleFor(line) == null), Is.True,
                $"{where}: needs a shrug (no slot, no condition) for when it doesn't know");
        }
    }

    [Test]
    public void EveryVoice_SaysWhatItIsDoing_ForEachState()
    {
        foreach (BotPersonality voice in Voices())
        {
            string[] lines = voice.Replies[eAutonomousChatIntent.Activity];
            foreach (string doing in PersonalityStateAnswers.DoingValues)
            {
                var facts = new DialogueFacts { BotDoing = doing };
                string[] fitting = lines.Where(line => DialogueLibrary.RuleFor(line)?.Conditions.All(facts.Holds) == true).ToArray();
                Assert.That(fitting, Is.Not.Empty, $"{voice.Key}: no '[if bot_doing {doing}]' line");
                if (doing == "fighting")
                    Assert.That(fitting.Any(line => !line.Contains("{monster}")), Is.True, $"{voice.Key}: a fighting line that doesn't need the monster");
            }
            Assert.That(voice.Replies[eAutonomousChatIntent.GroupStatus].Count(line => DialogueLibrary.RuleFor(line)?.Conditions.Count > 0), Is.GreaterThanOrEqualTo(2),
                $"{voice.Key}: a grouped and a solo answer");
        }
    }

    [Test]
    public void Persoper_FollowingTheOneWhoAsked_SaysYou()
    {
        var slots = new StateSlots("Vale of Mularn", 20, "Healer", null, null, 2, "Tester");
        string answer = PersonalityStateAnswers.Answer(PersoperIdentity.Personality, eAutonomousChatIntent.Activity, slots,
            new DialogueFacts { BotDoing = "following", BotGrouped = true }, "Tester", null);
        Assert.That(answer, Is.EqualTo("Following you and watching your health bar like a hawk."));
        string where = PersonalityStateAnswers.Answer(PersoperIdentity.Personality, eAutonomousChatIntent.Where, slots,
            new DialogueFacts { BotDoing = "following", BotGrouped = true }, "Tester", new Random(3));
        Assert.That(where, Does.Contain("Vale of Mularn"));
    }
}
