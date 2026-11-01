using System;
using System.Collections.Generic;
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
    public void BotToBotReply_UsesNearbyLines_WhenTheBotIsClose()
    {
        DialogueLibrary.Initialize(null);
        string Joke(params string[] nearby) => PersoperIdentity.Personality.Reply(eAutonomousChatIntent.Joke, "Spyburd", true, new Random(1),
            DialogueFactSource.ForBot("persoper", false, 2, "day", "Camelot Hills", new HashSet<string>(nearby, StringComparer.OrdinalIgnoreCase)));

        Assert.That(Joke("spyburd"), Does.Not.Contain("Shnacksize"), "no groan without Shnacksize");
        Assert.That(Joke("spyburd", "shnacksize"), Does.Contain("Shnacksize"), "a groan when she is close");
    }

    [Test]
    public void BotToBotReply_ConditionalLineBeatsTheTopicAnswer_EvenWhenSheTellsTheJoke()
    {
        DialogueLibrary.Initialize(null);
        DialogueFacts Facts(params string[] nearby) =>
            DialogueFactSource.ForBot("persoper", false, 2, "day", "Camelot Hills", new HashSet<string>(nearby, StringComparer.OrdinalIgnoreCase));

        Assert.That(PersoperIdentity.Personality.ConditionalReply(eAutonomousChatIntent.Joke, "Shnacksize", new Random(1), Facts("spyburd")), Is.Null);
        Assert.That(PersoperIdentity.Personality.ConditionalReply(eAutonomousChatIntent.Joke, "Shnacksize", new Random(1), Facts("shnacksize")),
            Does.StartWith("Oh, Shnacksize, that pun hurt"), "the groan that doesn't name her as someone else");
    }

    [TestCase("Persoper")]
    [TestCase("Kobaldjovi")]
    [TestCase("Spyburd")]
    [TestCase("Eaglefang")]
    public void Groaners_HaveALineForShnacksizesOwnJokes(string name)
    {
        // Lines that name the speaker are dropped when she is the one joking, so each needs one that doesn't.
        DialogueLibrary.Initialize(null);
        BotPersonality personality = DialoguePreview.Find(name);
        DialogueFacts Facts(params string[] nearby) =>
            DialogueFactSource.ForBot(name, false, 6, "day", "Jordheim", new HashSet<string>(nearby, StringComparer.OrdinalIgnoreCase));

        Assert.That(personality, Is.Not.Null);
        Assert.That(personality.ConditionalReply(eAutonomousChatIntent.Joke, "Shnacksize", new Random(1), Facts("shnacksize")), Is.Not.Null,
            $"{name} groans at her own joke");
        Assert.That(personality.ConditionalReply(eAutonomousChatIntent.Joke, "Shempenstein", new Random(1), Facts("shempenstein")), Is.Null,
            $"{name} doesn't groan when she isn't there");
    }

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
    public void CharacterFile_GivesAllThreeRealms_TheCharacterPlusTheirRealmFlavour()
    {
        // The built-in file plus one line in the character block and one in the Albion block.
        string text = Regex.Replace(BuiltInText("eaglefang"), @"(=== eaglefang\r?\n(?:\r?\n)*~ ambient\r?\n)", "$1Character line.\n");
        Write("eaglefang", Regex.Replace(text, @"(=== albion\r?\n~ ambient\r?\n)", "$1Albion line.\n"));
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.Errors, Is.Empty);
        Assert.That(report.FromFolder, Is.EqualTo(new[] { "eaglefang" }));
        Assert.That(PersonalityLines.Errors, Is.Empty);
        Assert.That(PersonalityLines.Own["named:eaglefang"].Talk.Ambient, Does.Contain("Character line.").And.No.Contain("Albion line."));
        Assert.That(PersonalityLines.Own["named:eaglefange"].Talk.Ambient, Does.Contain("Character line.").And.Contain("Albion line."));
        Assert.That(PersonalityLines.Own["named:eaglefangh"].Talk.Ambient, Does.Contain("Character line.").And.No.Contain("Albion line."));
        // Still mixed with the Gruff Doc type's lines.
        string[] mixed = PersonalityLines.NamedTalk["named:eaglefang"].Ambient;
        Assert.That(mixed, Does.Contain("Character line.").And.Contain("Sit down. Drink water. Doctor's orders."));
        // Other characters are unaffected.
        Assert.That(PersonalityLines.Own["named:spyburd"].Talk.Ambient, Is.Not.Empty);
    }

    // Bug 2026-10-31: "~ song_reaction" was an unknown section in type and character files, and the merge dropped it.
    [Test]
    public void SongReactions_NamedBotUsesItsOwn_AndKeepsEveryOtherMergedPool()
    {
        Write("gruffdoc", Regex.Replace(BuiltInText("gruffdoc"), @"(=== gruffdoc\r?\n)", "$1~ song_reaction\nType song line.\n\n"));
        string text = Regex.Replace(BuiltInText("eaglefang"), @"(=== eaglefang\r?\n)", "$1~ song_reaction\nCharacter song line.\n\n");
        Write("eaglefang", Regex.Replace(text, @"(=== albion\r?\n)", "$1~ song_reaction\nAlbion song line.\n\n"));
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.Errors, Is.Empty);
        Assert.That(PersonalityLines.Errors, Is.Empty);
        Assert.That(PersonalityLines.TalkFor("gruffdoc").SongReactions, Is.EqualTo(new[] { "Type song line." }));
        Assert.That(PersonalityLines.NamedTalk["named:eaglefang"].SongReactions, Is.EqualTo(new[] { "Character song line." }), "own lines only, not the type's");
        Assert.That(PersonalityLines.NamedTalk["named:eaglefange"].SongReactions, Is.EqualTo(new[] { "Character song line.", "Albion song line." }));
        BotPersonality eaglefang = PersonalityTypes.Named(NamedBotLineup.All.Single(entry => entry.Key == "gruffdoc-mid"));
        Assert.That(KobaldjoviIdentity.SongReaction(eaglefang, new Random(1)), Is.EqualTo("Character song line."));

        // Every other pool the merge copies still holds the type's and the character's lines.
        TypeTalk type = PersonalityLines.TalkFor("gruffdoc"), own = PersonalityLines.Own["named:eaglefang"].Talk, merged = PersonalityLines.NamedTalk["named:eaglefang"];
        Assert.That(merged.Ambient, Is.SupersetOf(type.Ambient).And.SupersetOf(own.Ambient));
        Assert.That(merged.FallbackReplies, Is.SupersetOf(type.FallbackReplies).And.SupersetOf(own.FallbackReplies));
        foreach ((eAutonomousChatIntent intent, string[] lines) in type.Replies.Concat(own.Replies))
            Assert.That(merged.Replies[intent], Is.SupersetOf(lines), $"reply {intent}");
        foreach ((eAutonomousChatIntent intent, string[] lines) in (type.CheekyReplies ?? new Dictionary<eAutonomousChatIntent, string[]>()).Concat(own.CheekyReplies ?? new Dictionary<eAutonomousChatIntent, string[]>()))
            Assert.That(merged.CheekyReplies[intent], Is.SupersetOf(lines), $"cheeky_reply {intent}");
        Assert.That(merged.Nicknames, Is.EqualTo(own.Nicknames));
    }

    [Test]
    public void SongReactions_NamedBotWithoutItsOwn_UsesItsTypes_ElseThePlainLines()
    {
        Write("gruffdoc", Regex.Replace(BuiltInText("gruffdoc"), @"(=== gruffdoc\r?\n)", "$1~ song_reaction\nType song line.\n\n"));
        DialogueLibrary.Initialize(_folder);
        BotPersonality eaglefang = PersonalityTypes.Named(NamedBotLineup.All.Single(entry => entry.Key == "gruffdoc-mid"));
        Assert.That(eaglefang.SongReactions, Is.EqualTo(new[] { "Type song line." }));

        DialogueLibrary.Initialize(null);
        Assert.That(eaglefang.SongReactions, Is.Null.Or.Empty, "no lines anywhere: KobaldjoviIdentity uses its plain reactions");
    }

    [Test]
    public void CharacterFile_WithAnotherBlock_IsNotUsed()
    {
        Write("eaglefang", "=== eaglefang\n~ ambient\nCharacter line.\n\n=== spyburd\n~ ambient\nWrong bot.\n");
        DialogueLoadReport report = DialogueLibrary.Initialize(_folder);

        Assert.That(report.FromFolder, Is.Empty);
        Assert.That(report.Errors, Has.Some.Contains("block 'spyburd' is neither '=== eaglefang' nor a realm"));
        Assert.That(PersonalityLines.Own["named:eaglefang"].Talk.Ambient, Is.Not.Empty.And.No.EqualTo("Character line."));
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

    [TestCase("=== joker-joker\n~ short a\nA: 1\nB: 2\n", "scenes.dlg:2: '=== joker-joker': a pair is two different types")]
    [TestCase("=== nobody-any\n~ short a\nA: 1\nB: 2\n", "or a named character and 'any', like 'shempenstein-any'")]
    [TestCase("=== joker-any\n~ short a\nA: 1\nB: 2\n", "scenes.dlg:2: '=== joker-any': a pair is two different types")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nB: 2\n~ crowd laugh\nHa.\n", "crowd lines are '~ crowd laugh', '~ crowd grumble' or '~ crowd confused', in a character's block")]
    [TestCase("=== shempenstein-any\n~ short a\nA: 1\nB: 2\n~ crowd laugh\nHa.\n", "a crowd needs both '~ crowd laugh' and '~ crowd grumble' lines")]
    [TestCase("=== shempenstein-any\n~ short a\nA: 1\nB: 2\n~ crowd laugh\nHa, {b}.\n~ crowd grumble\nHmph.\n", "crowd lines only take {a}")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nB: 2\n=== loner-joker\n~ short a\nA: 1\nB: 2\n", "'=== loner-joker': this pair is already in the file the other way round")]
    [TestCase("=== joker-loner\n~ tiny a\nA: 1\nB: 2\n", "scenes.dlg:2: '~ tiny a': a scene is '~ short', '~ medium' or '~ large'")]
    [TestCase("=== joker-loner\n~ short\nA: 1\nB: 2\n", "'~ short': the scene needs a name")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nB: 2\n~ medium a\nA: 1\nB: 2\nA: 3\nB: 4\nA: 5\n", "scenes.dlg:5: '~ medium a': another scene in this pair has the name 'a'")]
    [TestCase("=== joker-loner\n~ short a [if tone_down on]\nA: 1\nB: 2\n", "scenes only take [if tone_down off]")]
    [TestCase("=== joker-loner\n~ medium a\nA: 1\nB: 2\n", "has 2 lines; a medium scene has 5 to 8")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nD: 2\n", "line 2 doesn't start with 'A:', 'B:' or 'C:' (D: 2)")]
    [TestCase("=== joker-loner\n~ short a\nB: 1\nA: 2\n", "A says the first line")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nA: 2\nB: 3\n", "line 2: the same speaker twice in a row")]
    [TestCase("=== joker-loner\n~ short a\nA: 1\nC: 2\n", "B never speaks")]
    [TestCase("=== joker-loner\n~ short a\nA: hi {0}\nB: 2\n", "line 1: unknown {0}")]
    public void Checker_ReportsBrokenScenes(string text, string problem)
    {
        DialogueFile file = DialogueFile.Parse(text, "scenes.dlg");
        Assert.That(DialogueCheck.Problems(file, "scenes", null), Has.Some.Contains(problem));
    }

    [Test]
    public void ScenesFile_CharacterBlock_GivesThatCharacterItsOwnScenes()
    {
        Assert.That(PairScenes.ForCharacter("spyburd"), Is.Null);
        Write("scenes", BuiltInText("scenes") + "\n=== spyburd-any\n~ short buff\nA: Buff, {b}?\nB: No thanks, {a}.\n");
        Assert.That(DialogueLibrary.Initialize(_folder).Errors, Is.Empty);
        TypePair pair = PairScenes.ForCharacter("spyburd");
        Assert.That(pair.Name, Is.EqualTo("spyburd-any"));
        Assert.That(pair.Scenes.Single().Name, Is.EqualTo("buff"));
        Assert.That(PairScenes.Find("spyburd-any"), Is.SameAs(pair));
        Assert.That(pair.HasCrowd, Is.False);
        Assert.That(PairScenes.ForCharacter("eaglefang"), Is.Null);
        Assert.That(PairScenes.RivalsOf("casual"), Does.Not.Contain(PairScenes.AnyRival));
    }

    [Test]
    public void ScenesFile_Edit_IsUsed_AndABrokenOneKeepsTheBuiltInScenes()
    {
        string builtIn = BuiltInText("scenes");
        Assert.That(PairScenes.Find("loner-joker").Scenes.Single(scene => scene.Name == "knock-knock").Lines[0].Text, Is.EqualTo("{b}, knock knock."));

        Write("scenes", builtIn.Replace("A: {b}, knock knock.", "A: {b}, knock knock. From the folder."));
        Assert.That(DialogueLibrary.Initialize(_folder).Errors, Is.Empty);
        Assert.That(PairScenes.Find("joker-loner").Scenes.Single(scene => scene.Name == "knock-knock").Lines[0].Text, Is.EqualTo("{b}, knock knock. From the folder."));

        Write("scenes", builtIn.Replace("A: {b}, knock knock.", "D: {b}, knock knock."));
        DialogueLoadReport report = DialogueLibrary.Reload();
        Assert.That(report.Errors, Has.Some.Contains("doesn't start with 'A:', 'B:' or 'C:'"));
        Assert.That(report.Errors, Does.Contain("scenes.dlg: not used; the built-in copy stays"));
        Assert.That(PairScenes.Find("joker-loner").Scenes.Single(scene => scene.Name == "knock-knock").Lines[0].Text, Is.EqualTo("{b}, knock knock."));
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
