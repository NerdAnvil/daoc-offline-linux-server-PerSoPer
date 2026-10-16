using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityTypes
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<BotPersonality> Types => PersonalityTypes.Templates.Values;

    [Test]
    public void AllSixTypes_Exist_WithTheFullKit()
    {
        Assert.That(PersonalityTypes.Keys, Is.EquivalentTo(new[] { "hardcore", "casual", "minmaxer", "joker", "loner", "leeroy" }));
        foreach (BotPersonality type in Types)
        {
            Assert.That(type.Ambient, Is.Not.Empty, type.Key);
            Assert.That(type.Chattiness, Is.InRange(1, 100), type.Key);
            Assert.That(type.Memory, Is.Not.Null, type.Key);
            Assert.That(type.Build, Is.Not.Null, type.Key);
            Assert.That(type.Patience, Is.Not.Null, type.Key);
            Assert.That(type.Patience.MakeUps, Is.Not.Empty, type.Key);
            Assert.That(type.Matches(null), Is.False, "templates never match a bot themselves");
        }
        Assert.That(PersonalityTypes.Templates["loner"].Chattiness, Is.LessThan(PersonalityTypes.Templates["joker"].Chattiness));
    }

    [Test]
    public void EveryLine_FillsItsPlaceholders()
    {
        var random = new Random(11);
        foreach (BotPersonality type in Types)
        {
            foreach (eAutonomousChatIntent intent in Enum.GetValues<eAutonomousChatIntent>())
            foreach (bool toneDown in new[] { true, false })
            for (int i = 0; i < 6; i++)
                Assert.That(type.Reply(intent, "Tester", toneDown, random), Is.Not.Empty.And.Not.Contain("{"), $"{type.Key} {intent}");

            foreach (PersonalityAnnoyance.eIrritation irritation in Enum.GetValues<PersonalityAnnoyance.eIrritation>())
            foreach (PersonalityAnnoyance.eReaction reaction in Enum.GetValues<PersonalityAnnoyance.eReaction>())
                Assert.That(PersonalityAnnoyance.Line(type.Patience, irritation, reaction, "Tester", random), Is.Not.Contain("{"), type.Key);
            Assert.That(PersonalityAnnoyance.SulkRefusal(type.Patience, "Tester", TimeSpan.FromMinutes(3), random), Does.Contain("3 minutes"));
            foreach (string line in type.Patience.MakeUps.Concat(type.Patience.MakeUpInvites))
                Assert.That(string.Format(line, "Tester"), Does.Not.Contain("{"), type.Key);

            var memory = new PersonalityMemoryRecord
            {
                FirstMetUtc = Now.AddDays(-20).ToString("O"), LastSeenUtc = Now.AddDays(-3).ToString("O"),
                MeetCount = 2, RudeCount = 5, LastInsult = "noob", LastZone = "Mularn", LastSpec = (int)eSpecType.TwoHanded,
            };
            for (int i = 0; i < 10; i++)
                Assert.That(PersonalityMemory.RememberedLine(type, memory, "Tester", Now, random), Is.Not.Contain("{"), type.Key);
            Assert.That(PersonalityMemory.Recall(type, memory, "Tester", Now), Does.Contain("Mularn").And.Contain("two handed"), type.Key);
            Assert.That(PersonalityMemory.SpecAnnouncement(type, eSpecType.TwoHanded, eSpecType.OneHandAndShield, "Tester", random),
                Is.Not.Empty.And.Not.Contain("{"), type.Key);
        }
    }

    [TestCase("joker", "joker")]
    [TestCase("Min/Maxer", "minmaxer")]
    [TestCase(" LEEROY ", "leeroy")]
    [TestCase("mixed", "mixed")]
    [TestCase("bard", null)]
    [TestCase("", null)]
    public void Normalise_AcceptsFriendlyNames(string typed, string expected)
    {
        Assert.That(PersonalityTypes.Normalise(typed), Is.EqualTo(expected));
    }

    [Test]
    public void Mixed_PicksEveryTypeSometimes()
    {
        var random = new Random(5);
        var picked = Enumerable.Range(0, 300).Select(_ => PersonalityTypes.Pick(PersonalityTypes.Mixed, random)).ToHashSet();
        Assert.That(picked, Is.EquivalentTo(PersonalityTypes.Keys));
        Assert.That(PersonalityTypes.Pick("loner", random), Is.EqualTo("loner"));
        Assert.That(PersonalityTypes.InstanceKey("joker", 123), Is.EqualTo("joker#123"));
    }

    [Test]
    public void ChatterGaps_FollowTheVolume()
    {
        Assert.That(PersonalityChatter.FactionGap(0), Is.Null);
        Assert.That(PersonalityChatter.FactionGap(50), Is.EqualTo((30.0, 60.0)));
        Assert.That(PersonalityChatter.LocalGap(50), Is.EqualTo((15.0, 30.0)));
        Assert.That(PersonalityChatter.FactionGap(100), Is.EqualTo((15.0, 30.0)));
        Assert.That(PersonalityChatter.LocalGap(100), Is.EqualTo((10.0, 15.0)), "never under 10 s");
        Assert.That(PersonalityChatter.FactionGap(25), Is.EqualTo((60.0, 120.0)));
        Assert.That(PersonalityChatter.GroupGap(50), Is.EqualTo((45.0, 90.0)));
        Assert.That(PersonalityChatter.GroupGap(100), Is.EqualTo((22.5, 45.0)));
        Assert.That(PersonalityChatter.GroupGap(0), Is.Null);
    }

    [Test]
    public void PickWeighted_FavoursChattierBots()
    {
        var random = new Random(9);
        var candidates = new List<(string Item, int Weight)> { ("joker", 80), ("loner", 12) };
        int jokers = Enumerable.Range(0, 1000).Count(_ => PersonalityChatter.PickWeighted(candidates, random) == "joker");
        Assert.That(jokers, Is.InRange(800, 920));
        Assert.That(PersonalityChatter.PickWeighted(new List<(string, int)>(), random), Is.Null);
    }

    [Test]
    public void SpecWords_ReadsSpecNames()
    {
        Assert.That(PersonalityTypeVoices.SpecWords(eSpecType.TwoHanded), Is.EqualTo("two handed"));
        Assert.That(PersonalityTypeVoices.SpecWords(eSpecType.MendHealer), Is.EqualTo("mend healer"));
        Assert.That(PersonalityTypeVoices.SpecWords(eSpecType.None), Is.Null);
    }

    [Test]
    public void Roll_GivesAboutHalfARandomType_AndTheRestPlain()
    {
        var random = new Random(7);
        var rolls = Enumerable.Range(0, 2000).Select(_ => PersonalityTypes.Roll(random)).ToList();
        int typed = rolls.Count(key => key != PersonalityTypes.Plain);
        Assert.That(typed, Is.InRange(900, 1100), "about 50/50");
        Assert.That(rolls.Where(key => key != PersonalityTypes.Plain).Distinct(), Is.EquivalentTo(PersonalityTypes.Keys), "every type turns up");
        Assert.That(PersonalityTypes.Templates.ContainsKey(PersonalityTypes.Plain), Is.False, "plain is never a type");
        Assert.That(PersonalityTypes.Normalise(PersonalityTypes.Plain), Is.Null, "admins can't ask for plain as a type");
    }
}
