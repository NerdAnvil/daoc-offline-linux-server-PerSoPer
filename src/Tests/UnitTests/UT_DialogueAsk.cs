using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_DialogueAsk
{
    private const string Good =
        "=== persoper\n~ ask fav_music\nWhat do you listen to, {0}? Metal or ballads?\n" +
        "-> metal: A person of taste!\n-> Ballads: Ooh, a softie.\n-> other: Never heard of it: I'll write a song.\n";

    [Test]
    public void Parse_ReadsTheQuestionAndItsAnswers()
    {
        DialogueFile file = DialogueFile.Parse(Good, "persoper.dlg");

        Assert.That(file.Errors, Is.Empty);
        DialogueSection ask = file.Block("persoper").Single();
        Assert.That((ask.Name, ask.Argument), Is.EqualTo(("ask", "fav_music")));
        Assert.That(ask.Lines, Is.EqualTo(new[] { "What do you listen to, {0}? Metal or ballads?" }));
        Assert.That(ask.Answers.Select(answer => (answer.Key, answer.Reply)), Is.EqualTo(new[]
        {
            ("metal", "A person of taste!"), ("ballads", "Ooh, a softie."), ("other", "Never heard of it: I'll write a song."),
        }));
    }

    [Test]
    public void Parse_OtherSectionsHaveNoAnswers()
    {
        DialogueFile file = DialogueFile.Parse("=== a\n~ ding\nDing!\n\\-> not an answer\n", "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("a")[0].Answers, Is.Null);
        Assert.That(file.Block("a")[0].Lines, Is.EqualTo(new[] { "Ding!", "-> not an answer" }));
    }

    [TestCase("=== a\n~ ask q\nQ?\n-> x: y\n", "bad.dlg:2: an ask needs a '-> other: reply' row for answers it doesn't know")]
    [TestCase("=== a\n~ ask q\nQ?\n", "bad.dlg:2: an ask needs '-> answer: reply' rows")]
    [TestCase("=== a\n~ ask q\n-> other: y\n", "bad.dlg:2: an ask has no question line")]
    [TestCase("=== a\n~ ask\nQ?\n-> other: y\n", "bad.dlg:2: an ask needs a name: '~ ask fav_music'")]
    [TestCase("=== a\n~ ask q\n-> other: y\nQ?\n", "bad.dlg:3: an ask's question line comes before its answers")]
    [TestCase("=== a\n~ ask q\nQ?\nQ2?\n-> other: y\n", "bad.dlg:4: an ask has one question line; answers start with '->'")]
    [TestCase("=== a\n~ ask q\nQ?\n-> other: y\n-> other: z\n", "bad.dlg:5: answer 'other' appears twice")]
    [TestCase("=== a\n~ ask q\nQ?\n-> other:\n", "bad.dlg:4: answer 'other' has no reply")]
    [TestCase("=== a\n~ ask q\nQ?\n-> other\n", "bad.dlg:4: an answer reads '-> key: reply'")]
    [TestCase("=== a\n~ ask q\nQ?\n-> two words: y\n-> other: z\n", "bad.dlg:4: bad answer 'two words' (lower-case letters, digits, _ or -, one word)")]
    [TestCase("=== a\n~ ding\nDing!\n-> x: y\n", "bad.dlg:4: '->' answers only work in '~ ask' sections (use \\-> for text)")]
    [TestCase("=== a\n~ ask q\nQ? [once]\n-> other: y\n", "bad.dlg:3: rules only work in reply, cheeky_reply, fallback sections for now")]
    public void Parse_ReportsAskProblemsWithFileAndRow(string text, string expected)
    {
        Assert.That(DialogueFile.Parse(text, "bad.dlg").Errors, Does.Contain(expected));
    }

    [Test]
    public void Parse_StopsAtTheAnswerCap()
    {
        string answers = string.Concat(Enumerable.Range(0, DialogueFile.MaxAnswers + 2).Select(i => $"-> a{i}: ok\n"));
        DialogueFile file = DialogueFile.Parse("=== a\n~ ask q\nQ?\n" + answers, "big.dlg");

        Assert.That(file.Errors, Has.Some.Contains("more than"));
        Assert.That(file.Block("a")[0].Answers, Has.Count.EqualTo(DialogueFile.MaxAnswers));
    }

    [Test]
    public void PlayerLikes_IsTrueOnlyForASavedAnswerWord()
    {
        var errors = new List<string>();
        (_, DialogueRule rule) = DialogueRule.Split("Metalhead! [if player_likes Metal]", errors);
        DialogueCondition likes = rule.Conditions.Single();

        Assert.That(errors, Is.Empty);
        Assert.That((likes.Fact, likes.Value), Is.EqualTo(("player_likes", "metal")));
        Assert.That(new DialogueFacts { Likes = new HashSet<string> { "metal", "tea" } }.Holds(likes), Is.True);
        Assert.That(new DialogueFacts { Likes = new HashSet<string> { "ballads" } }.Holds(likes), Is.False);
        Assert.That(new DialogueFacts { Likes = new HashSet<string>() }.Holds(likes), Is.False);
        Assert.That(new DialogueFacts().Holds(likes), Is.False);
    }

    [TestCase("x [if player_likes]", "'player_likes' needs a name, for example [if player_likes camelot city]")]
    [TestCase("x [if player_likes other]", "'player_likes' needs one answer word from an ask (not 'other'), for example [if player_likes metal]")]
    [TestCase("x [if player_likes heavy metal]", "'player_likes' needs one answer word from an ask (not 'other'), for example [if player_likes metal]")]
    [TestCase("x [if not player_likes metal]", "'not' only works with player_dead, in_combat")]
    public void PlayerLikes_RejectsBadUse(string line, string expected)
    {
        var errors = new List<string>();
        DialogueRule.Split(line, errors);

        Assert.That(errors, Does.Contain(expected));
    }

    [Test]
    public void Preview_PlayerLikesWordSetsTheFact()
    {
        var errors = new List<string>();
        DialogueFacts facts = DialoguePreview.Facts(["player_likes=Metal"], errors);

        Assert.That(errors, Is.Empty);
        Assert.That(facts.Likes, Is.EquivalentTo(new[] { "metal" }));
    }

    [Test]
    public void Check_PersoperMayAsk_NamedBotsNotYet()
    {
        Assert.That(DialogueCheck.Problems(DialogueFile.Parse(Good, "persoper.dlg"), "persoper", null), Is.Empty);

        List<string> problems = DialogueCheck.Problems(DialogueFile.Parse(Good.Replace("persoper", "kobaldjovi"), "kobaldjovi.dlg"), "kobaldjovi", null);
        Assert.That(problems, Has.Some.Contains("only Persoper asks questions for now"));
    }
}
