using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Bug #7 and intent B1: a bot named in a line always answers it, first, even while riding, casting or fighting;
/// now and then one other bot chips in after it, never instead of it.
/// </summary>
[TestFixture]
public class UT_NamedBotAnswers
{
    [Test]
    public void FreeBot_SaysItsReply()
    {
        Assert.That(AutonomousBotChatCoordinator.NextReplyStep(true, false, false, 0), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Say));
        Assert.That(AutonomousBotChatCoordinator.NextReplyStep(true, false, true, 3), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Say));
    }

    [Test]
    public void BusyNamedBot_WaitsAFewTries_ThenAnswersAnyway()
    {
        int retries = AutonomousBotChatCoordinator.NamedReplyRetries;
        Assert.That(retries, Is.InRange(1, 5), "a short, capped wait");
        for (int left = retries; left > 0; left--)
            Assert.That(AutonomousBotChatCoordinator.NextReplyStep(true, true, true, left), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Retry));
        Assert.That(AutonomousBotChatCoordinator.NextReplyStep(true, true, true, 0), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Say),
            "a long fight doesn't swallow the answer");
    }

    [Test]
    public void BusyUnnamedBot_StillDropsItsReply()
    {
        Assert.That(AutonomousBotChatCoordinator.NextReplyStep(true, true, false, 3), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Drop));
    }

    [Test]
    public void GoneBot_NeverSpeaks()
    {
        Assert.That(AutonomousBotChatCoordinator.NextReplyStep(false, false, true, 3), Is.EqualTo(AutonomousBotChatCoordinator.eReplyStep.Drop));
    }

    [Test]
    public void ChipIn_NeedsSomeoneElse()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            Assert.That(AutonomousBotChatCoordinator.ChipIn(Array.Empty<string>(), new Random(seed)), Is.Null);
            Assert.That(AutonomousBotChatCoordinator.ChipIn<string>(null, new Random(seed)), Is.Null);
        }
    }

    [Test]
    public void ChipIn_IsRare_AndOnlyFromTheOthers()
    {
        string[] others = ["Shnacksize", "Kobaldjovi", "Persoper"];
        var random = new Random(7);
        string[] picks = Enumerable.Range(0, 4_000).Select(_ => AutonomousBotChatCoordinator.ChipIn(others, random)).ToArray();
        double rate = picks.Count(pick => pick != null) / (double)picks.Length;

        Assert.That(AutonomousBotChatCoordinator.ChipInChance, Is.InRange(0.05, 0.3), "now and then, not most of the time");
        Assert.That(rate, Is.EqualTo(AutonomousBotChatCoordinator.ChipInChance).Within(0.03));
        Assert.That(picks.Where(pick => pick != null), Is.All.AnyOf(others));
    }

    [Test]
    public void NamedBotOutOfEarshot_OthersStayQuiet()
    {
        Assert.That(AutonomousBotChatCoordinator.OthersStayQuiet(Array.Empty<string>(), ["Shempenstein"]), Is.True);
    }

    [Test]
    public void NamedBotHears_OthersNeedNotStayQuiet()
    {
        Assert.That(AutonomousBotChatCoordinator.OthersStayQuiet(["Shempenstein"], ["Shempenstein"]), Is.False);
        Assert.That(AutonomousBotChatCoordinator.OthersStayQuiet(["Shempenstein"], ["Shempenstein", "Kobaldjovi"]), Is.False,
            "one of two named bots in earshot answers");
    }

    [Test]
    public void NobodyNamed_OthersMayAnswer()
    {
        Assert.That(AutonomousBotChatCoordinator.OthersStayQuiet(Array.Empty<string>(), Array.Empty<string>()), Is.False);
        Assert.That(AutonomousBotChatCoordinator.OthersStayQuiet<string>(null, null), Is.False);
    }

    // The silent runs weren't for want of a line: Shempenstein has an answer every time, run after run.
    [Test]
    public void Shempenstein_AlwaysHasAnAnswer_ToSwordsTallerThanHim()
    {
        DialogueLibrary.Initialize(null);
        BotPersonality shemp = DialoguePreview.Find("Shempenstein");
        Assert.That(shemp, Is.Not.Null);
        eAutonomousChatIntent intent = AutonomousChatIntentModel.Predict("shemp i have swords taller than you");
        var random = new Random(3);
        for (int run = 0; run < 40; run++)
            Assert.That(string.IsNullOrWhiteSpace(shemp.Reply(intent, "Headlesstest", true, random)), Is.False, $"run {run}");
    }
}
