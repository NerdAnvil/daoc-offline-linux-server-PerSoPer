using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersoperMemory
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static PersoperMemoryRecord Met(int daysAgo, int summons = 3) => new()
    {
        PlayerId = "id",
        PlayerName = "Tester",
        FirstMetUtc = Now.AddDays(-30).ToString("O"),
        LastSeenUtc = Now.AddDays(-daysAgo).ToString("O"),
        SummonCount = summons,
    };

    [TestCase(eCharacterClass.Healer)]
    [TestCase(eCharacterClass.Cleric)]
    [TestCase(eCharacterClass.Druid)]
    public void EveryHealerBuild_HasAPlainPhraseAndAnnouncement(eCharacterClass characterClass)
    {
        foreach (eSpecType spec in BotSpec.GetSpecializationChoices(characterClass))
        {
            Assert.That(PersoperMemory.SpecPhrase(spec), Is.Not.Null, spec.ToString());
            string line = PersoperMemory.SpecAnnouncement(spec, eSpecType.None, "Tester", new Random(1));
            Assert.That(line, Is.Not.Empty.And.Not.Contain("{"), spec.ToString());
        }
    }

    [Test]
    public void SpecAnnouncement_SometimesMentionsLastTimesBuild()
    {
        var random = new Random(3);
        bool mentioned = Enumerable.Range(0, 40)
            .Select(_ => PersoperMemory.SpecAnnouncement(eSpecType.AugHealer, eSpecType.MendHealer, "Tester", random))
            .Any(line => line.Contains("heal-heavy last time"));
        Assert.That(mentioned, Is.True);
        Assert.That(PersoperMemory.SpecAnnouncement(eSpecType.None, eSpecType.None, "Tester", random), Is.Null);
    }

    [Test]
    public void Mood_FollowsNiceAndRudeBalance()
    {
        var memory = Met(1);
        Assert.That(PersoperMemory.MoodOf(memory), Is.EqualTo(PersoperMemory.eMood.Neutral));
        for (int i = 0; i < PersoperMemory.MoodMargin; i++)
            PersoperMemory.RecordChat(memory, eAutonomousChatIntent.Abuse, "you're useless");
        Assert.That(PersoperMemory.MoodOf(memory), Is.EqualTo(PersoperMemory.eMood.Cool));
        for (int i = 0; i < PersoperMemory.MoodMargin * 2; i++)
            PersoperMemory.RecordChat(memory, eAutonomousChatIntent.Greeting, "hi");
        Assert.That(PersoperMemory.MoodOf(memory), Is.EqualTo(PersoperMemory.eMood.Warm));
        PersoperMemory.RecordChat(memory, eAutonomousChatIntent.Travel, "which way?");
        Assert.That(memory.NiceCount + memory.RudeCount, Is.EqualTo(PersoperMemory.MoodMargin * 3));
    }

    [Test]
    public void Insult_IsTrimmedAndCannotBreakFormatting()
    {
        var memory = Met(1);
        PersoperMemory.RecordChat(memory, eAutonomousChatIntent.Abuse, "{0} " + new string('x', 100));
        Assert.That(memory.LastInsult.Length, Is.LessThanOrEqualTo(PersoperMemoryRecord.MaxInsultLength));
        Assert.That(memory.LastInsult, Does.Not.Contain("{"));
        memory.RudeCount = 10;
        var random = new Random(2);
        for (int i = 0; i < 20; i++)
            Assert.That(PersoperMemory.RememberedLine(memory, "Tester", Now, random), Does.Contain("Tester"));
    }

    [Test]
    public void RecordSummon_KeepsFirstMeetingAndCounts()
    {
        var memory = new PersoperMemoryRecord { PlayerId = "id" };
        Assert.That(PersoperMemory.IsNew(memory), Is.True);
        Assert.That(PersoperMemory.RememberedLine(memory, "Tester", Now, new Random(1)), Is.Null);

        PersoperMemory.RecordSummon(memory, "Tester", eSpecType.MendHealer, "Vale of Mularn", Now.AddDays(-2));
        string first = memory.FirstMetUtc;
        PersoperMemory.RecordSummon(memory, "Tester", eSpecType.AugHealer, "Jordheim", Now);
        Assert.That(memory.FirstMetUtc, Is.EqualTo(first));
        Assert.That(memory.SummonCount, Is.EqualTo(2));
        Assert.That((eSpecType)memory.LastSpec, Is.EqualTo(eSpecType.AugHealer));
        Assert.That(memory.LastZone, Is.EqualTo("Jordheim"));
    }

    [TestCase(3, "3 days")]
    [TestCase(14, "2 weeks")]
    public void RememberedLine_CanMentionTheGap(int daysAgo, string expected)
    {
        var random = new Random(4);
        bool mentioned = Enumerable.Range(0, 40).Any(_ => PersoperMemory.RememberedLine(Met(daysAgo), "Tester", Now, random).Contains(expected));
        Assert.That(mentioned, Is.True);
    }

    [Test]
    public void Recall_SummarisesHistory()
    {
        Assert.That(PersoperMemory.Recall(new PersoperMemoryRecord(), "Tester", Now), Does.Contain("just met"));
        var memory = Met(1, summons: 4);
        memory.LastZone = "Vale of Mularn";
        memory.LastSpec = (int)eSpecType.PacHealer;
        string recall = PersoperMemory.Recall(memory, "Tester", Now);
        Assert.That(recall, Does.Contain("4 times").And.Contain("Vale of Mularn").And.Contain("crowd control").And.Contain("30 days"));
    }

    [TestCase("what's your spec?", true, false)]
    [TestCase("Persoper, what build are you?", true, false)]
    [TestCase("do you remember me?", false, true)]
    [TestCase("hello there", false, false)]
    public void Questions_AreRecognised(string message, bool spec, bool memory)
    {
        Assert.That(PersoperMemory.AsksSpec(message), Is.EqualTo(spec));
        Assert.That(PersoperMemory.AsksMemory(message), Is.EqualTo(memory));
    }
}
