using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_Kobaldjovi
{
    [TestCase("Kobaldjovi")]
    [TestCase("kobaldjovi")]
    [TestCase("XKobaldjoviX")]
    public void ReservedName_BlocksAnyNameContainingKobaldjovi(string name)
    {
        Assert.That(KobaldjoviIdentity.IsReservedName(name), Is.True);
    }

    [TestCase("Kobold")]
    [TestCase("Jovi")]
    [TestCase("")]
    [TestCase(null)]
    public void ReservedName_AllowsOtherNames(string name)
    {
        Assert.That(KobaldjoviIdentity.IsReservedName(name), Is.False);
    }

    [Test]
    public void Identity_IsAMidgardKoboldSkaldTheGameAllows()
    {
        AutonomousBotIdentityGenerator.Identity identity = KobaldjoviIdentity.CreateIdentity();
        Assert.That(identity.Name, Is.EqualTo("Kobaldjovi"));
        Assert.That(identity.Realm, Is.EqualTo(eRealm.Midgard));
        Assert.That(identity.Gender, Is.EqualTo(eGender.Male));
        Assert.That(AutonomousBotIdentityGenerator.GetEraClasses(eRealm.Midgard), Does.Contain(identity.CharacterClass));
        Assert.That(AutonomousBotIdentityGenerator.GetEligibleRaces(identity.CharacterClass), Does.Contain(identity.Race));
    }

    [Test]
    public void AmbientLine_IsMostlySongs()
    {
        var random = new Random(42);
        int songs = 0;
        for (int i = 0; i < 1000; i++)
        {
            string line = KobaldjoviIdentity.AmbientLine(random, out bool isSong);
            Assert.That(line, Is.Not.Empty);
            Assert.That(isSong, Is.EqualTo(line.StartsWith('~')));
            if (isSong)
                songs++;
        }

        Assert.That(songs, Is.InRange(700, 900));
    }

    [Test]
    public void GeneratedBots_NeverGetHisName()
    {
        var reserved = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 200; i++)
        {
            string name = AutonomousBotIdentityGenerator.Generate(eRealm.Midgard, eGender.Male, reserved).Name;
            Assert.That(KobaldjoviIdentity.IsReservedName(name), Is.False);
        }
    }
}
