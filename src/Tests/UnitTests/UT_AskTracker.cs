using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_AskTracker
{
    private static readonly DateTime Now = new(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);

    private static DialogueSection Music() => DialogueFile.Parse(
        "=== persoper\n~ ask fav_music\nMetal or ballads?\n-> metal: Taste!\n-> ballads: Softie.\n-> other: Hm.\n",
        "t.dlg").Block("persoper").Single();

    [TestCase("metal", "metal")]
    [TestCase("Metal!", "metal")]
    [TestCase("I like metal a lot", "metal")]
    [TestCase("ballads or metal", "ballads")]
    [TestCase("jazz", "other")]
    [TestCase("no idea", "other")]
    public void Match_PicksTheFirstAnswerWord_ElseOther(string line, string expected)
    {
        Assert.That(AskTracker.Match(Music(), line).Key, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1 2 3")]
    [TestCase("well i am not sure what i would say to that at all")]
    public void Match_AnEmptyOrLongLine_IsNotAnAnswer(string line)
    {
        Assert.That(AskTracker.Match(Music(), line), Is.Null);
    }

    [Test]
    public void Match_WithoutOther_AnAnswerWordStillCounts_ButChatDoesNot()
    {
        Assert.That(AskTracker.Match(Music(), "persoper, metal", allowOther: false).Key, Is.EqualTo("metal"));
        Assert.That(AskTracker.Match(Music(), "persoper, hello", allowOther: false), Is.Null);
    }

    [Test]
    public void ALineToHerByName_WithNoAnswerWord_LeavesTheQuestionOpen()
    {
        var tracker = new AskTracker();
        tracker.Start("persoper", "p1", Music(), Now);

        Assert.That(tracker.TryAnswer("p1", "persoper, hello", Now.AddSeconds(3), "persoper", allowOther: false), Is.Null);
        Assert.That(tracker.IsOpen("p1", Now.AddSeconds(4)), Is.True);
        Assert.That(tracker.TryAnswer("p1", "jazz", Now.AddSeconds(5), "persoper", allowOther: false), Is.Null);
        Assert.That(tracker.TryAnswer("p1", "jazz", Now.AddSeconds(6), "persoper").Answer.Key, Is.EqualTo("other"));
    }

    [Test]
    public void Match_TheOtherRowIsNotAWordToSay()
    {
        Assert.That(AskTracker.Match(Music(), "other").Reply, Is.EqualTo("Hm."));
        Assert.That(AskTracker.Match(Music(), "other").Key, Is.EqualTo("other"));
    }

    [Test]
    public void TryAnswer_GivesTheReplyOnce_AndClosesTheQuestion()
    {
        var tracker = new AskTracker();
        Assert.That(tracker.Start("persoper", "p1", Music(), Now), Is.True);

        AskOutcome outcome = tracker.TryAnswer("p1", "metal", Now.AddSeconds(5), "persoper");

        Assert.That((outcome.Ask.Argument, outcome.Answer.Key, outcome.Answer.Reply), Is.EqualTo(("fav_music", "metal", "Taste!")));
        Assert.That(tracker.IsOpen("p1", Now.AddSeconds(6)), Is.False);
        Assert.That(tracker.TryAnswer("p1", "metal", Now.AddSeconds(7)), Is.Null);
    }

    [Test]
    public void ALongLine_LeavesTheQuestionOpen()
    {
        var tracker = new AskTracker();
        tracker.Start("persoper", "p1", Music(), Now);

        Assert.That(tracker.TryAnswer("p1", "well i am not sure what i would say to that at all", Now.AddSeconds(5)), Is.Null);
        Assert.That(tracker.IsOpen("p1", Now.AddSeconds(6)), Is.True);
        Assert.That(tracker.TryAnswer("p1", "ballads", Now.AddSeconds(8)).Answer.Key, Is.EqualTo("ballads"));
    }

    [Test]
    public void TheQuestion_DropsAfterAbout60Seconds()
    {
        var tracker = new AskTracker();
        tracker.Start("persoper", "p1", Music(), Now);

        Assert.That(tracker.IsOpen("p1", Now.AddSeconds(59)), Is.True);
        Assert.That(tracker.IsOpen("p1", Now + AskTracker.Timeout), Is.False);
        Assert.That(tracker.TryAnswer("p1", "metal", Now + AskTracker.Timeout), Is.Null);
    }

    [Test]
    public void AnswerFromAnotherBot_OrPlayer_DoesNotCount()
    {
        var tracker = new AskTracker();
        tracker.Start("persoper", "p1", Music(), Now);

        Assert.That(tracker.TryAnswer("p1", "metal", Now, "kobaldjovi"), Is.Null);
        Assert.That(tracker.TryAnswer("p2", "metal", Now), Is.Null);
        Assert.That(tracker.IsOpen("p1", Now, "persoper"), Is.True);
    }

    [Test]
    public void Start_OneOpenQuestionPerPlayer_AndAnExpiredOneMakesRoom()
    {
        var tracker = new AskTracker();
        Assert.That(tracker.Start("persoper", "p1", Music(), Now), Is.True);
        Assert.That(tracker.Start("persoper", "p1", Music(), Now.AddSeconds(10)), Is.False);
        Assert.That(tracker.Start("persoper", "p1", Music(), Now + AskTracker.Timeout), Is.True);
    }

    [Test]
    public void Start_StopsAtTheOpenCap_AndRejectsABadAsk()
    {
        var tracker = new AskTracker();
        for (int i = 0; i < AskTracker.MaxOpen; i++)
            Assert.That(tracker.Start("persoper", $"p{i}", Music(), Now), Is.True);

        Assert.That(tracker.Start("persoper", "one_more", Music(), Now), Is.False);
        Assert.That(tracker.Start("persoper", "p", null, Now), Is.False);
        Assert.That(tracker.Start("persoper", "", Music(), Now), Is.False);
    }

    [Test]
    public void PersoperFile_HasTheFavMusicQuestion_AndLinesForItsAnswers()
    {
        DialogueFile file = DialogueLibrary.For("persoper");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(DialogueCheck.Problems(file, "persoper", null), Is.Empty);
        DialogueSection ask = file.Block("persoper").Single(section => section.Name == "ask");
        Assert.That(ask.Argument, Is.EqualTo("fav_music"));
        Assert.That(ask.Answers.Select(answer => answer.Key), Is.EqualTo(new[] { "metal", "ballads", "other" }));
        Assert.That(PersoperIdentity.Asks.Select(found => found.Argument), Does.Contain("fav_music"));
        Assert.That(file.Rules.Values.SelectMany(rule => rule.Conditions).Where(c => c.Fact == "player_likes").Select(c => c.Value),
            Is.SupersetOf(new[] { "metal", "ballads" }));
    }
}
