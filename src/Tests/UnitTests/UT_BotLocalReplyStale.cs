using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_BotLocalReplyStale
{
    private static readonly eAutonomousChatIntent[] AllIntents = Enum.GetValues<eAutonomousChatIntent>();

    // The saved task still describes a trip the bot has already finished.
    private static OfflineWorldBotRecord OldTrip() => new()
    {
        Activity = "Walking to Cotswold Village",
        TargetName = "gnoll scout",
        TravelDestination = "Cotswold Village",
    };

    private static string Reply(eAutonomousChatIntent intent, bool moving, bool fighting, int seed) =>
        AutonomousBotChatCoordinator.BuildLocalReply(intent, OldTrip(), "Camelot Hills", false, moving, fighting, new Random(seed));

    [Test]
    public void StandingStillBot_NeverQuotesTheOldTrip()
    {
        foreach (eAutonomousChatIntent intent in AllIntents)
            for (int seed = 0; seed < 30; seed++)
            {
                string reply = Reply(intent, moving: false, fighting: false, seed);
                Assert.That(reply, Does.Not.Contain("Cotswold").And.Not.Contain("Walking").And.Not.Contain("walking").And.Not.Contain("gnoll"),
                    $"{intent}, seed {seed}: {reply}");
            }
    }

    [Test]
    public void StandingStillBot_AnswersAWhereAreYouGoingQuestionWithWhereItIs()
    {
        string reply = Reply(eAutonomousChatIntent.Travel, moving: false, fighting: false, 1);
        Assert.That(reply, Does.Contain("Camelot Hills").And.Not.Contain("headed toward"));
    }

    [Test]
    public void MovingBot_StillSaysWhereItIsHeaded()
    {
        string reply = Reply(eAutonomousChatIntent.Travel, moving: true, fighting: false, 1);
        Assert.That(reply, Is.EqualTo("I am headed toward Cotswold Village."));
    }

    [Test]
    public void FightingBot_StillNamesItsTarget()
    {
        string reply = Reply(eAutonomousChatIntent.Grinding, moving: false, fighting: true, 1);
        Assert.That(reply, Does.Contain("gnoll scout"));
    }

    [Test]
    public void NoSavedRecord_StillGivesAnAnswerForEveryIntent()
    {
        foreach (eAutonomousChatIntent intent in AllIntents)
            Assert.That(AutonomousBotChatCoordinator.BuildLocalReply(intent, null, "this area", false, false, false, new Random(1)), Is.Not.Empty);
    }
}
