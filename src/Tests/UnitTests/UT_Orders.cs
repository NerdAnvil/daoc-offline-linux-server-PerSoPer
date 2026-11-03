using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B6: orders in chat ("follow me", "shemp, stay here", "come here") are read, and each bot decides from
/// its kind and state: never for someone who doesn't lead it or own it, busy while fighting or riding, a companion
/// always obeys its owner otherwise, a world bot says no when sulking and now and then refuses "stay" by type. A stay
/// holds for a capped time and only while the leader stays in the region; answers are few and have a cooldown.
/// B6b: "leave the group" (named only; a companion says no), "assist me" (only while the leader fights something),
/// "rest up" (full when there's nothing to rest off), a bot that stormed off after a bad joke refusing in character,
/// Kobaldjovi's own lines and the nicknames of Spyburd, Eaglefang and Hunterson.
/// </summary>
[TestFixture]
public class UT_Orders
{
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    private static readonly eAutonomousChatIntent[] OrderKinds =
    [
        eAutonomousChatIntent.OrderFollow, eAutonomousChatIntent.OrderStay, eAutonomousChatIntent.OrderCome,
        eAutonomousChatIntent.OrderLeave, eAutonomousChatIntent.OrderAssist, eAutonomousChatIntent.OrderRest,
        eAutonomousChatIntent.OrderNo, eAutonomousChatIntent.OrderBusy, eAutonomousChatIntent.OrderNoTarget,
        eAutonomousChatIntent.OrderFull, eAutonomousChatIntent.OrderAway, eAutonomousChatIntent.OrderNot,
        eAutonomousChatIntent.OrderPull, eAutonomousChatIntent.OrderPullWhat, eAutonomousChatIntent.OrderPullEasy,
    ];

    private static readonly eBotOrder[] AllOrders =
        [eBotOrder.Follow, eBotOrder.Stay, eBotOrder.Come, eBotOrder.Leave, eBotOrder.Assist, eBotOrder.Rest, eBotOrder.Pull];

    // A world bot in the speaker's group, led by the speaker, free.
    private static readonly OrderState Free = new(true, false, false, false, false);

    [SetUp]
    public void Forget()
    {
        DialoguePicker.Shared.Clear();
        PersonalityOrders.Clear();
        PersonalityOrders.Always = false;
    }

    [TestCase("follow me", eBotOrder.Follow)]
    [TestCase("Follow me!", eBotOrder.Follow)]
    [TestCase("ok guys, stick with me", eBotOrder.Follow)]
    [TestCase("follow", eBotOrder.Follow)]
    [TestCase("stay here", eBotOrder.Stay)]
    [TestCase("wait here a sec", eBotOrder.Stay)]
    [TestCase("hold up", eBotOrder.Stay)]
    [TestCase("stay put", eBotOrder.Stay)]
    [TestCase("stop following", eBotOrder.Stay)]
    [TestCase("stay", eBotOrder.Stay)]
    [TestCase("wait", eBotOrder.Stay)]
    [TestCase("stop!", eBotOrder.Stay)]
    [TestCase("come here", eBotOrder.Come)]
    [TestCase("get over here", eBotOrder.Come)]
    [TestCase("regroup", eBotOrder.Come)]
    [TestCase("come", eBotOrder.Come)]
    [TestCase("leave the group", eBotOrder.Leave)]
    [TestCase("can you make room please", eBotOrder.Leave)]
    [TestCase("assist me", eBotOrder.Assist)]
    [TestCase("attack my target!", eBotOrder.Assist)]
    [TestCase("assist", eBotOrder.Assist)]
    [TestCase("rest up", eBotOrder.Rest)]
    [TestCase("ok everyone sit down", eBotOrder.Rest)]
    [TestCase("med up guys", eBotOrder.Rest)]
    [TestCase("rest", eBotOrder.Rest)]
    [TestCase("sit", eBotOrder.Rest)]
    [TestCase("pull this", eBotOrder.Pull)]
    [TestCase("pull that one please", eBotOrder.Pull)]
    [TestCase("pull", eBotOrder.Pull)]
    public void Orders_AreRead(string line, eBotOrder expected)
    {
        Assert.That(AutonomousChatIntentModel.ReadOrder(line), Is.EqualTo(expected), line);
        Assert.That(AutonomousChatIntentModel.Read(line), Is.EqualTo(eAutonomousChatIntent.Command), line);
    }

