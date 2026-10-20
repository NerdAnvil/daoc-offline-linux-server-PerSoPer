using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_DialogueFile
{
    [Test]
    public void Parse_ReadsBlocksSectionsLinesAndConditions()
    {
        DialogueFile file = DialogueFile.Parse(
            "# comment\n=== joker\n\n~ reply greeting\nHi {0}!\n  Hello, {0}.  \n~ cheeky [if tone_down off]\nCheeky.\n=== shnacksize\n~ ambient\n\\# not a comment\n",
            "test.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.BlockNames, Is.EquivalentTo(new[] { "joker", "shnacksize" }));
        IReadOnlyList<DialogueSection> joker = file.Block("joker");
        Assert.That(joker.Select(s => (s.Name, s.Argument, s.Condition)),
            Is.EqualTo(new[] { ("reply", "greeting", (string)null), ("cheeky", null, "tone_down off") }));
        Assert.That(joker[0].Lines, Is.EqualTo(new[] { "Hi {0}!", "Hello, {0}." }));
        Assert.That(file.Block("shnacksize")[0].Lines, Is.EqualTo(new[] { "# not a comment" }));
        Assert.That(file.LineCount, Is.EqualTo(4));
        Assert.That(file.Block("missing"), Is.Empty);
    }

    [Test]
    public void Parse_CrLfFile_ReadsTheSame()
    {
        DialogueFile file = DialogueFile.Parse("=== a\r\n~ ding\r\nDing!\r\n", "crlf.dlg");
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("a")[0].Lines, Is.EqualTo(new[] { "Ding!" }));
    }

    [TestCase("Orphan line\n", "crash.dlg:1: line outside a section")]
    [TestCase("~ ding\nDing\n", "crash.dlg:1: section before any '=== block'")]
    [TestCase("=== a\n~ ding\n", "crash.dlg:2: section 'ding' has no lines")]
    [TestCase("=== a\n~ ding [if raining]\nx\n", "crash.dlg:2: unknown condition 'raining'")]
    [TestCase("=== a\n~ ding\nx\n~ ding\ny\n", "crash.dlg:4: section 'ding ' appears twice in this block")]
    [TestCase("=== a\n~ ding\nx\n=== a\n~ drop\ny\n", "crash.dlg:4: block 'a' appears twice")]
    [TestCase("=== Bad Name\n", "crash.dlg:1: bad block name 'Bad Name'")]
    [TestCase("=== a\n~ 1bad!\n", "crash.dlg:2: bad section header '~ 1bad!'")]
    [TestCase("=== a\n~ ding\n= half a block\n", "crash.dlg:3: '=' starts a row but it isn't '=== block' (use \\= for text)")]
    public void Parse_ReportsProblemsWithFileAndRow(string text, string expected)
    {
        Assert.That(DialogueFile.Parse(text, "crash.dlg").Errors, Does.Contain(expected));
    }

    [Test]
    public void Parse_StopsAtTheRowCap()
    {
        string text = "=== a\n~ ding\n" + string.Concat(Enumerable.Repeat("x\n", DialogueFile.MaxRows + 10));
        DialogueFile file = DialogueFile.Parse(text, "big.dlg");
        Assert.That(file.LineCount, Is.EqualTo(DialogueFile.MaxRows - 2));
        Assert.That(file.Errors, Has.Some.Contains("more than"));
    }

    [Test]
    public void BuiltInFiles_LoadWithoutErrors()
    {
        Assert.That(DialogueLibrary.All.Keys, Is.SupersetOf(PersonalityLines.TypeKeys));
        Assert.That(DialogueLibrary.All.Values.SelectMany(file => file.Errors), Is.Empty);
        Assert.That(PersonalityLines.Errors, Is.Empty);
    }

    // The six types had these many lines in C# before the move to dialogue files (step 3b).
    [TestCase("hardcore", 230)]
    [TestCase("casual", 231)]
    [TestCase("minmaxer", 230)]
    [TestCase("joker", 244)]
    [TestCase("loner", 228)]
    [TestCase("leeroy", 243)]
    public void TypeFiles_KeepEveryLine(string key, int lines)
    {
        Assert.That(DialogueLibrary.For(key).Block(key).Sum(section => section.Lines.Count), Is.EqualTo(lines));
    }

    [Test]
    public void TypeVoices_TakeTheirTalkFromTheFiles()
    {
        foreach (string key in PersonalityLines.TypeKeys)
        {
            BotPersonality voice = PersonalityTypeVoices.All[key];
            Assert.That(voice.Ambient, Is.Not.Empty, key);
            Assert.That(voice.FallbackReplies, Is.Not.Empty, key);
            Assert.That(voice.Replies.Keys, Does.Contain(eAutonomousChatIntent.Greeting), key);
            Assert.That(voice.CheekyReplies != null, Is.EqualTo(key is "joker" or "leeroy"), key);
        }
    }
}
