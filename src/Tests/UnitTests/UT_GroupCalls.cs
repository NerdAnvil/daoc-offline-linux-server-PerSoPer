using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B5: an open group call ("who wants to group?", "lfg") is told apart from asking to join, and each
/// personality world bot that hears it answers from its real state and type: quiet when it is already with the caller
/// or the caller's group is full, busy while fighting or riding, "already grouped", the level gap, else yes or no by how
/// keen its type is. At most three answer, a second call within the cooldown gets none, and a "yes" lets a loner's
/// real /invite through.
/// </summary>
[TestFixture]
public class UT_GroupCalls
{
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    private static readonly eGroupCallAnswer[] Spoken =
        [eGroupCallAnswer.Yes, eGroupCallAnswer.No, eGroupCallAnswer.Busy, eGroupCallAnswer.Grouped, eGroupCallAnswer.Level];

    // A free bot of level 20 hearing a level 20 caller who has room.
    private static readonly GroupCallState Free = new(false, false, false, false, false, 20, 20, false);

    [SetUp]
    public void Forget()
    {
        DialoguePicker.Shared.Clear();
        PersonalityGroupCalls.Clear();
        PersonalityGroupCalls.Always = false;
    }

    [TestCase("who wants to group?")]
    [TestCase("anyone want to group")]
    [TestCase("anybody wanna grp?")]
    [TestCase("who's up for a dungeon?")]
    [TestCase("anyone up for some rvr")]
    [TestCase("anyone want to take a keep")]
    [TestCase("lfg")]
    [TestCase("LFG spraggon den")]
    [TestCase("lf group")]
    [TestCase("lfm for the tomb")]
    [TestCase("hey, anyone want to team up?")]
    public void OpenCalls_AreGroupCalls(string line)
    {
        Assert.That(AutonomousChatIntentModel.IsGroupCall(line), Is.True, line);
    }

    [TestCase("can i join?")]
    [TestCase("inv pls")]
    [TestCase("invite me")]
    [TestCase("are you in a group?")]
    [TestCase("need a healer")]
    [TestCase("power level me")]
    [TestCase("hello")]
    [TestCase("anyone want to group, you idiots")]
    [TestCase("Has anyone in Camelot seen my chicken?")]
    public void OtherLines_AreNot(string line)
    {
        Assert.That(AutonomousChatIntentModel.IsGroupCall(line), Is.False, line);
    }

    [TestCase("who wants to group?", eAutonomousChatIntent.Grouping)]
    [TestCase("who's up for a dungeon?", eAutonomousChatIntent.Dungeon)]
    [TestCase("anyone want to take a keep", eAutonomousChatIntent.RvR)]
    [TestCase("lfg", eAutonomousChatIntent.Grouping)]
    [TestCase("can i join?", eAutonomousChatIntent.Grouping)]
    public void Calls_StillReadAsBefore_ForPlainBots(string line, eAutonomousChatIntent expected)
    {
        Assert.That(AutonomousChatIntentModel.Read(line), Is.EqualTo(expected));
        Assert.That(AutonomousChatIntentModel.Predict(line), Is.EqualTo(expected));
    }

    [Test]
    public void AnswerKinds_AreNotSmallTalk()
    {
        foreach (eAutonomousChatIntent kind in new[]
                 {
                     eAutonomousChatIntent.GroupCall, eAutonomousChatIntent.GroupYes, eAutonomousChatIntent.GroupNo,
                     eAutonomousChatIntent.GroupBusy, eAutonomousChatIntent.GroupHave, eAutonomousChatIntent.GroupLevel,
                 })
        {
            Assert.That(AutonomousChatIntentModel.IsGroupCallKind(kind), Is.True, kind.ToString());
            Assert.That(AutonomousChatIntentModel.IsSmallTalk(kind), Is.False, kind.ToString());
        }
        Assert.That(PersonalityGroupCalls.KindFor(eGroupCallAnswer.Quiet), Is.EqualTo(eAutonomousChatIntent.Unknown));
        Assert.That(Spoken.Select(PersonalityGroupCalls.KindFor).Distinct().Count(), Is.EqualTo(Spoken.Length));
    }

