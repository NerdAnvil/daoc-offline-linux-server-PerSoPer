using System;
using DOL.GS.Quests;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// The quest-talk truce's name list is read from the database on first use. A read that met a locked database
/// used to be kept as a failure for good (Lazy&lt;T&gt;), so every later aggro check threw and companions were taken
/// out of the world. These check that a failure is not kept, is retried only after the gap, and is reported once.
/// </summary>
[TestFixture]
public class UT_QuestTalkTruceLoad
{
    private const long Gap = 30_000;

    [Test]
    public void FailedLoad_ReturnsNull_ThenSucceedsAfterTheGap()
    {
        int calls = 0;
        var load = new QuestTalkTruce.RetryingLoad<string>(() => ++calls == 1 ? throw new InvalidOperationException("database is locked") : "names", Gap);

        Assert.That(load.TryGet(0, out Exception error, out bool recovered), Is.Null);
        Assert.That(error, Is.Not.Null);
        Assert.That(recovered, Is.False);

        Assert.That(load.TryGet(Gap - 1, out error, out _), Is.Null, "no new attempt inside the gap");
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(error, Is.Null);

        Assert.That(load.TryGet(Gap, out error, out recovered), Is.EqualTo("names"));
        Assert.That(error, Is.Null);
        Assert.That(recovered, Is.True);
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void RepeatedFailures_ReportOnlyTheFirst()
    {
        var load = new QuestTalkTruce.RetryingLoad<string>(() => throw new InvalidOperationException("database is locked"), Gap);

        load.TryGet(0, out Exception first, out _);
        load.TryGet(Gap, out Exception second, out _);
        load.TryGet(2 * Gap, out Exception third, out _);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Null);
        Assert.That(third, Is.Null);
    }

    [Test]
    public void SuccessfulLoad_IsKeptAndNotRepeated()
    {
        int calls = 0;
        var load = new QuestTalkTruce.RetryingLoad<string>(() => { calls++; return "names"; }, Gap);

        Assert.That(load.TryGet(0, out _, out bool recovered), Is.EqualTo("names"));
        Assert.That(recovered, Is.False);
        Assert.That(load.TryGet(10 * Gap, out _, out _), Is.EqualTo("names"));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void NullResult_CountsAsAFailure()
    {
        var load = new QuestTalkTruce.RetryingLoad<string>(() => null, Gap);

        Assert.That(load.TryGet(0, out Exception error, out _), Is.Null);
        Assert.That(error, Is.Not.Null);
    }
}
