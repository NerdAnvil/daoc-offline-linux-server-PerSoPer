using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousDeathCauseLog
    {
        [Test]
        public void RingKeepsOnlyTheLastFiveHitsNewestFirst()
        {
            AutonomousDamageRing ring = new();
            for (int i = 1; i <= 8; i++)
                ring.Record(new AutonomousDamageHit("wolf", i, "Crush", 100 - i));

            var hits = ring.NewestFirst();
            Assert.That(hits, Has.Count.EqualTo(AutonomousDamageRing.Capacity));
            Assert.That(hits[0].Amount, Is.EqualTo(8));
            Assert.That(hits[4].Amount, Is.EqualTo(4));

            ring.Clear();
            Assert.That(ring.NewestFirst(), Is.Empty);
        }

        [Test]
        public void LineCarriesKillerHitsAndBotState()
        {
            AutonomousDeathCause cause = new()
            {
                BotName = "Hrolf", BotId = 123, Realm = "Midgard", ClassName = "Runemaster", Level = 19,
                RegionId = 100, Zone = "Vale of Mularn", X = 1, Y = 2, Z = 3,
                Activity = "Grinding \"rage wolf\"", Target = "rage wolf",
                Killer = "rage wolf", KillerLevel = 17, KillerCon = "GREEN", KillerKind = "mob",
                GroupSize = 0, PetOut = false, ArmorSlotsFilled = 2, MaxHealth = 400, Mana = 4, MaxMana = 300,
                Hits = [new("rage wolf", 42, "Crush", 30), new("rage wolf", 38, "Crush", 68)],
            };

            string line = AutonomousDeathCauseLog.Format(cause);

            Assert.That(line, Does.StartWith("AUTONOMOUS_BOT_DEATH_CAUSE bot=Hrolf id=123 realm=Midgard "));
            Assert.That(line, Does.Contain(" class=\"Runemaster\" level=19 region=100 zone=\"Vale of Mularn\" position=1,2,3"));
            Assert.That(line, Does.Contain(" activity=\"Grinding 'rage wolf'\""), "quotes inside values must not break the line");
            Assert.That(line, Does.Contain(" killer=\"rage wolf\" killer_level=17 killer_con=GREEN killer_kind=mob group=0"));
            Assert.That(line, Does.Not.Contain("killer_owner="));
            Assert.That(line, Does.Contain(" pet=0 armor=2/6 max_hp=400 mana=4/300 last_hit_hp_before=30"));
            Assert.That(line, Does.EndWith(" hits=\"rage wolf:42:Crush:30;rage wolf:38:Crush:68\""));
        }

        [Test]
        public void PetKillNamesTheOwnerAndEmptyDeathStillFormats()
        {
            string petLine = AutonomousDeathCauseLog.Format(new AutonomousDeathCause
            {
                BotName = "A", Killer = "Bob", KillerKind = "pet", KillerOwner = "Some;One",
            });
            Assert.That(petLine, Does.Contain(" killer_kind=pet killer_owner=\"Some,One\""));

            string empty = AutonomousDeathCauseLog.Format(new AutonomousDeathCause());
            Assert.That(empty, Does.Contain(" killer_con=NONE killer_kind=none"));
            Assert.That(empty, Does.EndWith(" last_hit_hp_before=0 hits=\"\""));
        }
    }
}
