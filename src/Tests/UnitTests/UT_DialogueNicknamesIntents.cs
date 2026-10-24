using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_DialogueNicknamesIntents
{
    [Test]
    public void Names_AreReadFromTheBlock()
    {
        DialogueFile file = DialogueFile.Parse("=== kj\nnames: Jovi, KJ\n~ ding\nDing!\n=== other\n~ ding\nDong!\n", "names.dlg");
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Names("kj"), Is.EqualTo(new[] { "Jovi", "KJ" }));
        Assert.That(file.Names("other"), Is.Empty);
        Assert.That(file.Names("missing"), Is.Empty);
    }

    [TestCase("=== a\nnames: Jovi\nnames: KJ\n~ ding\nx\n", "names.dlg:3: 'names:' comes once, right after the '=== block' line")]
    [TestCase("=== a\n~ ding\nx\n~\nnames: KJ\n", "names.dlg:5: 'names:' comes once, right after the '=== block' line")]
    [TestCase("=== a\nnames: J\n~ ding\nx\n", "names.dlg:2: bad name 'J' (2 to 24 letters or digits, starting with a letter)")]
    [TestCase("=== a\nnames: Jo vi\n~ ding\nx\n", "names.dlg:2: bad name 'Jo vi' (2 to 24 letters or digits, starting with a letter)")]
    [TestCase("=== a\nnames: Jovi, jovi\n~ ding\nx\n", "names.dlg:2: name 'jovi' appears twice")]
    [TestCase("=== a\nnames: ,\n~ ding\nx\n", "names.dlg:2: 'names:' has no names")]
    public void Names_Mistakes_AreReported(string text, string error)
    {
        Assert.That(DialogueFile.Parse(text, "names.dlg").Errors, Does.Contain(error));
    }

    [Test]
    public void Names_AreCapped()
    {
        string names = string.Join(", ", Enumerable.Range(0, DialogueFile.MaxNames + 3).Select(i => "Nick" + i));
        DialogueFile file = DialogueFile.Parse($"=== a\nnames: {names}\n~ ding\nx\n", "names.dlg");
        Assert.That(file.Names("a"), Has.Count.EqualTo(DialogueFile.MaxNames));
        Assert.That(file.Errors, Has.Count.EqualTo(1));
    }

    [Test]
    public void Kobaldjovi_HasHisNicknames()
    {
        Assert.That(KobaldjoviIdentity.Personality.Nicknames, Is.EqualTo(new[] { "Jovi", "KJ" }));
        Assert.That(PersoperIdentity.Personality.Nicknames, Is.Empty);
    }

    [TestCase("Jovi, thanks!", "Jovi", true)]
    [TestCase("kj hail", "KJ", true)]
    [TestCase("Kobaldjovi, thanks!", "Jovi", false)]
    [TestCase("Jovial crowd tonight", "Jovi", false)]
    public void Nickname_CountsAsAMention(string message, string nickname, bool expected)
    {
        Assert.That(AutonomousBotChatCoordinator.ContainsMention(message, nickname), Is.EqualTo(expected));
    }

    [TestCase("jovi", true)]
    [TestCase(" KJ ", true)]
    [TestCase("Jo", false)]
    public void TellName_MatchesANickname(string typed, bool expected)
    {
        Assert.That(AutonomousBotChatCoordinator.IsPersonalityName("Kobaldjovi", "Kobaldjovi", typed, KobaldjoviIdentity.Personality.Nicknames),
            Is.EqualTo(expected));
    }

    [Test]
    public void IntentFile_HasNoMistakes()
    {
        Assert.That(IntentPhrases.Shared.Errors, Is.Empty);
        Assert.That(IntentPhrases.Shared.Count, Is.GreaterThan(0));
    }

    [TestCase("Jovi, thanks!", eAutonomousChatIntent.Thank)]
    [TestCase("ty KJ", eAutonomousChatIntent.Thank)]
    [TestCase("thank you so much persoper", eAutonomousChatIntent.Thank)]
    [TestCase("really appreciate it", eAutonomousChatIntent.Thank)]
    [TestCase("thanks for the invite", eAutonomousChatIntent.Thank)]
    [TestCase("thanks, idiot", eAutonomousChatIntent.Abuse)]
    [TestCase("KJ, hail", eAutonomousChatIntent.Greeting)]
    public void IntentModel_UsesTheIntentFile(string text, eAutonomousChatIntent expected)
    {
        Assert.That(AutonomousChatIntentModel.Predict(text), Is.EqualTo(expected));
    }

    [TestCase("thanksgiving is coming")]
    [TestCase("tackle the target")]
    public void IntentModel_MatchesWholeWordsOnly(string text)
    {
        Assert.That(AutonomousChatIntentModel.Predict(text), Is.Not.EqualTo(eAutonomousChatIntent.Thank));
    }

    [Test]
    public void Phrases_LongestWinsThenFileOrder()
    {
        IntentPhrases phrases = IntentPhrases.From(DialogueFile.Parse(
            "=== intents\n~ intent farewell\ngood, see you later\n~ intent greeting\ngood day, hi\n", "intents.dlg"));
        Assert.That(phrases.Errors, Is.Empty);
        Assert.That(phrases.Match("good day to you"), Is.EqualTo(eAutonomousChatIntent.Greeting));
        Assert.That(phrases.Match("see you later, good luck"), Is.EqualTo(eAutonomousChatIntent.Farewell));
        Assert.That(phrases.Match("good"), Is.EqualTo(eAutonomousChatIntent.Farewell));
        Assert.That(phrases.Match("day good"), Is.EqualTo(eAutonomousChatIntent.Farewell));
        Assert.That(phrases.Match("nothing here"), Is.Null);
        Assert.That(phrases.Match(""), Is.Null);
    }

    [TestCase("=== intents\n~ intent dancing\nboogie\n", "intents.dlg:2: unknown intent 'dancing'")]
    [TestCase("=== intents\n~ intent unknown\nhuh\n", "intents.dlg:2: unknown intent 'unknown'")]
    [TestCase("=== intents\n~ reply thank\nthanks\n", "intents.dlg:2: only '~ intent <name>' sections belong here")]
    [TestCase("=== intents\n~ intent thank\nthanks, a\n", "intents.dlg:2: phrase 'a' needs 1 to 6 words of two letters or more")]
    [TestCase("=== intents\n~ intent thank\none two three four five six seven\n", "intents.dlg:2: phrase 'one two three four five six seven' needs 1 to 6 words of two letters or more")]
    public void Phrases_Mistakes_AreReported(string text, string error)
    {
        Assert.That(IntentPhrases.From(DialogueFile.Parse(text, "intents.dlg")).Errors, Does.Contain(error));
    }

    [Test]
    public void Thank_HasItsOwnRepliesForBothNamedBots()
    {
        foreach (BotPersonality personality in new[] { KobaldjoviIdentity.Personality, PersoperIdentity.Personality })
        {
            string[] lines = personality.Replies[eAutonomousChatIntent.Thank];
            Assert.That(lines, Is.Not.Empty, personality.Name);
            var random = new Random(3);
            for (int i = 0; i < 20; i++)
                Assert.That(lines.Select(line => string.Format(line, "Tester")),
                    Does.Contain(personality.Reply(eAutonomousChatIntent.Thank, "Tester", true, random)), personality.Name);
        }
    }
}