    [Test]
    public void Decide_GoesByRealStateFirst()
    {
        var random = new Random(3);
        Assert.That(PersonalityGroupCalls.Decide(Free with { SameGroup = true, Fighting = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Quiet),
            "already with the caller");
        Assert.That(PersonalityGroupCalls.Decide(Free with { CallerGroupFull = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Quiet),
            "no room");
        Assert.That(PersonalityGroupCalls.Decide(Free with { Fighting = true, InPlayerGroup = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Busy));
        Assert.That(PersonalityGroupCalls.Decide(Free with { Riding = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Busy));
        Assert.That(PersonalityGroupCalls.Decide(Free with { InPlayerGroup = true, BotLevel = 50 }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Grouped));
        Assert.That(PersonalityGroupCalls.Decide(Free with { BotLevel = 40, Sulking = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Level));
        Assert.That(PersonalityGroupCalls.Decide(Free with { Sulking = true }, "leeroy", random), Is.EqualTo(eGroupCallAnswer.No), "sulking at the caller");
        Assert.That(PersonalityGroupCalls.Decide(null, "leeroy", random), Is.EqualTo(eGroupCallAnswer.Quiet));
    }

    [TestCase(1, 4)]
    [TestCase(20, 4)]
    [TestCase(30, 6)]
    [TestCase(50, 10)]
    public void LevelRange_GrowsWithTheCaller(int caller, int range)
    {
        Assert.That(PersonalityGroupCalls.LevelRange(caller), Is.EqualTo(range));
        var random = new Random(1);
        Assert.That(PersonalityGroupCalls.Decide(Free with { CallerLevel = caller, BotLevel = caller + range }, "leeroy", random),
            Is.Not.EqualTo(eGroupCallAnswer.Level), "just in range");
        Assert.That(PersonalityGroupCalls.Decide(Free with { CallerLevel = caller, BotLevel = caller + range + 1 }, "leeroy", random),
            Is.EqualTo(eGroupCallAnswer.Level), "one level over, higher");
        if (caller - range - 1 >= 1)
            Assert.That(PersonalityGroupCalls.Decide(Free with { CallerLevel = caller, BotLevel = caller - range - 1 }, "leeroy", random),
                Is.EqualTo(eGroupCallAnswer.Level), "one level over, lower");
        Assert.That(PersonalityGroupCalls.Decide(Free with { CallerLevel = caller, BotLevel = 0 }, "leeroy", random),
            Is.Not.EqualTo(eGroupCallAnswer.Level), "an unknown level is never the reason");
    }

    [Test]
    public void Keenness_DiffersByType()
    {
        int Yeses(string type)
        {
            var random = new Random(7);
            return Enumerable.Range(0, 1000).Count(_ => PersonalityGroupCalls.Decide(Free, type, random) == eGroupCallAnswer.Yes);
        }
        int loner = Yeses("loner"), crafter = Yeses("crafter"), casual = Yeses("casual"), leeroy = Yeses("leeroy");
        Assert.That(loner, Is.InRange(80, 230), "a loner says yes now and then");
        Assert.That(crafter, Is.GreaterThan(loner));
        Assert.That(casual, Is.GreaterThan(crafter));
        Assert.That(leeroy, Is.GreaterThan(casual).And.LessThan(1000), "even Leeroy says no now and then");
        Assert.That(Yeses("someone-new"), Is.InRange(420, 580), "an unknown type is even");
    }

    [Test]
    public void AtMostThreeAnswer_AndNeverTheQuietOnes()
    {
        var counts = new int[PersonalityGroupCalls.MaxAnswers + 1];
        for (int seed = 0; seed < 500; seed++)
        {
            var random = new Random(seed);
            var decided = Enumerable.Range(0, 10).Select(i => (i, i % 3 == 0 ? eGroupCallAnswer.Quiet : eGroupCallAnswer.Yes));
            List<(int Bot, eGroupCallAnswer Answer)> picked = PersonalityGroupCalls.Pick(decided, random);
            Assert.That(picked, Has.Count.LessThanOrEqualTo(PersonalityGroupCalls.MaxAnswers));
            Assert.That(picked.Select(pair => pair.Answer), Has.None.EqualTo(eGroupCallAnswer.Quiet));
            counts[picked.Count]++;
        }
        Assert.That(counts, Has.All.GreaterThan(0), "0, 1, 2 and 3 answers all happen");
        Assert.That(counts[0], Is.LessThan(counts[1]), "silence is the rare one");
        Assert.That(PersonalityGroupCalls.AnswerCount(1, new Random(1)), Is.InRange(0, 1));
        Assert.That(PersonalityGroupCalls.AnswerCount(0, new Random(1)), Is.Zero);

        PersonalityGroupCalls.Always = true;
        Assert.That(PersonalityGroupCalls.AnswerCount(2, new Random(1)), Is.EqualTo(2));
        Assert.That(PersonalityGroupCalls.AnswerCount(9, new Random(1)), Is.EqualTo(PersonalityGroupCalls.MaxAnswers));
    }

    [Test]
    public void ASecondCall_WithinTheCooldown_GetsNoAnswers()
    {
        Assert.That(PersonalityGroupCalls.TryStart("p1", 1_000), Is.True);
        Assert.That(PersonalityGroupCalls.TryStart("p1", 1_000 + PersonalityGroupCalls.CooldownMs - 1), Is.False);
        Assert.That(PersonalityGroupCalls.TryStart("p2", 1_500), Is.True, "another player's call is its own");
        Assert.That(PersonalityGroupCalls.TryStart("p1", 1_000 + PersonalityGroupCalls.CooldownMs), Is.True);
        Assert.That(PersonalityGroupCalls.TryStart(null, 1_000), Is.False);
    }

    [Test]
    public void AYes_IsAPromiseForAWhile()
    {
        Assert.That(PersonalityGroupCalls.Promised(7, "p1", 1_000), Is.False);
        PersonalityGroupCalls.Promise(7, "p1", 1_000);
        Assert.That(PersonalityGroupCalls.Promised(7, "p1", 1_000 + PersonalityGroupCalls.PromiseMs - 1), Is.True);
        Assert.That(PersonalityGroupCalls.Promised(8, "p1", 2_000), Is.False, "another bot");
        Assert.That(PersonalityGroupCalls.Promised(7, "p2", 2_000), Is.False, "another player");
        Assert.That(PersonalityGroupCalls.Promised(7, "p1", 1_000 + PersonalityGroupCalls.PromiseMs), Is.False, "expired");
    }

    [Test]
    public void TestHold_LastsItsTimeCappedAndClears()
    {
        Assert.That(PersonalityGroupCalls.IsHeld(7, 1_000), Is.False);
        PersonalityGroupCalls.Hold(7, 1_000, 90_000);
        Assert.That(PersonalityGroupCalls.IsHeld(7, 90_999), Is.True);
        Assert.That(PersonalityGroupCalls.IsHeld(8, 2_000), Is.False, "another bot");
        Assert.That(PersonalityGroupCalls.IsHeld(7, 91_000), Is.False, "over");
        PersonalityGroupCalls.Hold(7, 0, 10_000_000);
        Assert.That(PersonalityGroupCalls.IsHeld(7, PersonalityGroupCalls.MaxHoldMs), Is.False, "capped");
        PersonalityGroupCalls.Hold(7, 0, 10_000);
        PersonalityGroupCalls.Clear();
        Assert.That(PersonalityGroupCalls.IsHeld(7, 1), Is.False, "forgotten");
    }

    [Test]
    public void Remembered_StaysBounded()
    {
        for (int i = 0; i < 10_000; i++)
        {
            PersonalityGroupCalls.TryStart("p" + i, i);
            PersonalityGroupCalls.Promise(i, "p" + i, i);
            PersonalityGroupCalls.Hold(i, i, 1_000);
        }
        Assert.That(PersonalityGroupCalls.Promised(9_999, "p9999", 9_999), Is.True, "the newest is kept");
        Assert.That(PersonalityGroupCalls.IsHeld(9_999, 9_999), Is.True, "the newest hold is kept");
    }

    private static IEnumerable<BotPersonality> WorldVoices() =>
        PersonalityTypes.Templates.Values
            .Concat(PersonalityTypeVoices.NamedOnly.Values)
            .Concat(NamedBotLineup.All.Select(PersonalityTypes.Named));

    [Test]
    public void EveryWorldVoice_HasEveryAnswer_WithOnlyKnownSlots()
    {
        var allowed = PersonalityStateAnswers.Slots.Append("{0}").ToHashSet();
        foreach (BotPersonality voice in WorldVoices())
        foreach (eGroupCallAnswer answer in Spoken)
        {
            eAutonomousChatIntent kind = PersonalityGroupCalls.KindFor(answer);
            string where = $"{voice.Key} ~ reply {kind.ToString().ToLowerInvariant()}";
            string[] lines = voice.Replies.GetValueOrDefault(kind);
            Assert.That(lines, Is.Not.Null.And.Not.Empty, where);
            foreach (string line in lines)
            {
                foreach (Match slot in Placeholder.Matches(line))
                    Assert.That(allowed, Does.Contain(slot.Value), $"{where}: {line}");
                Assert.That(line, Does.Not.StartWith("{leader}").And.Not.Contain("{leader}'s"), $"{where}: {line}");
                Assert.That(line, Does.Not.Match(@"\ban? \{(class|monster)\}"), $"{where}: {line}");
            }
            Assert.That(lines.Any(line => !PersonalityStateAnswers.HasSlot(line) && DialogueLibrary.RuleFor(line) == null), Is.True,
                $"{where}: needs a plain line for when nothing is known");
        }
    }

    [Test]
    public void EveryWorldVoice_SaysSomething_KnownOrNot()
    {
        var known = new StateSlots("Vale of Mularn", 20, "Thane", "rage wolf", "Mularn", 3, "Bob");
        var unknown = new StateSlots(null, 0, null, null, null, 0, null);
        var fighting = new DialogueFacts { BotDoing = "fighting", BotGrouped = true };
        foreach (BotPersonality voice in WorldVoices())
        foreach (eGroupCallAnswer answer in Spoken)
        foreach (StateSlots slots in new[] { known, unknown })
        for (int seed = 0; seed < 4; seed++)
        {
            string line = PersonalityGroupCalls.Line(voice, answer, slots, fighting, "Tester", new Random(seed));
            Assert.That(line, Is.Not.Null.And.Not.Empty.And.Not.Contain("{"), $"{voice.Key} {answer}");
        }
    }

    [Test]
    public void Line_FallsBack_AndNeverMakesUpALevel()
    {
        var bot = new BotPersonality
        {
            Name = "Testy", Key = "test-groupcall", Matches = _ => false,
            Replies = new Dictionary<eAutonomousChatIntent, string[]>
            {
                [eAutonomousChatIntent.Grouping] = ["Sure, {0}."],
                [eAutonomousChatIntent.GroupNo] = ["Nope, {0}."],
                [eAutonomousChatIntent.GroupLevel] = ["I'm {level}, {0}."],
            },
        };
        var slots = new StateSlots(null, 0, null, null, null, 0, null);
        Func<string, DialogueRule> noRules = _ => null;
        Assert.That(PersonalityGroupCalls.Line(bot, eGroupCallAnswer.Yes, slots, null, "Bob", null, noRules), Is.EqualTo("Sure, Bob."), "yes: its old grouping line");
        Assert.That(PersonalityGroupCalls.Line(bot, eGroupCallAnswer.Busy, slots, null, "Bob", null, noRules), Is.EqualTo("Nope, Bob."), "a reason without lines: its no");
        Assert.That(PersonalityGroupCalls.Line(bot, eGroupCallAnswer.Level, slots, null, "Bob", null, noRules), Is.EqualTo("Nope, Bob."),
            "an unknown level isn't said: its no instead");
        Assert.That(PersonalityGroupCalls.Line(bot, eGroupCallAnswer.Level, slots with { Level = 12 }, null, "Bob", null, noRules), Is.EqualTo("I'm 12, Bob."));
        Assert.That(PersonalityGroupCalls.Line(bot, eGroupCallAnswer.Quiet, slots, null, "Bob", null, noRules), Is.Null);
        Assert.That(PersonalityGroupCalls.Line(null, eGroupCallAnswer.Yes, slots, null, "Bob", null, noRules), Is.Null);
    }
}
