using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.PlayerClass;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_NamedBotLineup
{
    private static readonly string[] TypeKeys =
        ["hardcore", "casual", "leeroy", "loner", "minmaxer", "joker", "crafter", "roleplayer", "homeowner", "herbaldoctor", "gruffdoc"];

    private static readonly (eRealm Realm, string Suffix)[] Realms =
        [(eRealm.Midgard, "mid"), (eRealm.Albion, "alb"), (eRealm.Hibernia, "hib")];

    [Test]
    public void Lineup_HasOneBotPerTypePerRealm()
    {
        Assert.That(NamedBotLineup.All, Has.Count.EqualTo(33));
        foreach (string type in TypeKeys)
        foreach (var (realm, suffix) in Realms)
        {
            var entry = NamedBotLineup.All.SingleOrDefault(e => e.Key == $"{type}-{suffix}");
            Assert.That(entry, Is.Not.Null, $"{type}-{suffix}");
            Assert.That(entry.TypeKey, Is.EqualTo(type));
            Assert.That(entry.Realm, Is.EqualTo(realm), entry.Key);
        }
    }

    [Test]
    public void Lineup_NamesAreUniqueAndNotOtherPersonalities()
    {
        var names = NamedBotLineup.All.Select(e => e.Name).ToArray();
        Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(33));
        Assert.That(names, Has.None.Matches<string>(PersoperIdentity.IsReservedName));
        Assert.That(names, Has.None.Matches<string>(KobaldjoviIdentity.IsReservedName));
    }

    [Test]
    public void Lineup_SixTypesMatchGeneratedBotTypes()
    {
        foreach (string type in TypeKeys.Take(6))
            Assert.That(PersonalityTypes.Templates.ContainsKey(type), Is.True, type);
    }

    [Test]
    public void Lineup_EveryRaceClassPairIsValid()
    {
        Assert.Multiple(() =>
        {
            foreach (var entry in NamedBotLineup.All)
            {
                Assert.That(AutonomousBotIdentityGenerator.GetEraClasses(entry.Realm), Does.Contain(entry.CharacterClass), entry.Key);
                Assert.That(AutonomousBotIdentityGenerator.GetEligibleRaces(entry.CharacterClass), Does.Contain(entry.Race), entry.Key);

                // The same rule character creation uses: the class's own list of races.
                Type classType = typeof(ClassWarrior).Assembly.GetType($"DOL.GS.PlayerClass.Class{entry.CharacterClass}");
                Assert.That(classType, Is.Not.Null, entry.Key);
                var characterClass = (ICharacterClass) Activator.CreateInstance(classType);
                Assert.That(characterClass.EligibleRaces.Select(race => race.ID), Does.Contain(entry.Race), entry.Key);

                // Fixed genders must be allowed for the race and class; "Random" must allow both.
                IEnumerable<eGender> genders = entry.Gender is { } fixedGender ? [fixedGender] : [eGender.Male, eGender.Female];
                foreach (eGender gender in genders)
                {
                    if (GlobalConstants.RACE_GENDER_CONSTRAINTS_DICT.TryGetValue(entry.Race, out eGender raceGender))
                        Assert.That(gender, Is.EqualTo(raceGender), entry.Key);
                    if (GlobalConstants.CLASS_GENDER_CONSTRAINTS_DICT.TryGetValue(entry.CharacterClass, out eGender classGender))
                        Assert.That(gender, Is.EqualTo(classGender), entry.Key);
                }
            }
        });
    }

    [TestCase("Shempenstein")]
    [TestCase("shnacksize")]
    [TestCase("XXXNATASEHSXXX")]
    [TestCase("Eaglefangh")]
    public void ReservedName_BlocksEveryNamedBotInAnyCase(string name)
    {
        Assert.That(NamedBotLineup.IsReservedName(name), Is.True);
        Assert.That(new InvalidNamesManager(null)[name], Is.True, "character creation and renames use this check");
    }

    [TestCase("Shempen")]
    [TestCase("Shnacksizes")]
    [TestCase("Hunter")]
    [TestCase("")]
    [TestCase(null)]
    public void ReservedName_AllowsOtherNames(string name)
    {
        Assert.That(NamedBotLineup.IsReservedName(name), Is.False);
    }

    [Test]
    public void GeneratedBots_NeverGetANamedBotName()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var random = new Random(33);
        foreach (var (realm, _) in Realms)
        for (int index = 0; index < 500; index++)
            AutonomousBotIdentityGenerator.Generate(realm, index % 2 == 0 ? eGender.Male : eGender.Female, names, random);

        Assert.That(names, Has.None.Matches<string>(NamedBotLineup.IsReservedName));
    }
}