    [TestCase("shemp, stay here", eBotOrder.Stay)]
    [TestCase("shemp stay", eBotOrder.Stay)]
    [TestCase("Shempenstein wait", eBotOrder.Stay)]
    [TestCase("persoper follow", eBotOrder.Follow)]
    [TestCase("shemp come here", eBotOrder.Come)]
    [TestCase("shemp, leave the group", eBotOrder.Leave)]
    [TestCase("persoper assist", eBotOrder.Assist)]
    [TestCase("shemp rest", eBotOrder.Rest)]
    [TestCase("shemp, pull this", eBotOrder.Pull)]
    [TestCase("persoper pull", eBotOrder.Pull)]
    public void TheBotsName_IsTakenOffFirst(string line, eBotOrder expected)
    {
        Assert.That(AutonomousChatIntentModel.ReadOrder(line, ["Shempenstein", "Shemp", "Persoper"]), Is.EqualTo(expected), line);
    }

    [TestCase("shnacki, stay here", "shnacki")]
    [TestCase("persi come here", "persi")]
    [TestCase("stay here, shnacki", "shnacki")]
    [TestCase("bob, follow me", "bob")]
    public void AnUnknownName_IsAStranger_NotEveryone(string line, string stranger)
    {
        Assert.That(AutonomousChatIntentModel.ReadOrder(line, ["Shempenstein", "Shemp"]), Is.Not.EqualTo(eBotOrder.None), line);
        Assert.That(AutonomousChatIntentModel.OrderStranger(line, ["Shempenstein", "Shemp"]), Is.EqualTo(stranger), line);
    }

    [TestCase("stay here")]
    [TestCase("ok guys, stay here")]
    [TestCase("everyone come here please")]
    [TestCase("follow me")]
    [TestCase("hold up, guys")]
    [TestCase("wait for me at the bridge")]
    [TestCase("come here, now")]
    [TestCase("shemp, stay here")]
    [TestCase("stay here, Shempenstein")]
    [TestCase("stay")]
    [TestCase("make room")]
    [TestCase("shemp, leave the group")]
    [TestCase("assist me")]
    [TestCase("ok guys, med up")]
    [TestCase("pull this")]
    [TestCase("shemp, pull this")]
    public void OpenOrdersAndKnownNames_HaveNoStranger(string line)
    {
        Assert.That(AutonomousChatIntentModel.OrderStranger(line, ["Shempenstein", "Shemp"]), Is.Null, line);
    }

    [Test]
    public void Nicknames_ForShnacksizeAndPersoper()
    {
        Assert.That(PersonalityTypes.Named(NamedBotLineup.All.First(entry => entry.Name == "Shnacksize")).Nicknames, Does.Contain("Shnacki"));
        Assert.That(PersoperIdentity.Personality.Nicknames, Does.Contain("Persi"));
    }

    [TestCase("Spyburd", "Burd")]
    [TestCase("Spyburde", "Burd")]
    [TestCase("Spyburdagh", "Burd")]
    [TestCase("Eaglefang", "Eagle")]
    [TestCase("Eaglefangh", "Fang")]
    [TestCase("Hunterson", "Hunty")]
    public void Nicknames_ForSpyburdEaglefangAndHunterson(string bot, string nickname)
    {
        Assert.That(PersonalityTypes.Named(NamedBotLineup.All.First(entry => entry.Name == bot)).Nicknames, Does.Contain(nickname));
    }

    [TestCase("hello")]
    [TestCase("that seems odd to me")]
    [TestCase("it's on me")]
    [TestCase("keep up the good work")]
    [TestCase("I can't wait to hit 50")]
    [TestCase("stop it")]
    [TestCase("who wants to group?")]
    [TestCase("go pull")]
    [TestCase("did you get it?")]
    [TestCase("I need to take a break")]
    [TestCase("please leave a review")]
    [TestCase("can someone help me")]
    [TestCase("stay here, you idiot")]
    [TestCase("shemp, where are you?")]
    [TestCase("nice pull")]
    [TestCase("that was a bad pull")]
    public void OtherLines_AreNotOrders(string line)
    {
        Assert.That(AutonomousChatIntentModel.ReadOrder(line, ["Shemp"]), Is.EqualTo(eBotOrder.None), line);
    }

