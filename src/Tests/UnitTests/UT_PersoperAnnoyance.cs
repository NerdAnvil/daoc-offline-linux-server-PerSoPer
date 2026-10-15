using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersoperAnnoyance
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Irritations_BuildUpToARageQuit()
    {
        var tally = new PersoperAnnoyance.Tally();
        var reactions = Enumerable.Range(0, PersoperAnnoyance.QuitAt)
            .Select(i => PersoperAnnoyance.Add(tally, Now.AddSeconds(i * 10), rageQuit: true))
            .ToArray();
        Assert.That(reactions, Is.EqualTo(new[]
        {
            PersoperAnnoyance.eReaction.Grumble,
            PersoperAnnoyance.eReaction.Grumble,
            PersoperAnnoyance.eReaction.Warning,
            PersoperAnnoyance.eReaction.LastWarning,
            PersoperAnnoyance.eReaction.RageQuit,
        }));
    }

    [Test]
    public void RageQuitOff_StopsAtTheLastWarning()
    {
        var tally = new PersoperAnnoyance.Tally();
        PersoperAnnoyance.eReaction last = default;
        for (int i = 0; i < PersoperAnnoyance.QuitAt + 3; i++)
            last = PersoperAnnoyance.Add(tally, Now.AddSeconds(i), rageQuit: false);
        Assert.That(last, Is.EqualTo(PersoperAnnoyance.eReaction.LastWarning));
    }

    [Test]
    public void QuietSpell_ResetsTheCount()
    {
        var tally = new PersoperAnnoyance.Tally();
        for (int i = 0; i < PersoperAnnoyance.LastWarnAt; i++)
            PersoperAnnoyance.Add(tally, Now, rageQuit: true);
        var after = PersoperAnnoyance.Add(tally, Now + PersoperAnnoyance.CalmDown + TimeSpan.FromSeconds(1), rageQuit: true);
        Assert.That(after, Is.EqualTo(PersoperAnnoyance.eReaction.Grumble));
        Assert.That(tally.Points, Is.EqualTo(1));
    }

    [Test]
    public void Emotes_CountOnlyWhenSpammed()
    {
        var tally = new PersoperAnnoyance.Tally();
        for (int i = 0; i < PersoperAnnoyance.FreeEmotes; i++)
            Assert.That(PersoperAnnoyance.CountEmote(tally, Now.AddSeconds(i)), Is.False);
        Assert.That(PersoperAnnoyance.CountEmote(tally, Now.AddSeconds(5)), Is.True);
        // A minute later the old ones have expired.
        Assert.That(PersoperAnnoyance.CountEmote(tally, Now.AddMinutes(3)), Is.False);
        // The list stays bounded however much someone spams.
        for (int i = 0; i < 100; i++)
            PersoperAnnoyance.CountEmote(tally, Now.AddMinutes(3).AddMilliseconds(i));
        Assert.That(tally.Emotes.Count, Is.LessThanOrEqualTo(PersoperAnnoyance.FreeEmotes + 3));
    }

    [Test]
    public void QuickResummon_And_SulkTimes()
    {
        Assert.That(PersoperAnnoyance.IsQuickResummon(Now.AddSeconds(-30).ToString("O"), Now), Is.True);
        Assert.That(PersoperAnnoyance.IsQuickResummon(Now.AddMinutes(-5).ToString("O"), Now), Is.False);
        Assert.That(PersoperAnnoyance.IsQuickResummon(string.Empty, Now), Is.False);

        Assert.That(PersoperAnnoyance.SulkLeft(Now.AddMinutes(7).ToString("O"), Now), Is.EqualTo(TimeSpan.FromMinutes(7)));
        Assert.That(PersoperAnnoyance.SulkLeft(Now.AddMinutes(-1).ToString("O"), Now), Is.EqualTo(TimeSpan.Zero));
        Assert.That(PersoperAnnoyance.SulkLeft(null, Now), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Lines_NameThePlayerAndTheTimeLeft()
    {
        var random = new Random(6);
        foreach (PersoperAnnoyance.eIrritation irritation in Enum.GetValues<PersoperAnnoyance.eIrritation>())
        foreach (PersoperAnnoyance.eReaction reaction in Enum.GetValues<PersoperAnnoyance.eReaction>())
            Assert.That(PersoperAnnoyance.Line(irritation, reaction, "Tester", random), Is.Not.Empty.And.Not.Contain("{"));

        Assert.That(PersoperAnnoyance.SulkRefusal("Tester", TimeSpan.FromMinutes(6.2), random), Does.Contain("7 minutes"));
        Assert.That(PersoperAnnoyance.SulkRefusal("Tester", TimeSpan.FromSeconds(20), random), Does.Contain("a minute"));
    }
}
