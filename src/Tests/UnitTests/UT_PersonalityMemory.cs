using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityMemory
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BotPersonality Persoper = PersoperIdentity.Personality;
    private static readonly BotPersonality Kobaldjovi = KobaldjoviIdentity.Personality;
    private static readonly BotPersonality[] Both = [Persoper, Kobaldjovi];

    private static PersonalityMemoryRecord Met(int daysAgo, int meetings = 3) => new()
    {
        MemoryKey = "test|id",
        PlayerId = "id",
        PlayerName = "Tester",
        FirstMetUtc = Now.AddDays(-30).ToString("O"),
        LastSeenUtc = Now.AddDays(-daysAgo).ToString("O"),
        MeetCount = meetings,
    };

    [Test]
    public void EveryPersonality_HasMemoryBuildAndPatience()
    {
        foreach (BotPersonality personality in BotPersonalities.All)
        {
            Assert.That(personality.Memory, Is.Not.Null, personality.Name);
            Assert.That(personality.Build, Is.Not.Null, personality.Name);
            Assert.That(personality.Patience, Is.Not.Null, personality.Name);
        }
        Assert.That(BotPersonalities.All.Select(p => p.Key), Is.Unique);
    }

    [TestCase(eCharacterClass.Healer)]
    [TestCase(eCharacterClass.Cleric)]
    [TestCase(eCharacterClass.Druid)]
    public void EveryPersoperBuild_HasAPhraseAndAnnouncement(eCharacterClass characterClass)
    {
        foreach (eSpecType spec in BotSpec.GetSpecializationChoices(characterClass))
        {
            Assert.That(Persoper.Build.Phrase(spec), Is.Not.Null, spec.ToString());
            Assert.That(PersonalityMemory.SpecAnnouncement(Persoper, spec, eSpecType.None, "Tester", new Random(1)),
                Is.Not.Empty.And.Not.Contain("{"), spec.ToString());
        }
    }

    [Test]
    public void EveryKobaldjoviBuild_HasAPhraseAndAnnouncement()
    {
        foreach (eSpecType spec in BotSpec.GetSpecializationChoices(KobaldjoviIdentity.CharacterClass))
        {
            Assert.That(Kobaldjovi.Build.Phrase(spec), Is.Not.Null, spec.ToString());
            Assert.That(PersonalityMemory.SpecAnnouncement(Kobaldjovi, spec, eSpecType.None, "Tester", new Random(1)),
                Is.Not.Empty.And.Not.Contain("{"), spec.ToString());
        }
    }

    [Test]
    public void SpecAnnouncement_SometimesMentionsLastTimesBuild()
    {
        var random = new Random(3);
        Assert.That(Enumerable.Range(0, 40)
            .Select(_ => PersonalityMemory.SpecAnnouncement(Persoper, eSpecType.AugHealer, eSpecType.MendHealer, "Tester", random))
            .Any(line => line.Contains("heal-heavy last time")), Is.True);
        Assert.That(Enumerable.Range(0, 40)
            .Select(_ => PersonalityMemory.SpecAnnouncement(Kobaldjovi, eSpecType.TwoHanded, eSpecType.OneHandAndShield, "Tester", random))
            .Any(line => line.Contains("sword and board")), Is.True);
        Assert.That(PersonalityMemory.SpecAnnouncement(Persoper, eSpecType.None, eSpecType.None, "Tester", random), Is.Null);
    }

    [Test]
    public void Mood_FollowsNiceAndRudeBalance()
    {
        var memory = Met(1);
        Assert.That(PersonalityMemory.MoodOf(memory), Is.EqualTo(PersonalityMemory.eMood.Neutral));
        for (int i = 0; i < PersonalityMemory.MoodMargin; i++)
            PersonalityMemory.RecordChat(memory, eAutonomousChatIntent.Abuse, "you're useless");
        Assert.That(PersonalityMemory.MoodOf(memory), Is.EqualTo(PersonalityMemory.eMood.Cool));
        for (int i = 0; i < PersonalityMemory.MoodMargin * 2; i++)
            PersonalityMemory.RecordChat(memory, eAutonomousChatIntent.Greeting, "hi");
        Assert.That(PersonalityMemory.MoodOf(memory), Is.EqualTo(PersonalityMemory.eMood.Warm));
        PersonalityMemory.RecordChat(memory, eAutonomousChatIntent.Travel, "which way?");
        Assert.That(memory.NiceCount + memory.RudeCount, Is.EqualTo(PersonalityMemory.MoodMargin * 3));
    }

    [Test]
    public void RememberedLines_FillEveryTemplateInEveryMood()
    {
        var random = new Random(2);
        foreach (BotPersonality personality in Both)
        foreach (int rude in new[] { 0, 10 })
        foreach (int nice in new[] { 0, 10 })
        foreach (int daysAgo in new[] { 0, 3, 14 })
        {
            var memory = Met(daysAgo);
            memory.RudeCount = rude;
            memory.NiceCount = nice;
            memory.LastInsult = rude > 0 ? "{0} you're useless" : string.Empty;
            PersonalityMemory.RecordChat(memory, eAutonomousChatIntent.Abuse, memory.LastInsult);
            for (int i = 0; i < 10; i++)
            {
                string line = PersonalityMemory.RememberedLine(personality, memory, "Tester", Now, random);
                Assert.That(line, Does.Contain("Tester").And.Not.Contain("{"), $"{personality.Name} rude={rude} nice={nice} days={daysAgo}");
            }
        }
    }

    [TestCase(3, "3 days")]
    [TestCase(14, "2 weeks")]
    public void RememberedLine_CanMentionTheGap(int daysAgo, string expected)
    {
        var random = new Random(4);
        Assert.That(Enumerable.Range(0, 40).Any(_ => PersonalityMemory.RememberedLine(Persoper, Met(daysAgo), "Tester", Now, random).Contains(expected)), Is.True);
    }

    [Test]
    public void RecordMeeting_KeepsFirstMeetingAndCounts()
    {
        var memory = new PersonalityMemoryRecord { PlayerId = "id" };
        Assert.That(PersonalityMemory.IsNew(memory), Is.True);
        Assert.That(PersonalityMemory.RememberedLine(Kobaldjovi, memory, "Tester", Now, new Random(1)), Is.Null);

        PersonalityMemory.RecordMeeting(memory, "Tester", eSpecType.TwoHanded, "Vale of Mularn", Now.AddDays(-2));
        string first = memory.FirstMetUtc;
        PersonalityMemory.RecordMeeting(memory, "Tester", eSpecType.OneHandAndShield, "Jordheim", Now);
        Assert.That(memory.FirstMetUtc, Is.EqualTo(first));
        Assert.That(memory.MeetCount, Is.EqualTo(2));
        Assert.That((eSpecType)memory.LastSpec, Is.EqualTo(eSpecType.OneHandAndShield));
        Assert.That(memory.LastZone, Is.EqualTo("Jordheim"));
    }

    [Test]
    public void Recall_SummarisesHistoryInEachVoice()
    {
        Assert.That(PersonalityMemory.Recall(Persoper, new PersonalityMemoryRecord(), "Tester", Now), Does.Contain("just met"));
        Assert.That(PersonalityMemory.Recall(Kobaldjovi, new PersonalityMemoryRecord(), "Tester", Now), Does.Contain("just met"));

        var memory = Met(1, meetings: 4);
        memory.LastZone = "Vale of Mularn";
        memory.LastSpec = (int)eSpecType.PacHealer;
        Assert.That(PersonalityMemory.Recall(Persoper, memory, "Tester", Now),
            Does.Contain("4 times").And.Contain("Vale of Mularn").And.Contain("crowd control").And.Contain("30 days"));

        memory.LastSpec = (int)eSpecType.TwoHanded;
        memory.RudeCount = 5;
        Assert.That(PersonalityMemory.Recall(Kobaldjovi, memory, "Tester", Now),
            Does.Contain("grouped 4 times").And.Contain("two-handed").And.Contain("heckled me 5 times"));
    }

    [Test]
    public void LegacyPersoperMemory_MovesAcrossUnchanged()
    {
        var legacy = new PersoperMemoryRecord
        {
            PlayerId = "abc", PlayerName = "Tester", FirstMetUtc = "2026-01-01T00:00:00.0000000Z",
            LastSeenUtc = "2026-02-01T00:00:00.0000000Z", SummonCount = 7, NiceCount = 2, RudeCount = 4,
            LastSpec = (int)eSpecType.MendHealer, LastZone = "Jordheim", LastInsult = "useless", SulkUntilUtc = "x",
        };
        PersonalityMemoryRecord moved = PersonalityMemory.FromLegacy(legacy, Persoper.Key);
        Assert.That(moved.MemoryKey, Is.EqualTo("persoper|abc"));
        Assert.That((moved.Personality, moved.PlayerName, moved.FirstMetUtc, moved.LastSeenUtc, moved.MeetCount),
            Is.EqualTo(("persoper", "Tester", legacy.FirstMetUtc, legacy.LastSeenUtc, 7)));
        Assert.That((moved.NiceCount, moved.RudeCount, moved.LastSpec, moved.LastZone, moved.LastInsult, moved.SulkUntilUtc),
            Is.EqualTo((2, 4, (int)eSpecType.MendHealer, "Jordheim", "useless", "x")));
    }

    [Test]
    public void Tint_UsesEachVoice()
    {
        var cool = Met(1);
        cool.RudeCount = 10;
        var random = new Random(8);
        Assert.That(Enumerable.Range(0, 30).Select(_ => PersonalityMemory.Tint(Kobaldjovi, cool, "reply", random)).Any(r => r.StartsWith("*sour chord*")), Is.True);
        Assert.That(Enumerable.Range(0, 30).Select(_ => PersonalityMemory.Tint(Persoper, cool, "reply", random)).Any(r => r.StartsWith("Hmph.")), Is.True);
    }

    [TestCase("what's your spec?", true, false)]
    [TestCase("Kobaldjovi, what build are you?", true, false)]
    [TestCase("do you remember me?", false, true)]
    [TestCase("hello there", false, false)]
    public void Questions_AreRecognised(string message, bool spec, bool memory)
    {
        Assert.That(PersonalityMemory.AsksSpec(message), Is.EqualTo(spec));
        Assert.That(PersonalityMemory.AsksMemory(message), Is.EqualTo(memory));
    }
}
