using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_GroupBotsLeftBehind
    {
        [Test]
        public void LastPlayerLeavingBotsWhoFollowedThemDisbandsTheGroup() =>
            Assert.That(Group.ShouldDisbandBotsLeftBehind(leaverIsPlayer: true, playerRemains: false, botFollowsPlayer: true), Is.True);

        [Test]
        public void AnotherPlayerStillInTheGroupKeepsIt() =>
            Assert.That(Group.ShouldDisbandBotsLeftBehind(leaverIsPlayer: true, playerRemains: true, botFollowsPlayer: true), Is.False);

        [Test]
        public void LeaderRemovingOneBotKeepsTheGroup() =>
            Assert.That(Group.ShouldDisbandBotsLeftBehind(leaverIsPlayer: false, playerRemains: true, botFollowsPlayer: true), Is.False);

        [Test]
        public void ABotLeavingKeepsTheGroup() =>
            Assert.That(Group.ShouldDisbandBotsLeftBehind(leaverIsPlayer: false, playerRemains: false, botFollowsPlayer: true), Is.False);

        [Test]
        public void AutonomousBotGroupWithNoPlayerLeadIsKept() =>
            Assert.That(Group.ShouldDisbandBotsLeftBehind(leaverIsPlayer: true, playerRemains: false, botFollowsPlayer: false), Is.False);
    }
}
