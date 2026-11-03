using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B3a: a riddle gets a guess and waits for its punchline, which gets a groan or a laugh;
/// knock-knock goes "Who's there?" and "X who?"; a joke told in full is groaned at straight away;
/// "tell me a joke" gets one of the bot's own.
/// </summary>
[TestFixture]
public class UT_JokeFlow
{
    [SetUp]
    public void Forget() => PersonalityJokes.Clear();

    [TestCase("why did the troll cross the road?", eJokeKind.Riddle)]
    [TestCase("why did the troll cross the road", eJokeKind.Riddle)]
    [TestCase("shnacki, want to know why the troll crossed the road?", eJokeKind.Riddle)]
    [TestCase("Persoper, what do you call a kobold with a sword?", eJokeKind.Riddle)]
    [TestCase("Persoper, why did the astronaut leave Pluto? It had no atmosphere", eJokeKind.Told)]
    [TestCase("a dwarf walks into a bar and orders a mead", eJokeKind.Told)]
    [TestCase("knock knock", eJokeKind.KnockKnock)]
    [TestCase("shemp, knock knock", eJokeKind.KnockKnock)]
    [TestCase("tell me a joke", eJokeKind.Ask)]
    [TestCase("jovi, know any jokes?", eJokeKind.Ask)]
    [TestCase("jk", eJokeKind.None)]
    [TestCase("just kidding", eJokeKind.None)]
    [TestCase("anyone want to group?", eJokeKind.None)]
    public void Lines_AreSortedIntoJokeKinds(string line, eJokeKind expected)
    {
        Assert.That(PersonalityJokes.Kind(line, AutonomousChatIntentModel.Read(line)), Is.EqualTo(expected));
    }

    [TestCase("boo", "Boo who?")]
    [TestCase("Lettuce!", "Lettuce who?")]
    [TestCase("  orange. ", "Orange who?")]
    public void KnockWho_EchoesTheName(string name, string expected)
    {
        Assert.That(PersonalityJokes.KnockWho(name), Is.EqualTo(expected));
    }

    [Test]
    public void KnockWho_IsNullForALongLine()
    {
        Assert.That(PersonalityJokes.KnockWho("this is not a name at all really"), Is.Null);
        Assert.That(PersonalityJokes.KnockWho(" ?! "), Is.Null);
    }

    [TestCase("to get to the alb!", true)]
    [TestCase("no, to get to the other side", true)]
    [TestCase("because it had no atmosphere", true)]
    [TestCase("anyone want to group?", false)]
    [TestCase("why did the chicken cross the road?", false)]
    [TestCase("knock knock", false)]
    public void Punchline_IsAShortLineThatIsntARequestOrANewJoke(string line, bool expected)
    {
        Assert.That(PersonalityJokes.CanAnswer(eJokeStage.Punchline, line), Is.EqualTo(expected));
    }

    [Test]
    public void OpenJoke_IsTakenOnce_ByItsAnswer()
    {
        DateTime now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.That(PersonalityJokes.Start("player", null, eJokeStage.Punchline, now), Is.True);
        Assert.That(PersonalityJokes.TryTake("player", "anyone want to group?", now), Is.Null, "not a punchline: stays open");
        Assert.That(PersonalityJokes.TryTake("other", "to get to the alb!", now), Is.Null, "another player's line");
        Assert.That(PersonalityJokes.TryTake("player", "to get to the alb!", now.AddSeconds(5)), Is.Not.Null);
        Assert.That(PersonalityJokes.TryTake("player", "to get to the alb!", now.AddSeconds(6)), Is.Null, "already answered");
    }

    [Test]
    public void OpenJoke_DropsAfterTheTimeout()
    {
        DateTime now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        PersonalityJokes.Start("player", null, eJokeStage.Punchline, now);
        Assert.That(PersonalityJokes.TryTake("player", "to get to the alb!", now + PersonalityJokes.Timeout), Is.Null);
    }