    [Test]
    public void OrderKinds_AreNotSmallTalk_AndDistinct()
    {
        foreach (eAutonomousChatIntent kind in OrderKinds)
        {
            Assert.That(AutonomousChatIntentModel.IsOrderKind(kind), Is.True, kind.ToString());
            Assert.That(AutonomousChatIntentModel.IsSmallTalk(kind), Is.False, kind.ToString());
        }
        Assert.That(AutonomousChatIntentModel.IsOrderKind(eAutonomousChatIntent.Command), Is.False);
        var kinds = new List<eAutonomousChatIntent>();
        foreach (eBotOrder order in AllOrders)
            kinds.Add(PersonalityOrders.KindFor(order, eOrderAnswer.Yes));
        kinds.AddRange(new[] { eOrderAnswer.No, eOrderAnswer.Busy, eOrderAnswer.NoTarget, eOrderAnswer.Full, eOrderAnswer.Away, eOrderAnswer.NotYours }
            .Select(answer => PersonalityOrders.KindFor(eBotOrder.Stay, answer)));
        kinds.Add(PersonalityOrders.KindFor(eBotOrder.Pull, eOrderAnswer.NoTarget));
        kinds.Add(PersonalityOrders.KindFor(eBotOrder.Pull, eOrderAnswer.TooEasy));
        Assert.That(kinds, Is.EquivalentTo(OrderKinds));
        Assert.That(PersonalityOrders.KindFor(eBotOrder.Follow, eOrderAnswer.Quiet), Is.EqualTo(eAutonomousChatIntent.Unknown));
    }

