using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityAnnoyance
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PatienceProfile Persoper = PersoperIdentity.Personality.Patience;
    private static readonly PatienceProfile Kobaldjovi = KobaldjoviIdentity.Personality.Patience;

    private static PersonalityAnnoyance.eReaction[] Run(PatienceProfile profile, int count, bool canQuit = true)
    {
        var tally = new PersonalityAnnoyance.Tally();
        return Enumerable.Range(0, count).Select(i => PersonalityAnnoyance.Add(tally, profile, Now.AddSeconds(i * 10), canQuit)).ToArray();
    }

    [Test]
    public void Persoper_WarnsAtThreeAndFour_QuitsAtFive()
    {
        Assert.That(Run(Persoper, 5), Is.EqualTo(new[]
        {
            PersonalityAnnoyance.eReaction.Grumble,
            PersonalityAnnoyance.eReaction.Grumble,
            PersonalityAnnoyance.eReaction.Warning,
            PersonalityAnnoyance.eReaction.LastWarning,
            PersonalityAnnoyance.eReaction.Quit,
        }));
    }

    [Test]
    public void Kobaldjovi_IsChill_WarnsAtFiveAndSix_QuitsAtSeven()
    {
        var reactions = Run(Kobaldjovi, 7);
        Assert.That(reactions.Take(4), Is.All.EqualTo(PersonalityAnnoyance.eReaction.Grumble));
        Assert.That(reactions[4], Is.EqualTo(PersonalityAnnoyance.eReaction.Warning));
        Assert.That(reactions[5], Is.EqualTo(PersonalityAnnoyance.eReaction.LastWarning));
        Assert.That(reactions[6], Is.EqualTo(PersonalityAnnoyance.eReaction.Quit));
        Assert.That(Kobaldjovi.CountsDeaths, Is.False);
        Assert.That(Persoper.CountsDeaths, Is.True);
        Assert.That(Kobaldjovi.CalmDown, Is.LessThan(Persoper.CalmDown));
    }

    [Test]
    public void Kobaldjovi_MakesUp_PersoperDoesNot()
    {
        // Persoper is summoned again instead; Kobaldjovi, a world bot, tells you and comes back.
        Assert.That(Persoper.MakeUps, Is.Null);
        Assert.That(Kobaldjovi.MakeUps, Is.Not.Empty);
        Assert.That(Kobaldjovi.MakeUpInvites, Is.Not.Empty);
        foreach (string line in Kobaldjovi.MakeUps.Concat(Kobaldjovi.MakeUpInvites))
            Assert.That(string.Format(line, "Tester"), Does.Contain("Tester").And.Not.Contain("{"));

        Assert.That(PersonalityAnnoyance.MakeUpDelay(10), Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(PersonalityAnnoyance.MakeUpDelay(0), Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(PersonalityAnnoyance.MakeUpDelay(500), Is.EqualTo(TimeSpan.FromMinutes(100)));
    }

    [Test]
    public void QuitOff_StopsAtTheLastWarning()
    {
        Assert.That(Run(Persoper, Persoper.QuitAt + 3, canQuit: false).Last(), Is.EqualTo(PersonalityAnnoyance.eReaction.LastWarning));
    }

    [Test]
    public void QuietSpell_ResetsTheCount_PerProfile()
    {
        foreach (PatienceProfile profile in new[] { Persoper, Kobaldjovi })
        {
            var tally = new PersonalityAnnoyance.Tally();
            for (int i = 0; i < profile.LastWarnAt; i++)
                PersonalityAnnoyance.Add(tally, profile, Now, canQuit: true);
            var after = PersonalityAnnoyance.Add(tally, profile, Now + profile.CalmDown + TimeSpan.FromSeconds(1), canQuit: true);
            Assert.That(after, Is.EqualTo(PersonalityAnnoyance.eReaction.Grumble));
            Assert.That(tally.Points, Is.EqualTo(1));
        }
    }

    [Test]
    public void Emotes_CountOnlyWhenSpammed()
    {
        var tally = new PersonalityAnnoyance.Tally();
        for (int i = 0; i < PersonalityAnnoyance.FreeEmotes; i++)
            Assert.That(PersonalityAnnoyance.CountEmote(tally, Now.AddSeconds(i)), Is.False);
        Assert.That(PersonalityAnnoyance.CountEmote(tally, Now.AddSeconds(5)), Is.True);
        Assert.That(PersonalityAnnoyance.CountEmote(tally, Now.AddMinutes(3)), Is.False);
        for (int i = 0; i < 100; i++)
            PersonalityAnnoyance.CountEmote(tally, Now.AddMinutes(3).AddMilliseconds(i));
        Assert.That(tally.Emotes.Count, Is.LessThanOrEqualTo(PersonalityAnnoyance.FreeEmotes + 3));
    }

    [Test]
    public void QuickRecall_And_SulkTimes()
    {
        Assert.That(PersonalityAnnoyance.IsQuickRecall(Now.AddSeconds(-30).ToString("O"), Now), Is.True);
        Assert.That(PersonalityAnnoyance.IsQuickRecall(Now.AddMinutes(-5).ToString("O"), Now), Is.False);
        Assert.That(PersonalityAnnoyance.IsQuickRecall(string.Empty, Now), Is.False);
        Assert.That(PersonalityAnnoyance.SulkLeft(Now.AddMinutes(7).ToString("O"), Now), Is.EqualTo(TimeSpan.FromMinutes(7)));
        Assert.That(PersonalityAnnoyance.SulkLeft(Now.AddMinutes(-1).ToString("O"), Now), Is.EqualTo(TimeSpan.Zero));
        Assert.That(PersonalityAnnoyance.SulkLeft((string)null, Now), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Lines_NameThePlayerAndTheTimeLeft_InEachVoice()
    {
        var random = new Random(6);
        foreach (PatienceProfile profile in new[] { Persoper, Kobaldjovi })
        {
            foreach (PersonalityAnnoyance.eIrritation irritation in Enum.GetValues<PersonalityAnnoyance.eIrritation>())
            foreach (PersonalityAnnoyance.eReaction reaction in Enum.GetValues<PersonalityAnnoyance.eReaction>())
                Assert.That(PersonalityAnnoyance.Line(profile, irritation, reaction, "Tester", random), Is.Not.Empty.And.Not.Contain("{"));
            Assert.That(PersonalityAnnoyance.SulkRefusal(profile, "Tester", TimeSpan.FromMinutes(6.2), random), Does.Contain("7 minutes"));
            Assert.That(PersonalityAnnoyance.SulkRefusal(profile, "Tester", TimeSpan.FromSeconds(20), random), Does.Contain("a minute"));
        }
        Assert.That(PersonalityAnnoyance.Line(Kobaldjovi, PersonalityAnnoyance.eIrritation.Rude, PersonalityAnnoyance.eReaction.Quit, "Tester", random),
            Does.Contain("Tester"));
    }
}
