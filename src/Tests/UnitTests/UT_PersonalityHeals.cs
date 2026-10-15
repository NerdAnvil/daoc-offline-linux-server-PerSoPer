using System;
using System.Collections.Generic;
using NUnit.Framework;
using static DOL.GS.PersonalityHeals;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityHeals
{
    [TestCase("small heal", eHealSize.Small, null)]
    [TestCase("Persoper, give me a small heal please", eHealSize.Small, null)]
    [TestCase("little heal?", eHealSize.Small, null)]
    [TestCase("top me off", eHealSize.Small, null)]
    [TestCase("top Bob off", eHealSize.Small, "bob")]
    [TestCase("medium heal please", eHealSize.Medium, null)]
    [TestCase("big heal on Bob", eHealSize.Big, "bob")]
    [TestCase("heal me", eHealSize.Auto, null)]
    [TestCase("Persoper, heal me", eHealSize.Auto, null)]
    [TestCase("heal the tank", eHealSize.Auto, "tank")]
    [TestCase("heal yourself", eHealSize.Auto, "yourself")]
    [TestCase("HELP!", eHealSize.Panic, null)]
    [TestCase("ACK!", eHealSize.Panic, null)]
    [TestCase("ON ME!", eHealSize.Panic, null)]
    [TestCase("help!!", eHealSize.Panic, null)]
    [TestCase("heal me now", eHealSize.Panic, null)]
    [TestCase("HEAL!!", eHealSize.Panic, null)]
    [TestCase("HEAL BOB", eHealSize.Panic, "bob")]
    [TestCase("AAAHHH!", eHealSize.Panic, null)]
    [TestCase("Persoper heal me", eHealSize.Auto, null)]
    [TestCase("please heal me", eHealSize.Auto, null)]
    [TestCase("heal, please", eHealSize.Auto, null)]
    [TestCase("can you heal me?", eHealSize.Auto, null)]
    [TestCase("I need a heal", eHealSize.Auto, null)]
    [TestCase("hey Persoper, a big heal please", eHealSize.Big, null)]
    public void Parse_ReadsSizeAndTarget(string message, eHealSize size, string target)
    {
        HealRequest request = Parse(message);
        Assert.That(request, Is.Not.Null, message);
        Assert.That(request.Size, Is.EqualTo(size), message);
        Assert.That(request.TargetWord, Is.EqualTo(target), message);
    }

    [TestCase("can you help me find the bank")]
    [TestCase("I need a healer for this dungeon")]
    [TestCase("hello there")]
    [TestCase("Persoper, what's your spec?")]
    [TestCase("dont heal")]
    [TestCase("don't heal me")]
    [TestCase("Persoper, do not heal Bob")]
    [TestCase("please don't heal the tank")]
    [TestCase("DONT HEAL!!")]
    [TestCase("no heal needed")]
    [TestCase("stop trying to heal me")]
    [TestCase("don't top me off")]
    [TestCase("nice heal")]
    [TestCase("thanks for the heal")]
    [TestCase("that was a big heal")]
    [TestCase("your heal was late")]
    [TestCase("heal was great")]
    [TestCase("I can heal myself")]
    [TestCase("druids heal well")]
    [TestCase("")]
    [TestCase(null)]
    public void Parse_IgnoresOrdinaryChat(string message)
    {
        Assert.That(Parse(message), Is.Null);
    }

    [TestCase(eHealSize.Auto, 30, eHealSize.Big)]
    [TestCase(eHealSize.Auto, 70, eHealSize.Medium)]
    [TestCase(eHealSize.Auto, 100, eHealSize.Small)]
    [TestCase(eHealSize.Small, 10, eHealSize.Small)]
    [TestCase(eHealSize.Panic, 100, eHealSize.Panic)]
    public void Resolve_UsesHealthOnlyWhenNoSizeGiven(eHealSize asked, int health, eHealSize expected)
    {
        Assert.That(Resolve(asked, health), Is.EqualTo(expected));
    }

    private static readonly List<HealOption<string>> Heals =
    [
        new("cheap", 10, 50, false),
        new("biggest", 30, 200, false),
        new("efficient", 20, 150, false),
        new("instant", 25, 120, true),
    ];

    [TestCase(eHealSize.Small, "cheap")]
    [TestCase(eHealSize.Medium, "efficient")]
    [TestCase(eHealSize.Big, "biggest")]
    [TestCase(eHealSize.Panic, "instant")]
    public void Choose_PicksTheRightHealPerSize(eHealSize size, string expected)
    {
        Assert.That(Choose(Heals, size), Is.EqualTo(expected));
    }

    [Test]
    public void Choose_PanicWithoutAnInstantHeal_UsesTheBiggest()
    {
        var noInstant = Heals.FindAll(o => !o.Instant);
        Assert.That(Choose(noInstant, eHealSize.Panic), Is.EqualTo("biggest"));
        Assert.That(Choose(new List<HealOption<string>>(), eHealSize.Big), Is.Null);
    }

    [Test]
    public void FullHealthSpam_CountsThreeInAMinute()
    {
        var recent = new List<DateTime>();
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.That(CountFullHealthRequest(recent, now), Is.False);
        Assert.That(CountFullHealthRequest(recent, now.AddSeconds(10)), Is.False);
        Assert.That(CountFullHealthRequest(recent, now.AddSeconds(20)), Is.True);
        Assert.That(recent, Is.Empty);
        // Spread out over more than a minute, they don't add up.
        CountFullHealthRequest(recent, now.AddMinutes(5));
        CountFullHealthRequest(recent, now.AddMinutes(7));
        Assert.That(CountFullHealthRequest(recent, now.AddMinutes(9)), Is.False);
    }

    [Test]
    public void Lines_ExistForEveryOutcome_InBothVoices()
    {
        var random = new Random(3);
        foreach (HealVoice voice in new[] { Neutral, PersoperIdentity.Personality.Heals })
        foreach (eHealOutcome outcome in Enum.GetValues<eHealOutcome>())
        foreach (eHealSize size in Enum.GetValues<eHealSize>())
            Assert.That(Line(voice, outcome, size, "Tester", random), Is.Not.Empty.And.Not.Contain("{"));
        Assert.That(KobaldjoviIdentity.Personality.Heals, Is.Null, "Kobaldjovi is not a healer");
    }
}