    [Test]
    public void Decide_NeverForSomeoneWhoDoesntLeadOrOwnIt()
    {
        var random = new Random(1);
        foreach (eBotOrder order in AllOrders)
        {
            Assert.That(PersonalityOrders.Decide(order, Free with { Allowed = false }, "casual", random), Is.EqualTo(eOrderAnswer.NotYours));
            Assert.That(PersonalityOrders.Decide(order, Free with { Allowed = false, Companion = true, Fighting = true }, "casual", random),
                Is.EqualTo(eOrderAnswer.NotYours), "not yours beats busy");
        }
        Assert.That(PersonalityOrders.Decide(eBotOrder.None, Free, "casual", random), Is.EqualTo(eOrderAnswer.Quiet));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Stay, null, "casual", random), Is.EqualTo(eOrderAnswer.Quiet));
    }

    [Test]
    public void Decide_BusyWhileFightingOrRiding_EvenACompanion()
    {
        var random = new Random(2);
        Assert.That(PersonalityOrders.Decide(eBotOrder.Stay, Free with { Fighting = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Come, Free with { Riding = true, Sulking = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Follow, Free with { Companion = true, Fighting = true }, null, random), Is.EqualTo(eOrderAnswer.Busy));
    }

    [Test]
    public void Decide_ACompanionAlwaysObeys_WhenFree()
    {
        for (int seed = 0; seed < 200; seed++)
        foreach (eBotOrder order in new[] { eBotOrder.Follow, eBotOrder.Stay, eBotOrder.Come })
            Assert.That(PersonalityOrders.Decide(order, Free with { Companion = true, Sulking = true }, "leeroy", new Random(seed)),
                Is.EqualTo(eOrderAnswer.Yes), $"{order} seed {seed}");
    }

    [Test]
    public void Decide_AWorldBotSulking_SaysNo()
    {
        Assert.That(PersonalityOrders.Decide(eBotOrder.Follow, Free with { Sulking = true }, "casual", new Random(3)), Is.EqualTo(eOrderAnswer.No));
    }

    [TestCase("leeroy")]
    [TestCase("loner")]
    [TestCase("casual")]
    public void Decide_StayIsRefusedByType_FollowAndComeNever(string type)
    {
        int refused = 0;
        const int Tries = 4_000;
        var random = new Random(7);
        for (int i = 0; i < Tries; i++)
        {
            if (PersonalityOrders.Decide(eBotOrder.Stay, Free, type, random) == eOrderAnswer.No)
                refused++;
            Assert.That(PersonalityOrders.Decide(eBotOrder.Follow, Free, type, random), Is.EqualTo(eOrderAnswer.Yes));
            Assert.That(PersonalityOrders.Decide(eBotOrder.Come, Free, type, random), Is.EqualTo(eOrderAnswer.Yes));
        }
        Assert.That(refused / (double)Tries, Is.EqualTo(PersonalityOrders.RefuseChance(type)).Within(0.03), type);

        PersonalityOrders.Always = true;
        for (int i = 0; i < 200; i++)
            Assert.That(PersonalityOrders.Decide(eBotOrder.Stay, Free, type, random), Is.EqualTo(eOrderAnswer.Yes), "test switch: no refusals");
    }

    [Test]
    public void RefuseChance_LeeroyHatesWaiting()
    {
        Assert.That(PersonalityOrders.RefuseChance("leeroy"), Is.GreaterThan(PersonalityOrders.RefuseChance("loner")));
        Assert.That(PersonalityOrders.RefuseChance("loner"), Is.GreaterThan(PersonalityOrders.RefuseChance("casual")));
        Assert.That(PersonalityOrders.RefuseChance("unknown-type"), Is.EqualTo(0.05));
    }

    [Test]
    public void Stay_HoldsForAtMostFiveMinutes_InTheSameRegion()
    {
        PersonalityOrders.Stay(5, 100, 1_000);
        Assert.That(PersonalityOrders.Holds(5, 100, 1_000), Is.True);
        Assert.That(PersonalityOrders.Holds(5, 100, 1_000 + PersonalityOrders.MaxStayMs - 1), Is.True);
        Assert.That(PersonalityOrders.StaySecondsLeft(5, 1_000), Is.EqualTo(PersonalityOrders.MaxStayMs / 1_000));
        Assert.That(PersonalityOrders.Holds(5, 100, 1_000 + PersonalityOrders.MaxStayMs), Is.False, "capped in time");
        Assert.That(PersonalityOrders.Holds(5, 100, 2_000), Is.False, "an ended stay is forgotten");
        Assert.That(PersonalityOrders.StaySecondsLeft(5, 2_000), Is.EqualTo(0));

        PersonalityOrders.Stay(6, 100, 0);
        Assert.That(PersonalityOrders.Holds(6, 101, 10), Is.False, "the leader left the region");
        Assert.That(PersonalityOrders.Holds(6, 100, 20), Is.False, "and the stay is over");

        PersonalityOrders.Stay(7, 100, 0);
        PersonalityOrders.Release(7);
        Assert.That(PersonalityOrders.Holds(7, 100, 10), Is.False, "follow or come ends it");
        Assert.That(PersonalityOrders.Holds(8, 100, 10), Is.False, "never told");
    }

    [Test]
    public void Remembered_StaysBounded()
    {
        for (int i = 0; i < 10_000; i++)
        {
            PersonalityOrders.TryStart("p" + i, i * 10_000L);
            PersonalityOrders.Stay(i, 1, i);
        }
        Assert.That(PersonalityOrders.Holds(9_999, 1, 9_999), Is.True, "the newest stay is kept");
        Assert.That(PersonalityOrders.TryStart("p9999", 9_999 * 10_000L + 1), Is.False, "the newest cooldown is kept");
    }

    [Test]
    public void Cooldown_SilencesAnswers_NotObedience()
    {
        Assert.That(PersonalityOrders.TryStart("p", 0), Is.True);
        Assert.That(PersonalityOrders.TryStart("p", PersonalityOrders.CooldownMs - 1), Is.False);
        Assert.That(PersonalityOrders.TryStart("other", 10), Is.True, "per player");
        Assert.That(PersonalityOrders.TryStart("p", PersonalityOrders.CooldownMs), Is.True);
        Assert.That(PersonalityOrders.TryStart(null, 0), Is.False);
    }

    [Test]
    public void AnswerCount_OneForANamedOrder_FewForAnOpenOne()
    {
        var random = new Random(11);
        int namedSilent = 0;
        for (int i = 0; i < 2_000; i++)
        {
            int named = PersonalityOrders.AnswerCount(5, true, random);
            Assert.That(named, Is.InRange(0, 1));
            if (named == 0) namedSilent++;
            Assert.That(PersonalityOrders.AnswerCount(8, false, random), Is.InRange(0, PersonalityOrders.MaxOpenAnswers));
            Assert.That(PersonalityOrders.AnswerCount(1, false, random), Is.InRange(0, 1));
        }
        Assert.That(namedSilent, Is.InRange(100, 300), "a named bot sometimes just does it");
        Assert.That(PersonalityOrders.AnswerCount(0, true, random), Is.EqualTo(0));

        PersonalityOrders.Always = true;
        Assert.That(PersonalityOrders.AnswerCount(5, true, random), Is.EqualTo(1));
        Assert.That(PersonalityOrders.AnswerCount(5, false, random), Is.EqualTo(PersonalityOrders.MaxOpenAnswers));
    }

    [Test]
    public void Pick_StrangersIgnoreAnOpenOrder()
    {
        PersonalityOrders.Always = true;
        var decided = new[] { ("stranger", eOrderAnswer.NotYours), ("quiet", eOrderAnswer.Quiet), ("member", eOrderAnswer.Yes) };
        List<(string Bot, eOrderAnswer Answer)> open = PersonalityOrders.Pick(decided, false, new Random(1));
        Assert.That(open.Select(pair => pair.Bot), Is.EqualTo(new[] { "member" }));
        List<(string Bot, eOrderAnswer Answer)> named = PersonalityOrders.Pick(new[] { ("stranger", eOrderAnswer.NotYours) }, true, new Random(1));
        Assert.That(named.Select(pair => pair.Answer), Is.EqualTo(new[] { eOrderAnswer.NotYours }));
    }

    [Test]
    public void Line_FallsBack_AndAYesWithoutLinesIsSilence()
    {
        var bot = new BotPersonality
        {
            Name = "Testy", Key = "test-orders", Matches = _ => false,
            Replies = new Dictionary<eAutonomousChatIntent, string[]>
            {
                [eAutonomousChatIntent.OrderStay] = ["Staying, {0}."],
                [eAutonomousChatIntent.OrderNo] = ["Nope, {0}."],
            },
        };
        var slots = new StateSlots(null, 0, null, null, null, 0, null);
        Func<string, DialogueRule> noRules = _ => null;
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Stay, eOrderAnswer.Yes, slots, null, "Bob", null, noRules), Is.EqualTo("Staying, Bob."));
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Follow, eOrderAnswer.Yes, slots, null, "Bob", null, noRules), Is.Null, "no follow lines: it just follows");
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Come, eOrderAnswer.Busy, slots, null, "Bob", null, noRules), Is.EqualTo("Nope, Bob."), "busy without lines: its no");
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Come, eOrderAnswer.NotYours, slots, null, "Bob", null, noRules), Is.Null);
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Come, eOrderAnswer.Quiet, slots, null, "Bob", null, noRules), Is.Null);
        Assert.That(PersonalityOrders.Line(null, eBotOrder.Come, eOrderAnswer.Yes, slots, null, "Bob", null, noRules), Is.Null);
        foreach (eOrderAnswer answer in new[] { eOrderAnswer.NoTarget, eOrderAnswer.Full, eOrderAnswer.Away })
            Assert.That(PersonalityOrders.Line(bot, eBotOrder.Assist, answer, slots, null, "Bob", null, noRules), Is.EqualTo("Nope, Bob."), $"{answer} without lines: its no");
        Assert.That(PersonalityOrders.Line(bot, eBotOrder.Leave, eOrderAnswer.Yes, slots, null, "Bob", null, noRules), Is.Null, "no leave lines: it just goes");
    }

    [Test]
    public void Decide_AwayAfterABadJoke_RefusesEverything_EvenACompanion()
    {
        var random = new Random(5);
        foreach (eBotOrder order in AllOrders)
        {
            Assert.That(PersonalityOrders.Decide(order, Free with { Away = true }, "casual", random), Is.EqualTo(eOrderAnswer.Away), order.ToString());
            Assert.That(PersonalityOrders.Decide(order, Free with { Away = true, Companion = true }, null, random), Is.EqualTo(eOrderAnswer.Away), order.ToString());
            Assert.That(PersonalityOrders.Decide(order, Free with { Away = true, Allowed = false }, "casual", random), Is.EqualTo(eOrderAnswer.Away),
                "a world bot that stormed off is out of the group, and says so in character");
        }
    }

    [Test]
    public void Decide_Leave_AWorldBotGoes_ACompanionSaysNo()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free, "leeroy", new Random(seed)), Is.EqualTo(eOrderAnswer.Yes));
            Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free with { Sulking = true }, "casual", new Random(seed)), Is.EqualTo(eOrderAnswer.Yes),
                "a sulk only makes it gladder to go");
            Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free with { Companion = true }, null, new Random(seed)), Is.EqualTo(eOrderAnswer.No));
        }
        Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free with { Fighting = true }, "casual", new Random(1)), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free with { Riding = true }, "casual", new Random(1)), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Leave, Free with { Allowed = false }, "casual", new Random(1)), Is.EqualTo(eOrderAnswer.NotYours));
        Assert.That(PersonalityOrders.NamedOnly(eBotOrder.Leave), Is.True, "an open \"leave the group\" goes to nobody");
        foreach (eBotOrder order in AllOrders.Where(order => order != eBotOrder.Leave))
            Assert.That(PersonalityOrders.NamedOnly(order), Is.False, order.ToString());
    }

    [Test]
    public void Decide_Assist_OnlyWhileTheLeaderFights_AndFightingIsNoExcuse()
    {
        var random = new Random(6);
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free, "casual", random), Is.EqualTo(eOrderAnswer.NoTarget));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { HasTarget = true }, "casual", random), Is.EqualTo(eOrderAnswer.Yes));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { HasTarget = true, Fighting = true }, "casual", random), Is.EqualTo(eOrderAnswer.Yes),
            "already fighting: it joins the leader's target");
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { HasTarget = true, Riding = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { HasTarget = true, Sulking = true }, "casual", random), Is.EqualTo(eOrderAnswer.No));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { HasTarget = true, Companion = true, Sulking = true }, null, random), Is.EqualTo(eOrderAnswer.Yes));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Assist, Free with { Companion = true }, null, random), Is.EqualTo(eOrderAnswer.NoTarget));
    }

    [Test]
    public void Decide_Rest_FullWhenThereIsNothingToRestOff()
    {
        var random = new Random(8);
        Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free, "casual", random), Is.EqualTo(eOrderAnswer.Yes));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free with { NeedsRest = false }, "casual", random), Is.EqualTo(eOrderAnswer.Full));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free with { NeedsRest = false, Companion = true }, null, random), Is.EqualTo(eOrderAnswer.Full));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free with { Fighting = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free with { Sulking = true }, "casual", random), Is.EqualTo(eOrderAnswer.No));
        for (int seed = 0; seed < 200; seed++)
            Assert.That(PersonalityOrders.Decide(eBotOrder.Rest, Free, "leeroy", new Random(seed)), Is.EqualTo(eOrderAnswer.Yes), "only stay is refused by type");
    }

    [Test]
    public void Decide_Pull_NeedsATarget_AndASupportBotPullsToo()
    {
        var random = new Random(9);
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free, "casual", random), Is.EqualTo(eOrderAnswer.NoTarget));
        Assert.That(PersonalityOrders.KindFor(eBotOrder.Pull, eOrderAnswer.NoTarget), Is.EqualTo(eAutonomousChatIntent.OrderPullWhat), "pull what?");
        Assert.That(PersonalityOrders.KindFor(eBotOrder.Assist, eOrderAnswer.NoTarget), Is.EqualTo(eAutonomousChatIntent.OrderNoTarget), "assist keeps on what?");
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { HasTarget = true }, "casual", random), Is.EqualTo(eOrderAnswer.Yes));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { HasTarget = true, Companion = true }, null, random), Is.EqualTo(eOrderAnswer.Yes));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { HasTarget = true, Fighting = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { HasTarget = true, Sulking = true }, "casual", random), Is.EqualTo(eOrderAnswer.No));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { HasTarget = true, Allowed = false }, "casual", random), Is.EqualTo(eOrderAnswer.NotYours));
        Assert.That(PersonalityOrders.NamedOnly(eBotOrder.Pull), Is.False, "pull this with no name is the plain /pull");
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { TargetGrey = true }, "casual", random), Is.EqualTo(eOrderAnswer.TooEasy), "grey: too easy");
        Assert.That(PersonalityOrders.KindFor(eBotOrder.Pull, eOrderAnswer.TooEasy), Is.EqualTo(eAutonomousChatIntent.OrderPullEasy));
        Assert.That(PersonalityOrders.Decide(eBotOrder.Pull, Free with { TargetGrey = true, Fighting = true }, "casual", random), Is.EqualTo(eOrderAnswer.Busy));
    }

    private static IEnumerable<BotPersonality> WorldVoices() =>
        PersonalityTypes.Templates.Values
            .Concat(PersonalityTypeVoices.NamedOnly.Values)
            .Concat(NamedBotLineup.All.Select(PersonalityTypes.Named));

    private static IEnumerable<(BotPersonality Voice, eAutonomousChatIntent Kind)> VoicesAndKinds() =>
        WorldVoices().SelectMany(voice => OrderKinds.Select(kind => (voice, kind)))
            .Concat(OrderKinds.Where(kind => kind != eAutonomousChatIntent.OrderLeave).Select(kind => (Persoper(), kind)))
            .Concat(OrderKinds.Select(kind => (KobaldjoviIdentity.Personality, kind)));

    private static BotPersonality Persoper() => PersoperIdentity.Personality;

    [Test]
    public void EveryWorldVoice_AndPersoper_HasEveryAnswer_WithOnlyKnownSlots()
    {
        var allowed = PersonalityStateAnswers.Slots.Append("{0}").ToHashSet();
        foreach ((BotPersonality voice, eAutonomousChatIntent kind) in VoicesAndKinds())
        {
            string where = $"{voice.Key} ~ reply {kind.ToString().ToLowerInvariant()}";
            string[] lines = voice.Replies.GetValueOrDefault(kind);
            Assert.That(lines, Is.Not.Null.And.Not.Empty, where);
            foreach (string line in lines)
            {
                foreach (Match slot in Placeholder.Matches(line))
                    Assert.That(allowed, Does.Contain(slot.Value), $"{where}: {line}");
                Assert.That(line, Does.Not.StartWith("{leader}"), $"{where}: {line}");
            }
            Assert.That(lines.Any(line => !PersonalityStateAnswers.HasSlot(line) && DialogueLibrary.RuleFor(line) == null), Is.True,
                $"{where}: needs a plain line for when nothing is known");
        }
    }

    [Test]
    public void EveryWorldVoice_AndPersoper_SaysSomething_KnownOrNot()
    {
        var known = new StateSlots("Vale of Mularn", 20, "Thane", "rage wolf", "Mularn", 3, "Bob");
        var unknown = new StateSlots(null, 0, null, null, null, 0, null);
        var fighting = new DialogueFacts { BotDoing = "fighting", BotGrouped = true };
        var answers = new (eBotOrder Order, eOrderAnswer Answer)[]
        {
            (eBotOrder.Follow, eOrderAnswer.Yes), (eBotOrder.Stay, eOrderAnswer.Yes), (eBotOrder.Come, eOrderAnswer.Yes),
            (eBotOrder.Stay, eOrderAnswer.Busy), (eBotOrder.Stay, eOrderAnswer.NotYours),
            (eBotOrder.Assist, eOrderAnswer.Yes), (eBotOrder.Rest, eOrderAnswer.Yes), (eBotOrder.Assist, eOrderAnswer.NoTarget),
            (eBotOrder.Rest, eOrderAnswer.Full), (eBotOrder.Follow, eOrderAnswer.Away), (eBotOrder.Leave, eOrderAnswer.No),
            (eBotOrder.Pull, eOrderAnswer.Yes), (eBotOrder.Pull, eOrderAnswer.NoTarget), (eBotOrder.Pull, eOrderAnswer.TooEasy),
        };
        foreach (BotPersonality voice in WorldVoices().Append(Persoper()).Append(KobaldjoviIdentity.Personality))
        foreach ((eBotOrder order, eOrderAnswer answer) in answers)
        foreach (StateSlots slots in new[] { known, unknown })
        for (int seed = 0; seed < 4; seed++)
        {
            string line = PersonalityOrders.Line(voice, order, answer, slots, fighting, "Tester", new Random(seed));
            Assert.That(line, Is.Not.Null.And.Not.Empty.And.Not.Contain("{"), $"{voice.Key} {order} {answer}");
        }
    }
}
