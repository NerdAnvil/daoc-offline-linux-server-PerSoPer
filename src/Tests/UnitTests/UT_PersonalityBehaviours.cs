using System;
using System.Linq;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityBehaviours
{
    [Test]
    public void Rolls_RespectsTheEnds()
    {
        var random = new Random(3);
        Assert.That(Enumerable.Range(0, 200).Any(_ => PersonalityBehaviours.Rolls(0, random)), Is.False);
        Assert.That(Enumerable.Range(0, 200).All(_ => PersonalityBehaviours.Rolls(100, random)), Is.True);
        int hits = Enumerable.Range(0, 3000).Count(_ => PersonalityBehaviours.Rolls(33, random));
        Assert.That(hits, Is.InRange(850, 1150), "about 1 in 3");
    }

    [Test]
    public void Loner_DeclinesLessForPeopleItKnowsOrLikes()
    {
        double stranger = PersonalityBehaviours.LonerDeclineChance(0, PersonalityMemory.eMood.Neutral);
        double known = PersonalityBehaviours.LonerDeclineChance(3, PersonalityMemory.eMood.Neutral);
        double liked = PersonalityBehaviours.LonerDeclineChance(1, PersonalityMemory.eMood.Warm);
        Assert.That(stranger, Is.GreaterThan(0.5), "usually declines");
        Assert.That(known, Is.LessThan(stranger));
        Assert.That(liked, Is.LessThan(known));
    }

    [Test]
    public void EmoteReplies_FitTheEmote_AndTheType()
    {
        var random = new Random(5);
        Assert.That(Enumerable.Range(0, 50).All(_ => PersonalityBehaviours.EmoteReply(eEmote.Wave, null, random) == eEmote.Wave),
            "a plain bot waves back");
        var jokerReplies = Enumerable.Range(0, 300).Select(_ => PersonalityBehaviours.EmoteReply(eEmote.Wave, "joker", random)).ToHashSet();
        Assert.That(jokerReplies, Does.Contain(eEmote.Rofl).Or.Contain(eEmote.Dance), "a Joker sometimes does its own thing");
        Assert.That(PersonalityBehaviours.EmoteReplyPercent("joker", false), Is.GreaterThan(PersonalityBehaviours.EmoteReplyPercent("loner", false)));
        Assert.That(PersonalityBehaviours.EmoteReply(eEmote.Knock, null, random), Is.Not.EqualTo(eEmote.Knock), "unknown emotes get a generic answer");
    }

    [Test]
    public void DeathEmotes_GrieveFriends_AndMockEnemies()
    {
        var random = new Random(9);
        Assert.That(Enumerable.Range(0, 30).All(_ => PersonalityBehaviours.DeathEmote("casual", false, random) == eEmote.Cry));
        var enemy = Enumerable.Range(0, 100).Select(_ => PersonalityBehaviours.DeathEmote(null, true, random)).ToHashSet();
        Assert.That(enemy, Does.Not.Contain(eEmote.Cry), "nobody cries over an enemy");
    }

    [Test]
    public void EmoteText_ReadsNaturally()
    {
        Assert.That(PersonalityBehaviours.EmoteText("Bob", eEmote.Wave, "you"), Is.EqualTo("Bob waves at you."));
        Assert.That(PersonalityBehaviours.EmoteText("Bob", eEmote.Hug, "Ann"), Is.EqualTo("Bob hugs Ann."));
        Assert.That(PersonalityBehaviours.EmoteText("Bob", eEmote.Dance, null), Is.EqualTo("Bob dances."));
    }

    [Test]
    public void MinMaxer_NamesARealBuild()
    {
        string spec = PersonalityBehaviours.SpecSummary(eCharacterClass.Spiritmaster);
        Assert.That(spec, Is.Not.Null.And.Contains("/"));
        Assert.That(spec, Does.Contain("Darkness").Or.Contain("Suppression").Or.Contain("Summoning"));
        string line = PersonalityBehaviours.InspectLine("Ann", "Spiritmaster", spec, new Random(1));
        Assert.That(line, Does.Contain(spec));
        Assert.That(line, Does.Not.Contain("{"));
        Assert.That(PersonalityBehaviours.InspectLine("Ann", "Spiritmaster", null, new Random(1)), Is.Null);
    }

    [Test]
    public void EveryBehaviourLine_FillsItsPlaceholders()
    {
        var slots = new LineSlots("Mularn", "a large skeleton", 10, "Warrior", "a sword");
        foreach (string[] pool in new[]
                 {
                     BehaviourLines.LeeroyBattleCries, BehaviourLines.FullLeeroyCries, BehaviourLines.LonerInviteDeclines,
                     BehaviourLines.LonerInviteAccepts, BehaviourLines.CasualBreakStarts, BehaviourLines.CasualBreakEnds,
                     BehaviourLines.HardcoreSitComplaints,
                 })
        {
            Assert.That(pool, Is.Not.Empty);
            foreach (string line in pool)
                Assert.That(PersonalityReactions.Fill(line, slots, "Ann"), Is.Not.Null.And.Not.Contain("{"), line);
        }
        foreach (string line in BehaviourLines.MinMaxerInspects)
            Assert.That(line, Does.Contain("{spec}").And.Contain("{class}"), line);
    }
}
