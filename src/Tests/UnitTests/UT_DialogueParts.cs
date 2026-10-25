using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_DialogueParts
{
    private const string Greeting =
        "=== a\n~ part greet\nHail\nWell met\nOi\n\n~ reply greeting\n{greet}, {0}!\nPlain hello, {0}.\n";

    [Test]
    public void Parse_FillsAPartInOneLinePerAlternative()
    {
        DialogueFile file = DialogueFile.Parse(Greeting, "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("a").Single().Lines, Is.EqualTo(new[] { "Hail, {0}!", "Well met, {0}!", "Oi, {0}!", "Plain hello, {0}." }));
        Assert.That(file.Parts("a")["greet"], Is.EqualTo(new[] { "Hail", "Well met", "Oi" }));
    }

    [Test]
    public void Parse_PartSectionsAreNotOrdinarySections()
    {
        DialogueFile file = DialogueFile.Parse(Greeting, "a.dlg");

        Assert.That(file.Block("a").Select(section => section.Name), Is.EqualTo(new[] { "reply" }));
    }

    [Test]
    public void Parse_EveryFilledInLineKeepsTheTags()
    {
        DialogueFile file = DialogueFile.Parse(
            "=== a\n~ part greet\nHail\nOi\n\n~ reply greeting\n{greet}, {0}! [if player_dead] [once]\n", "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        foreach (string line in new[] { "Hail, {0}!", "Oi, {0}!" })
        {
            DialogueRule rule = file.RuleFor(line);
            Assert.That(rule.Conditions.Single().Fact, Is.EqualTo("player_dead"));
            Assert.That(rule.Once, Is.True);
        }
    }

    [Test]
    public void Parse_TwoPartsInALineGiveEveryPairing()
    {
        DialogueFile file = DialogueFile.Parse(
            "=== a\n~ part greet\nHail\nOi\n~ part mood\nfriend\nfoe\n~ reply greeting\n{greet}, {mood}!\n", "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("a").Single().Lines, Is.EquivalentTo(new[] { "Hail, friend!", "Hail, foe!", "Oi, friend!", "Oi, foe!" }));
    }

    [Test]
    public void Parse_APartMayUseAnotherPart()
    {
        DialogueFile file = DialogueFile.Parse(
            "=== a\n~ part name\nfriend\nfoe\n~ part greet\nHail, {name}\nOi\n~ reply greeting\n{greet}!\n", "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Parts("a")["greet"], Is.EqualTo(new[] { "Hail, friend", "Hail, foe", "Oi" }));
        Assert.That(file.Block("a").Single().Lines, Is.EqualTo(new[] { "Hail, friend!", "Hail, foe!", "Oi!" }));
    }

    [Test]
    public void Parse_TheGamesOwnSlotsAreLeftAlone()
    {
        DialogueFile file = DialogueFile.Parse("=== a\n~ part greet\nHail\n~ reply greeting\n{greet} from {zone}, {0}, {a} {b} {level} {monster} {item} {class}!\n", "a.dlg");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("a").Single().Lines, Is.EqualTo(new[] { "Hail from {zone}, {0}, {a} {b} {level} {monster} {item} {class}!" }));
    }

    [TestCase("=== a\n~ reply greeting\n{greet}, {0}!\n", "bad.dlg:3: unknown part '{greet}' (a part is defined with '~ part greet' earlier in the same block)")]
    [TestCase("=== a\n~ reply greeting\n{greet}, {0}!\n~ part greet\nHail\n", "bad.dlg:3: unknown part '{greet}' (a part is defined with '~ part greet' earlier in the same block)")]
    [TestCase("=== a\n~ part greet\n{greet} again\n", "bad.dlg:3: unknown part '{greet}' (a part is defined with '~ part greet' earlier in the same block)")]
    [TestCase("=== a\n~ part zone\nx\n", "bad.dlg:2: '{zone}' is filled in by the game; call the part something else")]
    [TestCase("=== a\n~ part\nx\n", "bad.dlg:2: a part needs a name of 2 to 24 lower-case letters or _, for example '~ part greet'")]
    [TestCase("=== a\n~ part greet\n", "bad.dlg:2: part 'greet' has no lines")]
    [TestCase("=== a\n~ part greet\nHail\n~ part greet\nOi\n", "bad.dlg:4: part 'greet' appears twice in this block")]
    [TestCase("=== a\n~ part greet\nHail\nHail\n", "bad.dlg:2: part 'greet' has the same alternative twice")]
    [TestCase("=== a\n~ part greet [if tone_down off]\nHail\n", "bad.dlg:2: a part has no condition")]
    [TestCase("=== a\n~ part greet\nHail [once]\n", "bad.dlg:3: a part's alternatives can't carry tags; put them on the lines that use the part")]
    [TestCase("=== a\n~ part greet\nHail\n~ ask q\n{greet}?\n-> other: ok\n", "bad.dlg:5: part '{greet}' can't be used in an ask yet")]
    public void Parse_ReportsPartProblemsWithFileAndRow(string text, string expected)
    {
        Assert.That(DialogueFile.Parse(text, "bad.dlg").Errors, Does.Contain(expected));
    }

    [Test]
    public void Parse_DropsALineWithAnUnknownPartSoNothingBreaksAtRuntime()
    {
        DialogueFile file = DialogueFile.Parse("=== a\n~ reply greeting\n{nope}, {0}!\nFine.\n", "bad.dlg");

        Assert.That(file.Errors, Has.Count.EqualTo(1));
        Assert.That(file.Block("a").Single().Lines, Is.EqualTo(new[] { "Fine." }));
    }

    [Test]
    public void Parse_StopsAtThePartsPerLineCap()
    {
        string parts = "~ part aa\nx\n~ part bb\nx\n~ part cc\nx\n~ part dd\nx\n";
        DialogueFile ok = DialogueFile.Parse("=== a\n" + parts + "~ reply r\n{aa}{bb}{cc}\n", "a.dlg");
        DialogueFile tooMany = DialogueFile.Parse("=== a\n" + parts + "~ reply r\n{aa}{bb}{cc}{dd}\n", "a.dlg");

        Assert.That(ok.Errors, Is.Empty);
        Assert.That(tooMany.Errors, Has.Some.Contains($"4 parts in one line (at most {DialogueFile.MaxPartsPerLine})"));
    }

    [Test]
    public void Parse_StopsAtTheNestingCap()
    {
        string text = "=== a\n~ part one\nx\n~ part two\n{one}\n~ part three\n{two}\n~ reply r\n{three}\n";
        DialogueFile file = DialogueFile.Parse(text, "a.dlg");

        Assert.That(file.Errors, Has.Some.Contains("part 'three' uses parts"));
        Assert.That(file.Parts("a").Keys, Is.EquivalentTo(new[] { "one", "two" }));
        Assert.That(file.Errors, Has.Some.Contains("unknown part '{three}'"));
    }

    [Test]
    public void Parse_StopsAtThePartsPerBlockCap()
    {
        string parts = string.Concat(Enumerable.Range(0, DialogueFile.MaxParts + 1).Select(i => $"~ part p{(char)('a' + i)}x\nx\n"));
        DialogueFile file = DialogueFile.Parse("=== a\n" + parts, "big.dlg");

        Assert.That(file.Errors, Has.Some.Contains($"more than {DialogueFile.MaxParts} parts in a block"));
        Assert.That(file.Parts("a"), Has.Count.EqualTo(DialogueFile.MaxParts));
    }

    [Test]
    public void Parse_StopsAtTheAlternativesCap()
    {
        string alternatives = string.Concat(Enumerable.Range(0, DialogueFile.MaxAlternatives + 1).Select(i => $"word{(char)('a' + i)}\n"));
        DialogueFile file = DialogueFile.Parse("=== a\n~ part big\n" + alternatives, "big.dlg");

        Assert.That(file.Errors, Has.Some.Contains($"at most {DialogueFile.MaxAlternatives}"));
        Assert.That(file.Parts("a"), Is.Empty);
    }

    [Test]
    public void Parse_StopsAtTheFilledInLinesCap()
    {
        string alternatives = string.Concat(Enumerable.Range(0, 6).Select(i => $"w{(char)('a' + i)}\n"));
        string text = $"=== a\n~ part pp\n{alternatives}~ part qq\n{alternatives}~ reply r\n{{pp}} {{qq}}\n";
        DialogueFile file = DialogueFile.Parse(text, "big.dlg");

        Assert.That(file.Errors, Has.Some.Contains($"this line turns into 36 lines once its parts are filled in (at most {DialogueFile.MaxVariants})"));
        Assert.That(file.Block("a").Single().Lines, Is.Empty);
    }

    [Test]
    public void Picker_PicksAFilledInLineAndVariesItsWord()
    {
        DialogueFile file = DialogueFile.Parse(Greeting, "a.dlg");
        var picker = new DialoguePicker();
        var said = new System.Collections.Generic.HashSet<string>();
        var random = new System.Random(7);
        for (int i = 0; i < 60; i++)
            said.Add(picker.Pick(file.Block("a").Single().Lines, file.RuleFor, null, "bot", System.DateTime.UtcNow, random));

        Assert.That(said, Is.EquivalentTo(new[] { "Hail, {0}!", "Well met, {0}!", "Oi, {0}!", "Plain hello, {0}." }));
        Assert.That(said, Has.None.Contain("{greet}"));
    }

    [Test]
    public void BuiltInFiles_HaveNoProblemsAndPersoperGreetsWithAPart()
    {
        foreach ((string name, DialogueFile file) in DialogueLibrary.BuiltIn)
            Assert.That(file.Errors, Is.Empty, name);

        DialogueFile persoper = DialogueLibrary.BuiltIn["persoper"];
        Assert.That(persoper.Parts("persoper").ContainsKey("greet"), Is.True);
        Assert.That(persoper.Block("persoper").SelectMany(section => section.Lines), Has.None.Contain("{greet}"));
        Assert.That(persoper.Block("persoper").Where(section => section.Name == "reply" && section.Argument == "greeting")
            .SelectMany(section => section.Lines).Count(line => line.Contains("metalhead")), Is.EqualTo(persoper.Parts("persoper")["greet"].Count));
    }
}
