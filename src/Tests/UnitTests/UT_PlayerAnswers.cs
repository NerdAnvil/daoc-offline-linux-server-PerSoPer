using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PlayerAnswers
{
    private readonly Dictionary<string, Dictionary<string, string>> _database = new();
    private readonly List<string> _loads = [];
    private readonly List<string> _saves = [];

    [SetUp]
    public void SetUp()
    {
        _database.Clear();
        _loads.Clear();
        _saves.Clear();
    }

    private PlayerAnswerCache NewCache() => new(
        id => { _loads.Add(id); return _database.GetValueOrDefault(id); },
        (id, question, answer) =>
        {
            _saves.Add($"{id}|{question}|{answer}");
            (_database.TryGetValue(id, out var answers) ? answers : _database[id] = new())[question] = answer;
        });

    [Test]
    public void Set_SavesAndGetReadsItBack()
    {
        PlayerAnswerCache cache = NewCache();

        Assert.That(cache.Set("p1", "fav_music", "metal"), Is.True);

        Assert.That(cache.Get("p1", "fav_music"), Is.EqualTo("metal"));
        Assert.That(cache.Get("p1", "other_question"), Is.Null);
        Assert.That(cache.Get("p2", "fav_music"), Is.Null);
        Assert.That(_saves, Is.EqualTo(new[] { "p1|fav_music|metal" }));
    }

    [Test]
    public void NewCache_ReadsWhatAnEarlierRunSaved()
    {
        NewCache().Set("p1", "fav_music", "metal");

        PlayerAnswerCache afterRestart = NewCache();

        Assert.That(afterRestart.Get("p1", "fav_music"), Is.EqualTo("metal"));
        Assert.That(afterRestart.For("p1"), Is.EquivalentTo(new Dictionary<string, string> { ["fav_music"] = "metal" }));
    }

    [Test]
    public void Set_ANewAnswerReplacesTheOldOne()
    {
        PlayerAnswerCache cache = NewCache();
        cache.Set("p1", "fav_music", "metal");
        cache.Set("p1", "fav_music", "ballads");

        Assert.That(cache.Get("p1", "fav_music"), Is.EqualTo("ballads"));
        Assert.That(cache.For("p1"), Has.Count.EqualTo(1));
    }

    [Test]
    public void ARepeatedLookup_DoesNotReadTheDatabaseAgain()
    {
        PlayerAnswerCache cache = NewCache();
        cache.Get("p1", "a");
        cache.Get("p1", "b");
        cache.For("p1");

        Assert.That(_loads, Is.EqualTo(new[] { "p1" }));
    }

    [TestCase(null, "q", "a")]
    [TestCase("", "q", "a")]
    [TestCase("p1", null, "a")]
    [TestCase("p1", "Bad Question", "a")]
    [TestCase("p1", "q", "")]
    [TestCase("p1", "q", "Metal")]
    [TestCase("p1", "q", "a very long answer that goes past thirty two characters")]
    public void Set_RejectsABadIdOrWord_AndSavesNothing(string playerId, string question, string answer)
    {
        PlayerAnswerCache cache = NewCache();

        Assert.That(cache.Set(playerId, question, answer), Is.False);
        Assert.That(_saves, Is.Empty);
    }

    [Test]
    public void Get_AnEmptyIdOrQuestion_IsNullWithoutADatabaseRead()
    {
        PlayerAnswerCache cache = NewCache();

        Assert.That(cache.Get("", "q"), Is.Null);
        Assert.That(cache.Get("p1", ""), Is.Null);
        Assert.That(cache.For(null), Is.Empty);
        Assert.That(_loads, Is.Empty);
    }

    [Test]
    public void Set_StopsAtTheAnswersPerPlayerCap_ButStillReplaces()
    {
        PlayerAnswerCache cache = NewCache();
        for (int i = 0; i < PlayerAnswerCache.MaxAnswersPerPlayer; i++)
            Assert.That(cache.Set("p1", $"q{i}", "a"), Is.True);

        Assert.That(cache.Set("p1", "one_more", "a"), Is.False);
        Assert.That(cache.Set("p1", "q0", "b"), Is.True);
        Assert.That(cache.Get("p1", "q0"), Is.EqualTo("b"));
    }

    [Test]
    public void TheCache_StartsOverAtThePlayerCap_AndReloadsFromTheDatabase()
    {
        PlayerAnswerCache cache = NewCache();
        for (int i = 0; i < PlayerAnswerCache.MaxPlayers + 5; i++)
            cache.Set($"p{i}", "q", "a");

        Assert.That(cache.Get("p0", "q"), Is.EqualTo("a"));
        Assert.That(_loads.Count(id => id == "p0"), Is.EqualTo(2));
    }

    [Test]
    public void For_ReturnsACopy()
    {
        PlayerAnswerCache cache = NewCache();
        cache.Set("p1", "q", "a");

        ((Dictionary<string, string>)cache.For("p1")).Clear();

        Assert.That(cache.Get("p1", "q"), Is.EqualTo("a"));
    }
}
