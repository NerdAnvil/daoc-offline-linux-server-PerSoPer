using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Persoper answers her own owner in faction chat first, on top of the usual answers (CompanionFirst).</summary>
[TestFixture]
public class UT_PersoperChatTurns
{
    [Test]
    public void CompanionFirst_PutsHerFirst_AndAddsOneAnswer()
    {
        (string[] responders, int count) = AutonomousBotChatCoordinator.CompanionFirst(["Spyburd", "Persoper", "Eaglefang", "Muckaduck"], "Persoper", 2);

        Assert.That(responders, Is.EqualTo(new[] { "Persoper", "Spyburd", "Eaglefang" }));
        Assert.That(count, Is.EqualTo(3));
    }

    [TestCase(0, new[] { "Persoper" }, 1)]
    [TestCase(5, new[] { "Persoper", "Spyburd" }, 2)]
    public void CompanionFirst_KeepsTheCountInRange(int asked, string[] expected, int count)
    {
        (string[] responders, int kept) = AutonomousBotChatCoordinator.CompanionFirst(["Persoper", "Spyburd"], "Persoper", asked);

        Assert.That(responders, Is.EqualTo(expected));
        Assert.That(kept, Is.EqualTo(count));
    }
}
