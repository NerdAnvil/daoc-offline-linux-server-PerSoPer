using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Guild chat: a member with a personality talks in its own voice, a plain bot with stock lines.</summary>
    [TestFixture]
    public class UT_BotGuildChat
    {
        [Test]
        public void AMemberWithItsOwnLineSaysIt()
        {
            Assert.That(AutonomousBotGuilds.OwnOrStock("  Ope, guild!  ", () => "stock"), Is.EqualTo("Ope, guild!"));
        }

        [Test]
        public void APlainBotOrOneWithNoLineGetsTheStockLine()
        {
            Assert.That(AutonomousBotGuilds.OwnOrStock(null, () => "stock"), Is.EqualTo("stock"), "plain bot: no own line");
            Assert.That(AutonomousBotGuilds.OwnOrStock(" ", () => "stock"), Is.EqualTo("stock"), "a blank own line is not said");
        }

        [Test]
        public void TheStockLineIsOnlyPickedWhenNeeded()
        {
            Assert.That(AutonomousBotGuilds.OwnOrStock("mine", () => throw new InvalidOperationException()), Is.EqualTo("mine"));
        }

        [Test]
        public void ChattyPersonalitiesStartGuildChatMoreOftenThanPlainBots()
        {
            Assert.That(AutonomousBotGuilds.SpeakerWeight(80), Is.GreaterThan(AutonomousBotGuilds.SpeakerWeight(null)), "Joker over a plain bot");
            Assert.That(AutonomousBotGuilds.SpeakerWeight(12), Is.LessThan(AutonomousBotGuilds.SpeakerWeight(null)), "Loner under a plain bot");
        }
    }
}
