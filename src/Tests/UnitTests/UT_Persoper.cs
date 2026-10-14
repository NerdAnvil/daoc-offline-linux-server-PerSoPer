using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_Persoper
{
    private static readonly eRealm[] Realms = [eRealm.Albion, eRealm.Midgard, eRealm.Hibernia];

    [TestCase("Persoper")]
    [TestCase("persoper")]
    [TestCase("PERSOPER")]
    [TestCase("Xpersoperx")]
    [TestCase("Persoper Healsalot")]
    public void ReservedName_BlocksAnyNameContainingPersoper(string name)
    {
        Assert.That(PersoperIdentity.IsReservedName(name), Is.True);
    }

    [TestCase("Persephone")]
    [TestCase("Soper")]
    [TestCase("")]
    [TestCase(null)]
    public void ReservedName_AllowsOtherNames(string name)
    {
        Assert.That(PersoperIdentity.IsReservedName(name), Is.False);
    }

    [TestCase(eRealm.Albion, eCharacterClass.Cleric)]
    [TestCase(eRealm.Midgard, eCharacterClass.Healer)]
    [TestCase(eRealm.Hibernia, eCharacterClass.Druid)]
    public void HealerClass_IsTheRealmsHealerAndAvailableThere(eRealm realm, eCharacterClass expected)
    {
        Assert.That(PersoperIdentity.HealerClassFor(realm), Is.EqualTo(expected));
        Assert.That(AutonomousBotIdentityGenerator.GetEraClasses(realm), Does.Contain(expected));
        Assert.That(AutonomousBotIdentityGenerator.GetEligibleRaces(expected), Is.Not.Empty);
    }

    [Test]
    public void EverySurname_IsOneShortWordAndKeepsTheFullNameShort()
    {
        foreach (eRealm realm in Realms)
        {
            eCharacterClass characterClass = PersoperIdentity.HealerClassFor(realm);
            foreach (eRace race in AutonomousBotIdentityGenerator.GetEligibleRaces(characterClass))
            foreach (bool toneDown in new[] { false, true })
            foreach (string surname in PersoperIdentity.SurnamePool(characterClass, race, toneDown))
            {
                Assert.That(PersoperIdentity.IsValidSurname(surname), Is.True, surname);
                Assert.That(PersoperIdentity.FullName(surname).Length, Is.LessThanOrEqualTo(23), surname);
                Assert.That(PersoperIdentity.IsReservedName(surname), Is.False, surname);
            }
        }
    }

    [Test]
    public void ToneDown_OnlyOffersCleanSurnames()
    {
        foreach (eRealm realm in Realms)
        {
            eCharacterClass characterClass = PersoperIdentity.HealerClassFor(realm);
            foreach (eRace race in AutonomousBotIdentityGenerator.GetEligibleRaces(characterClass))
            {
                var clean = PersoperIdentity.SurnamePool(characterClass, race, toneDown: true);
                var full = PersoperIdentity.SurnamePool(characterClass, race, toneDown: false);
                Assert.That(clean.All(PersoperIdentity.IsCleanSurname), Is.True);
                Assert.That(full.Count, Is.GreaterThan(clean.Count));
            }
        }

        Assert.That(PersoperIdentity.IsCleanSurname("Rezurrection"), Is.False);
        Assert.That(PersoperIdentity.IsCleanSurname("Healsalot"), Is.True);
    }

    [Test]
    public void ChooseSurname_NeverRepeatsTheCurrentOne()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            string chosen = PersoperIdentity.ChooseSurname(eCharacterClass.Healer, eRace.Dwarf, false, "Healsalot", new Random(seed));
            Assert.That(chosen, Is.Not.EqualTo("Healsalot"));
        }
    }

    [Test]
    public void SurnameChange_RespectsAndClampsThePercent()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            Assert.That(PersoperIdentity.ShouldChangeSurname(0, new Random(seed)), Is.False);
            Assert.That(PersoperIdentity.ShouldChangeSurname(-5, new Random(seed)), Is.False);
            Assert.That(PersoperIdentity.ShouldChangeSurname(100, new Random(seed)), Is.True);
            Assert.That(PersoperIdentity.ShouldChangeSurname(500, new Random(seed)), Is.True);
        }

        Random random = new(42);
        int changes = Enumerable.Range(0, 10_000).Count(_ => PersoperIdentity.ShouldChangeSurname(5, random));
        Assert.That(changes, Is.InRange(350, 650));
    }

    [Test]
    public void Greetings_SayHerSurnameAndFillEveryPlaceholder()
    {
        foreach (PersoperIdentity.eGreeting kind in Enum.GetValues<PersoperIdentity.eGreeting>())
        foreach (bool toneDown in new[] { false, true })
        for (int seed = 0; seed < 20; seed++)
        {
            string line = PersoperIdentity.Greeting(kind, "Healsalot", "Tester", toneDown, new Random(seed));
            Assert.That(line, Does.Contain("Healsalot"), line);
            Assert.That(line, Does.Not.Contain("{"), line);
        }
    }

    [Test]
    public void ToneDown_NeverUsesCheekyGreetings()
    {
        string[] cheekyWords = ["hands-on", "darling", "All of it", "curves", "slipping", "healing hands"];
        foreach (PersoperIdentity.eGreeting kind in Enum.GetValues<PersoperIdentity.eGreeting>())
        for (int seed = 0; seed < 200; seed++)
        {
            string line = PersoperIdentity.Greeting(kind, "Healsalot", "Tester", toneDown: true, new Random(seed));
            foreach (string word in cheekyWords)
                Assert.That(line, Does.Not.Contain(word), line);
        }
    }

    [Test]
    public void OffClassGrumble_OnlyWhenNotAHealer()
    {
        Assert.That(PersoperIdentity.OffClassGrumble(eCharacterClass.Healer, new Random(1)), Is.Null);
        Assert.That(PersoperIdentity.OffClassGrumble(eCharacterClass.Cleric, new Random(1)), Does.Contain("Cleric"));
        Assert.That(PersoperIdentity.OffClassGrumble(eCharacterClass.Druid, new Random(1)), Does.Contain("Druid"));
    }

    [Test]
    public void GeneratedBotNames_NeverUsePersoper()
    {
        foreach (eRealm realm in Realms)
        foreach (eGender gender in new[] { eGender.Male, eGender.Female })
        for (int seed = 0; seed < 50; seed++)
        {
            var identity = AutonomousBotIdentityGenerator.Generate(realm, gender, random: new Random(seed));
            Assert.That(PersoperIdentity.IsReservedName(identity.Name), Is.False, identity.Name);
        }
    }
}
