using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaocServer.Admin;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Dialogue step 3: files from the release folder, the built-in fallback, reload, the checker and preview.</summary>
[TestFixture, NonParallelizable]
public class UT_DialogueFolder
{
    private string _folder;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "dialogue-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        DialoguePicker.Shared.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        DialogueLibrary.Initialize(null);
        DialoguePicker.Shared.Clear();
        Directory.Delete(_folder, true);
    }

    private static string BuiltInText(string name)
    {
        var assembly = typeof(DialogueLibrary).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(resource => resource.EndsWith($".{name}.dlg", StringComparison.Ordinal)));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Kobaldjovi's file with his thank-you replies swapped for one new line.</summary>
    private static string KobaldjoviWithNewThanks() =>
        Regex.Replace(BuiltInText("kobaldjovi"), @"(~ reply thank\r?\n)(?:[^~\r\n][^\r\n]*\r?\n)+", "$1Folder thanks, {0}!\n");

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_folder, name + ".dlg"), text);

    private static string KobaldjoviThanks() => KobaldjoviIdentity.Personality.Reply(eAutonomousChatIntent.Thank, "Tester", true, new Random(1));

    [Test]
    public void FolderFile_IsUsed_AndTheBotAnswersWithIt()
    {
        Write("kobaldjovi", KobaldjoviWithNewThanks());
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.Errors, Is.Empty);
        Assert.That(report.FromFolder, Is.EqualTo(new[] { "kobaldjovi" }));
        Assert.That(report.BuiltIn, Does.Contain("persoper").And.Not.Contain("kobaldjovi"));
        Assert.That(KobaldjoviThanks(), Is.EqualTo("Folder thanks, Tester!"));
        Assert.That(KobaldjoviIdentity.Personality.Nicknames, Is.EqualTo(new[] { "Jovi", "KJ" }));
    }

    [Test]
    public void Reload_PicksUpAnEdit_WithoutARestart()
    {
        Write("kobaldjovi", BuiltInText("kobaldjovi"));
        int before = DialogueLibrary.Initialize(_folder).Version;
        Assert.That(KobaldjoviThanks(), Is.Not.EqualTo("Folder thanks, Tester!"));

        Write("kobaldjovi", KobaldjoviWithNewThanks());
        DialogueLoadReport report = DialogueLibrary.Reload();

        Assert.That(report.Version, Is.EqualTo(before + 1));
        Assert.That(KobaldjoviThanks(), Is.EqualTo("Folder thanks, Tester!"));
    }

    [Test]
    public void BrokenFile_IsReported_AndTheBuiltInCopyStays()
    {
        Write("kobaldjovi", KobaldjoviWithNewThanks() + "\n~ reply nonsense\nOops.\n");
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.FromFolder, Is.Empty);
        Assert.That(report.Errors, Has.Some.Match(@"^kobaldjovi\.dlg:\d+: unknown intent 'nonsense'$"));
        Assert.That(report.Errors, Does.Contain("kobaldjovi.dlg: not used; the built-in copy stays"));
        string[] builtIn = DialogueLibrary.BuiltIn["kobaldjovi"].Block("kobaldjovi").Single(section => section.Name == "reply" && section.Argument == "thank").Lines.ToArray();
        Assert.That(builtIn.Select(line => string.Format(line, "Tester")), Does.Contain(KobaldjoviThanks()));
    }

    [Test]
    public void FileThatLostASection_IsRejected()
    {
        Write("kobaldjovi", Regex.Replace(BuiltInText("kobaldjovi"), @"^~ song\r?$", "~ sonng", RegexOptions.Multiline));
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.FromFolder, Is.Empty);
        Assert.That(report.Errors, Does.Contain("kobaldjovi.dlg: '=== kobaldjovi' has no '~ song' (the built-in file has it)"));
    }

    [Test]
    public void IntentPhrase_AddedInTheFolder_IsUsed()
    {
        Assert.That(AutonomousChatIntentModel.Predict("grazie KJ"), Is.Not.EqualTo(eAutonomousChatIntent.Thank));
        Write("intents", BuiltInText("intents").Replace("~ intent thank\n", "~ intent thank\ngrazie\n"));
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.Errors, Is.Empty);
        Assert.That(AutonomousChatIntentModel.Predict("grazie KJ"), Is.EqualTo(eAutonomousChatIntent.Thank));
        DialogueLibrary.Initialize(null);
        Assert.That(AutonomousChatIntentModel.Predict("grazie KJ"), Is.Not.EqualTo(eAutonomousChatIntent.Thank));
    }

    [Test]
    public void TypeFile_Edit_ReachesTheTypeAndItsNamedBots()
    {
        const string line = "A brand new joker fallback, {0}.";
        Write("joker", new Regex(@"~ fallback\r?\n").Replace(BuiltInText("joker"), "~ fallback\n" + line + "\n", 1));
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.Errors, Is.Empty);
        Assert.That(PersonalityTypes.Templates["joker"].FallbackReplies, Does.Contain(line));
        NamedBotLineup.Entry named = NamedBotLineup.All.First(entry => entry.TypeKey == "joker");
        Assert.That(PersonalityTypes.Named(named).FallbackReplies, Does.Contain(line));
        Assert.That(PersonalityLines.Errors, Is.Empty);
    }

    [Test]
    public void MissingFolder_UsesTheBuiltInFiles()
    {
        DialogueLoadReport report = DialogueLibrary.Initialize(Path.Combine(_folder, "nothing-here"));

        Assert.That(report.FromFolder, Is.Empty);
        Assert.That(report.BuiltIn, Has.Count.EqualTo(DialogueLibrary.BuiltIn.Count));
        Assert.That(report.Errors.Single(), Does.EndWith("not found; using the built-in files"));
        Assert.That(KobaldjoviThanks(), Is.Not.Empty);
    }

    [Test]
    public void Checker_FindsNothingWrongWithTheBuiltInFiles()
    {
        foreach ((string name, DialogueFile file) in DialogueLibrary.BuiltIn)
            Assert.That(DialogueCheck.Problems(file, name, file), Is.Empty, name);
    }

    [TestCase("joker", "=== joker\n~ fallback\nHi.\n~ reply nonsense\nX.\n", "joker.dlg:4: unknown intent 'nonsense'")]
    [TestCase("joker", "=== joker\n~ fallback\nHi.\n~ wobble\nX.\n", "joker.dlg:4: unknown section 'wobble'")]
    [TestCase("intents", "=== intents\n", "intents.dlg: no phrases")]
    [TestCase("persoper", "=== someone\n~ fallback\nHi.\n", "persoper.dlg: no block '=== persoper'")]
    public void Checker_ReportsWhatTheCodeRejects(string name, string text, string problem)
    {
        DialogueFile file = DialogueFile.Parse(text, name + ".dlg");
        Assert.That(DialogueCheck.Problems(file, name, null), Does.Contain(problem));
    }

    private static string VerdictOf(string[] facts, string line)
    {
        var errors = new System.Collections.Generic.List<string>();
        DialogueFacts situation = DialoguePreview.Facts(facts, errors);
        Assert.That(errors, Is.Empty);
        (string pool, var lines) = DialoguePreview.Reply(DialoguePreview.Find("persoper"), eAutonomousChatIntent.Help, situation, cheeky: false);
        Assert.That(pool, Is.EqualTo("reply help"));
        return lines.Single(found => found.Text.StartsWith(line, StringComparison.Ordinal)).Verdict;
    }

    [Test]
    public void Preview_FollowsTheConditions_AndMostSpecificWins()
    {
        Assert.That(VerdictOf([], "Hold still"), Is.EqualTo("not now: [if player_dead]"));
        Assert.That(VerdictOf(["player_dead"], "Hold still"), Is.EqualTo(DialoguePreview.CanSay));
        Assert.That(VerdictOf(["player_dead", "deaths_today=3"], "Hold still"), Is.EqualTo(DialoguePreview.Beaten));
        Assert.That(VerdictOf(["player_dead", "deaths_today=3"], "Dead again"), Is.EqualTo(DialoguePreview.CanSay));
    }

    [TestCase("health=abc", "'health=abc': health needs a number")]
    [TestCase("mood=angry", "'mood=angry': mood is one of")]
    [TestCase("player_dead=maybe", "'player_dead=maybe': player_dead is yes or no")]
    [TestCase("bogus", "'bogus': unknown fact 'bogus'")]
    public void Preview_RejectsUnknownFacts(string word, string error)
    {
        var errors = new System.Collections.Generic.List<string>();
        DialoguePreview.Facts([word], errors);
        Assert.That(errors.Single(), Does.StartWith(error));
    }

    [TestCase("KJ", "kobaldjovi")]
    [TestCase("Persoper", "persoper")]
    [TestCase("joker", "joker")]
    public void Preview_FindsBotsByNameNicknameOrType(string name, string key)
    {
        Assert.That(DialoguePreview.Find(name)?.Key, Is.EqualTo(key));
    }

    [Test]
    public void Preview_FindsANamedBot()
    {
        NamedBotLineup.Entry named = NamedBotLineup.All.First();
        Assert.That(DialoguePreview.Find(named.Name)?.Key, Is.EqualTo(PersonalityLines.NamedKey(named)));
        Assert.That(DialoguePreview.Find("nobody-at-all"), Is.Null);
    }

    [TestCase("dialogue", AdminOps.DialogueStatus)]
    [TestCase("dialogue status", AdminOps.DialogueStatus)]
    [TestCase("dialogue reload", AdminOps.DialogueReload)]
    public void AdminCommand_Dialogue(string line, string op)
    {
        Assert.That(AdminCommandLine.Parse(AdminCommandLine.SplitLine(line)).Op, Is.EqualTo(op));
    }
}
