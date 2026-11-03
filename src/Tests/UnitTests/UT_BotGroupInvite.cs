using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_BotGroupInvite
{
    [Test]
    public void OnlyALiveBotInAnotherRegionIsMovedBesideThePlayer()
    {
        Assert.That(BotGroupInvite.ShouldMoveBesidePlayer(129, 100, true), Is.True, "dungeon to Midgard");
        Assert.That(BotGroupInvite.ShouldMoveBesidePlayer(100, 100, true), Is.False, "same region walks");
        Assert.That(BotGroupInvite.ShouldMoveBesidePlayer(129, 100, false), Is.False, "dead bot stays");
    }
}