    [Test]
    public void OpenJokes_AreCapped()
    {
        DateTime now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < PersonalityJokes.MaxOpen; i++)
            Assert.That(PersonalityJokes.Start("p" + i, null, eJokeStage.Punchline, now), Is.True);
        Assert.That(PersonalityJokes.Start("one more", null, eJokeStage.Punchline, now), Is.False);
        Assert.That(PersonalityJokes.Start("one more", null, eJokeStage.Punchline, now + PersonalityJokes.Timeout), Is.True, "expired ones make room");
    }

    [Test]
    public void Persoper_GuessesGroansLaughsAndTellsHerOwn()
    {
        BotPersonality persoper = DialoguePreview.Find("Persoper");
        Random random = new(5);
        string[] guesses = Filled(persoper, eAutonomousChatIntent.JokeGuess);
        string[] reactions = Filled(persoper, eAutonomousChatIntent.Groan).Concat(Filled(persoper, eAutonomousChatIntent.JokeLaugh)).ToArray();
        string[] jokes = Filled(persoper, eAutonomousChatIntent.JokeAsk);
        for (int i = 0; i < 30; i++)
        {
            Assert.That(guesses, Does.Contain(PersonalityJokes.Opening(persoper, eJokeKind.Riddle, "Tester", random)));
            Assert.That(reactions, Does.Contain(PersonalityJokes.Opening(persoper, eJokeKind.Told, "Tester", random)));
            Assert.That(reactions, Does.Contain(PersonalityJokes.Punchline(persoper, "Tester", random)));
            Assert.That(jokes, Does.Contain(PersonalityJokes.Opening(persoper, eJokeKind.Ask, "Tester", random)));
        }
        Assert.That(PersonalityJokes.Opening(persoper, eJokeKind.KnockKnock, "Tester", random), Is.EqualTo("Who's there?"));
        Assert.That(PersonalityJokes.Opening(persoper, eJokeKind.None, "Tester", random), Is.Null);
    }

    [Test]
    public void Punchline_IsMostlyAGroan()
    {
        BotPersonality casual = DialoguePreview.Find("casual");
        HashSet<string> groans = Filled(casual, eAutonomousChatIntent.Groan).ToHashSet();
        Random random = new(9);
        const int tries = 1000;
        int groaned = Enumerable.Range(0, tries).Count(_ => groans.Contains(PersonalityJokes.Punchline(casual, "Tester", random)));
        Assert.That(groaned / (double)tries, Is.EqualTo(PersonalityJokes.GroanChance).Within(0.06));
    }

    // B3b: now and then the bot that groaned says it has one too, and tells one of its own.
    [Test]
    public void JokeBack_ComesNowAndThen_LeadInThenAJoke()
    {
        BotPersonality persoper = DialoguePreview.Find("Persoper");
        HashSet<string> leadIns = Filled(persoper, eAutonomousChatIntent.JokeBack).ToHashSet();
        HashSet<string> jokes = Filled(persoper, eAutonomousChatIntent.JokeAsk).ToHashSet();
        Random random = new(13);
        const int tries = 2000;
        int told = 0;
        for (int i = 0; i < tries; i++)
        {
            if (PersonalityJokes.JokeBack(persoper, "Tester", random) is not { } back)
                continue;
            told++;
            Assert.That(leadIns, Does.Contain(back.LeadIn));
            Assert.That(jokes, Does.Contain(back.Joke));
        }
        Assert.That(told / (double)tries, Is.EqualTo(PersonalityJokes.JokeBackChance).Within(0.04));
    }

    [Test]
    public void JokeBack_WithoutItsOwnJokes_TellsOneFromItsType_AndNoJokeMeansNone()
    {
        BotPersonality casual = NoJokes();
        Random random = new(2);
        var backs = Enumerable.Range(0, 200).Select(_ => PersonalityJokes.JokeBack(casual, "Tester", random, null, () => "a type joke"))
            .Where(back => back != null).ToArray();
        Assert.That(backs, Is.Not.Empty);
        Assert.That(backs.Select(back => back.Value.Joke), Is.All.EqualTo("a type joke"));
        Assert.That(Enumerable.Range(0, 200).Select(_ => PersonalityJokes.JokeBack(casual, "Tester", random, null, () => null)), Is.All.Null);
    }

    [Test]
    public void ABotWithoutItsOwnJokes_TellsOneFromItsType()
    {
        BotPersonality casual = NoJokes();
        Assert.That(PersonalityJokes.Opening(casual, eJokeKind.Ask, "Tester", new Random(1), null, () => "a type joke"), Is.EqualTo("a type joke"));
    }

    // Every type, Persoper, Kobaldjovi and every named character (mixed over its type) can guess, groan, laugh, tell a joke and tell one back.
    [Test]
    public void EveryPersonality_HasJokeFlowLines()
    {
        string[] names = PersonalityTypes.Templates.Keys.Concat(["Persoper", "Kobaldjovi"])
            .Concat(NamedBotLineup.All.Select(entry => entry.Name)).ToArray();
        foreach (string name in names)
        {
            BotPersonality personality = DialoguePreview.Find(name);
            foreach (eAutonomousChatIntent kind in new[] { eAutonomousChatIntent.JokeGuess, eAutonomousChatIntent.Groan, eAutonomousChatIntent.JokeLaugh, eAutonomousChatIntent.JokeBack, eAutonomousChatIntent.JokeAsk })
                Assert.That(personality.Replies.GetValueOrDefault(kind), Is.Not.Null.And.Length.AtLeast(2), $"{name} {kind}");
        }
    }

    [Test]
    public void JokeReplyKinds_AreNeverReadFromChat()
    {
        Assert.That(AutonomousChatIntentModel.IsJokeReplyKind(eAutonomousChatIntent.Groan), Is.True);
        Assert.That(AutonomousChatIntentModel.IsSmallTalk(eAutonomousChatIntent.JokeGuess), Is.False);
        foreach (string line in new[] { "groan", "joke guess", "ha ha", "ugh" })
            Assert.That(AutonomousChatIntentModel.IsJokeReplyKind(AutonomousChatIntentModel.Read(line)), Is.False, line);
    }

    // A personality with a joke-back lead-in but no "~ reply jokeask": its type's jokes stand in.
    private static BotPersonality NoJokes() => new()
    {
        Key = "nojokes", Name = "Nojokes", Matches = _ => false, FallbackReplies = ["Hm?"],
        Replies = new Dictionary<eAutonomousChatIntent, string[]> { [eAutonomousChatIntent.JokeBack] = ["Got one."] },
    };

    private static string[] Filled(BotPersonality personality, eAutonomousChatIntent kind) =>
        personality.Replies[kind].Select(line => string.Format(line, "Tester")).ToArray();
}
