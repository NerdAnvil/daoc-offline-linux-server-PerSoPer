using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>{bot:shnacksize} in a line: each realm says its own bot's name (DialogueBotNames).</summary>
[TestFixture]
public class UT_DialogueBotNames
{
    [TestCase(eRealm.Midgard, "Shnacksize is one ahead.")]
    [TestCase(eRealm.Albion, "Shnacksizer is one ahead.")]
    [TestCase(eRealm.Hibernia, "Shnacksizey is one ahead.")]
    [TestCase(null, "Shnacksize is one ahead.")]
    public void Fill_GivesTheRealmsName(eRealm? realm, string said) =>
        Assert.That(DialogueBotNames.Fill("{bot:shnacksize} is one ahead.", realm), Is.EqualTo(said));

    [Test]
    public void Fill_KeepsOtherBracesAndUnknownCharacters()
    {
        Assert.That(DialogueBotNames.Fill("{bot:nobody} and {0} near {zone}", eRealm.Albion), Is.EqualTo("{bot:nobody} and {0} near {zone}"));
        Assert.That(DialogueBotNames.Fill("Ask {bot:Spyburd}, {0}.", eRealm.Hibernia), Is.EqualTo("Ask Spyburdagh, {0}."));
        Assert.That(DialogueBotNames.Fill("{bot:xxxnatasehsxxx}", eRealm.Albion), Is.EqualTo("xAxNataSehSxAx"));
    }

    [Test]
    public void Check_WantsTheSlot_InLinesEveryRealmSays()
    {
        DialogueFile file = DialogueFile.Parse(
            "=== shnacksize\n~ ambient\nEaglefang told me to sit.\n{bot:eaglefang} told me to stand.\n{bot:gary} waved.\n" +
            "=== albion\n~ ambient\nEaglefange and Spyburde, my Albion pals.\n", "shnacksize.dlg");
        List<string> problems = DialogueBotNames.Check(file, "shnacksize");

        Assert.That(problems, Has.Count.EqualTo(2));
        Assert.That(problems, Has.Some.Contains("'Eaglefang' is one realm's name; write {bot:eaglefang}"));
        Assert.That(problems, Has.Some.Contains("'{bot:gary}' is no character"));
    }

    [Test]
    public void Check_RefusesTheSlot_WhereNothingFillsIt()
    {
        DialogueFile joker = DialogueFile.Parse("=== joker\n~ ambient\n{bot:spyburd} owes me a buff.\n", "joker.dlg");
        DialogueFile persoper = DialogueFile.Parse("=== persoper\n~ reply joke\n{bot:spyburd} laughed.\n~ ding\n{bot:spyburd} cheered.\n", "persoper.dlg");

        Assert.That(DialogueBotNames.Check(joker, "joker"), Has.One.Contains("isn't filled in here"));
        Assert.That(DialogueBotNames.Check(persoper, "persoper"), Has.One.Contains("~ ding: '{bot:spyburd}' isn't filled in here"));
    }

    [Test]
    public void BuiltInFiles_NameBotsByTheSlot()
    {
        foreach ((string name, DialogueFile file) in DialogueLibrary.BuiltIn)
            Assert.That(DialogueBotNames.Check(file, name), Is.Empty, name);
    }

    [Test]
    public void NamedBots_SayTheirRealmsNames()
    {
        DialogueLibrary.Initialize(null);
        string[] Ambient(string bot) => PersonalityLines.Own[PersonalityLines.NamedKey(NamedBotLineup.FindByName(bot))].Talk.Ambient;
        IEnumerable<string> All(string bot)
        {
            (TypeLines lines, TypeTalk talk) = PersonalityLines.Own[PersonalityLines.NamedKey(NamedBotLineup.FindByName(bot))];
            return talk.Ambient.Concat(talk.Replies.Values.SelectMany(pool => pool)).Concat(lines.Openers.Values.SelectMany(pool => pool))
                .Concat(lines.Ding).Concat(lines.OwnDeath).Concat(lines.OtherDeath).Concat(lines.Drop);
        }

        Assert.That(Ambient("TheriousTham"), Has.Some.Contains("Shnacksize says her joke count"));
        Assert.That(Ambient("TheriousThamm"), Has.Some.Contains("Shnacksizer says her joke count"));
        Assert.That(Ambient("TheriousThamh"), Has.Some.Contains("Shnacksizey says her joke count"));
        foreach (NamedBotLineup.Entry entry in NamedBotLineup.All)
            Assert.That(All(entry.Name), Has.None.Contains("{bot:"), entry.Name);
    }

    [TestCase("Shnacksize", "shnacksize")]
    [TestCase("Shnacksizer", "shnacksize")]
    [TestCase("Spyburdagh", "spyburd")]
    [TestCase("xAxNataSehSxAx", "xxxnatasehsxxx")]
    [TestCase("Gary", null)]
    public void Nearby_KnowsANamedCharacterInEveryRealm(string bot, string character) =>
        Assert.That(DialogueFactSource.CharacterOf(bot), Is.EqualTo(character));

    [TestCase(eRealm.Midgard, "Shnacksize")]
    [TestCase(eRealm.Albion, "Shnacksizer")]
    [TestCase(eRealm.Hibernia, "Shnacksizey")]
    public void Persoper_GroansWithHerRealmsName_AndKeepsTheLinesTags(eRealm realm, string joker)
    {
        DialogueLibrary.Initialize(null);
        var nearby = new HashSet<string>(["spyburd", "shnacksize"], StringComparer.OrdinalIgnoreCase);
        var said = new HashSet<string>();
        for (int seed = 0; seed < 40; seed++)
            if (PersoperIdentity.Personality.ConditionalReply(eAutonomousChatIntent.Joke, "Spyburd", new Random(seed),
                    new DialogueFacts { BotId = $"persoper-{realm}-{seed}", Realm = realm, InCombat = false, GroupSize = 2, TimeOfDay = "day", Nearby = nearby }) is { } line)
                said.Add(line);

        Assert.That(said, Has.Some.Contains(joker + " "), "a groan line that names her");
        Assert.That(said, Has.None.Contains("{bot:"));
        Assert.That(PersoperIdentity.Personality.ConditionalReply(eAutonomousChatIntent.Joke, "Spyburd", new Random(1),
            new DialogueFacts { BotId = $"persoper-{realm}-alone", Realm = realm, Nearby = new HashSet<string>(["spyburd"], StringComparer.OrdinalIgnoreCase) }), Is.Null, "the [if nearby] tag still counts");
    }
}
