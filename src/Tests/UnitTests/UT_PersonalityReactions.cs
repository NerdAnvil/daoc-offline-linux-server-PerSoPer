using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityReactions
{
    private static readonly LineSlots Full = new("Myrkwood Forest", "a grass snake", 12, "Thane", "a Runed Helm");
    private static readonly LineSlots Empty = new(null, "", 0, null, null);

    [Test]
    public void Fill_ReplacesEverySlotAndName()
    {
        string line = PersonalityReactions.Fill("{0}: {zone} {monster} {level} {class} {item}", Full, "Bob");
        Assert.That(line, Is.EqualTo("Bob: Myrkwood Forest a grass snake 12 Thane a Runed Helm"));
    }

    [Test]
    public void Fill_SkipsLinesWithAMissingSlot()
    {
        Assert.That(PersonalityReactions.Fill("Hunting {monster} here.", Empty), Is.Null);
        Assert.That(PersonalityReactions.Fill("Ding {level}!", Empty), Is.Null);
        Assert.That(PersonalityReactions.Fill("No slots at all.", Empty), Is.EqualTo("No slots at all."));
        Assert.That(PersonalityReactions.Fill("Grats {0}!", Empty, null), Is.EqualTo("Grats friend!"));
    }

    [Test]
    public void EveryTypeVoice_HasLines()
    {
        Assert.That(PersonalityTypeVoices.All.Keys, Is.EquivalentTo(PersonalityLines.All.Keys));
        foreach (var (key, template) in PersonalityTypeVoices.All)
            Assert.That(PersonalityReactions.LinesFor(template with { TypeKey = key }), Is.SameAs(PersonalityLines.All[key]), key);
        Assert.That(PersonalityReactions.LinesFor(PersoperIdentity.Personality), Is.Null);
    }

    [Test]
    public void Opener_ComesFromItsTopicPool_AndIsFilled()
    {
        var random = new Random(1);
        foreach (var (key, lines) in PersonalityLines.All)
        for (int i = 0; i < 200; i++)
        {
            string line = PersonalityReactions.Opener(lines, Full, toneDown: true, random, out eChatTopic? topic);
            Assert.That(topic, Is.Not.Null, key);
            Assert.That(line, Does.Not.Contain("{").And.Not.Contain("}"), key);
            Assert.That(lines.Openers[topic.Value].Any(raw => PersonalityReactions.Fill(raw, Full) == line), key);
        }
    }

    [Test]
    public void Opener_WithNoSlotsKnown_UsesOnlyPlainLines()
    {
        var random = new Random(2);
        foreach (var (key, lines) in PersonalityLines.All)
        for (int i = 0; i < 200; i++)
        {
            string line = PersonalityReactions.Opener(lines, Empty, toneDown: true, random, out _);
            // Null is allowed (every try hit a slot line); a line never has a hole in it.
            if (line != null)
                Assert.That(line, Does.Not.Contain("{"), key);
        }
    }

    [Test]
    public void CheekyLines_OnlyWithToneDownOff()
    {
        foreach (string key in new[] { "joker", "leeroy" })
        {
            TypeLines lines = PersonalityLines.All[key];
            var random = new Random(3);
            int cheekyOn = 0, cheekyOff = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (lines.Cheeky.Contains(PersonalityReactions.Opener(lines, Full, toneDown: false, random, out _)))
                    cheekyOn++;
                if (lines.Cheeky.Contains(PersonalityReactions.Opener(lines, Full, toneDown: true, random, out _)))
                    cheekyOff++;
            }
            Assert.That(cheekyOn, Is.InRange(50, 250), key);
            Assert.That(cheekyOff, Is.Zero, key);
        }
    }

    [Test]
    public void TopicReply_NamesTheOneWhoOpened()
    {
        var random = new Random(4);
        foreach (var (key, lines) in PersonalityLines.All)
        foreach (eChatTopic topic in Enum.GetValues<eChatTopic>())
        {
            string reply = PersonalityReactions.TopicReply(lines, topic, "Sigrun", Full, random);
            Assert.That(reply, Does.Contain("Sigrun"), $"{key} {topic}");
        }
        Assert.That(PersonalityReactions.TopicReply(null, eChatTopic.Xp, "Sigrun", Full, random), Is.Null);
    }

    [Test]
    public void Reactions_ForEveryEvent()
    {
        var random = new Random(5);
        foreach (var (key, lines) in PersonalityLines.All)
        foreach (ePersonalityEvent kind in Enum.GetValues<ePersonalityEvent>())
        {
            string line = PersonalityReactions.Reaction(lines, kind, "Sigrun", Full, random);
            Assert.That(line, Is.Not.Null.And.Not.Contain("{"), $"{key} {kind}");
            if (kind is ePersonalityEvent.Grats or ePersonalityEvent.OtherDeath)
                Assert.That(line, Does.Contain("Sigrun"), $"{key} {kind}");
        }
    }

    [Test]
    public void ReactChance_FollowsVolumeAndChattiness()
    {
        Assert.That(PersonalityReactions.ReactChance(90, 0, true), Is.Zero);
        Assert.That(PersonalityReactions.ReactChance(90, 100, true), Is.GreaterThan(PersonalityReactions.ReactChance(90, 100, false)));
        Assert.That(PersonalityReactions.ReactChance(15, 100, false), Is.LessThan(PersonalityReactions.ReactChance(90, 100, false)));
        Assert.That(PersonalityReactions.ReactChance(90, 50, true), Is.LessThan(PersonalityReactions.ReactChance(90, 100, true)));
        foreach (int chattiness in new[] { -5, 0, 50, 100, 500 })
        foreach (int volume in new[] { -10, 0, 50, 100, 300 })
        foreach (bool own in new[] { true, false })
            Assert.That(PersonalityReactions.ReactChance(chattiness, volume, own), Is.InRange(0.0, 1.0));
    }

    [Test]
    public void GroupingLines_FromGroupmates_GetBanterInstead()
    {
        Assert.That(AutonomousBotChatCoordinator.NotWhenGrouped(eAutonomousChatIntent.Grouping, sameGroup: true), Is.EqualTo(eAutonomousChatIntent.Banter));
        Assert.That(AutonomousBotChatCoordinator.NotWhenGrouped(eAutonomousChatIntent.Grouping, sameGroup: false), Is.EqualTo(eAutonomousChatIntent.Grouping));
        Assert.That(AutonomousBotChatCoordinator.NotWhenGrouped(eAutonomousChatIntent.Dungeon, sameGroup: true), Is.EqualTo(eAutonomousChatIntent.Dungeon));
    }
}
