using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_DialogueRules
{
    private static readonly DateTime Noon = new(2026, 10, 23, 12, 0, 0);

    private static DialogueFile Parse(string lines) => DialogueFile.Parse("=== bot\n~ reply help\n" + lines, "rules.dlg");

    private static string Pick(DialoguePicker picker, DialogueFile file, DialogueFacts facts, DateTime now, int seed = 1) =>
        picker.Pick(file.Block("bot")[0].Lines, file.RuleFor, facts, "bot", now, new Random(seed));

    [Test]
    public void Parse_TakesTheTagsOffTheText()
    {
        DialogueFile file = Parse("Plain.\nDead! [if player_dead, deaths_today >= 3] [once]\nShiny. [if recent loot] [cooldown 11m]\n");

        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.Block("bot")[0].Lines, Is.EqualTo(new[] { "Plain.", "Dead!", "Shiny." }));
        Assert.That(file.RuleFor("Plain."), Is.Null);
        DialogueRule dead = file.RuleFor("Dead!");
        Assert.That(dead.Once, Is.True);
        Assert.That(dead.Conditions.Select(c => (c.Fact, c.Operator, c.Number)), Is.EqualTo(new[] { ("player_dead", (string)null, 0), ("deaths_today", ">=", 3) }));
        Assert.That(file.RuleFor("Shiny.").Cooldown, Is.EqualTo(TimeSpan.FromMinutes(11)));
    }

    [TestCase("x [if raining]", "rules.dlg:3: unknown fact 'raining'")]
    [TestCase("x [if mood grumpy]", "rules.dlg:3: 'mood' needs one of: warm, neutral, cool")]
    [TestCase("x [if health]", "rules.dlg:3: 'health' needs a comparison, for example [if health >= 3]")]
    [TestCase("x [if player_dead 3]", "rules.dlg:3: 'player_dead' takes no value: [if player_dead] or [if not player_dead]")]
    [TestCase("x [cooldown soon]", "rules.dlg:3: bad cooldown 'soon' (use for example 30s, 11m or 2h, at most 24h)")]
    [TestCase("x [twice]", "rules.dlg:3: unknown tag '[twice]'")]
    [TestCase("x [once]\nx [once]", "rules.dlg:4: a line with tags must not appear twice in a file; change its text")]
    public void Parse_ReportsBadTags(string lines, string error)
    {
        Assert.That(Parse(lines + "\nplain\n").Errors, Does.Contain(error));
    }

    [Test]
    public void Parse_RulesOnlyInReplySections_AndFallbackNeedsAPlainLine()
    {
        DialogueFile file = DialogueFile.Parse("=== bot\n~ ding\nDing! [once]\n~ fallback\nHuh? [if player_dead]\n", "rules.dlg");
        Assert.That(file.Errors, Does.Contain("rules.dlg:3: rules only work in reply, cheeky_reply, fallback sections for now"));
        Assert.That(file.Errors, Does.Contain("rules.dlg:4: every fallback line has [if ...]; keep at least one without"));
        Assert.That(file.RuleFor("Ding!"), Is.Null);
    }

    [Test]
    public void Pick_FalseConditionsAreDropped_AndTheMostSpecificWins()
    {
        DialogueFile file = Parse("Plain.\nDead! [if player_dead]\nDead three times! [if player_dead, deaths_today >= 3]\n");
        var picker = new DialoguePicker();

        var alive = new DialogueFacts { PlayerId = "p", PlayerDead = false, DeathsToday = 5 };
        Assert.That(Enumerable.Range(0, 20).Select(seed => Pick(picker, file, alive, Noon, seed)).Distinct(), Is.EqualTo(new[] { "Plain." }));

        var deadOnce = new DialogueFacts { PlayerId = "p", PlayerDead = true, DeathsToday = 1 };
        Assert.That(Pick(picker, file, deadOnce, Noon), Is.EqualTo("Dead!"));

        var deadThrice = new DialogueFacts { PlayerId = "p", PlayerDead = true, DeathsToday = 3 };
        Assert.That(Pick(picker, file, deadThrice, Noon), Is.EqualTo("Dead three times!"));
    }

    [Test]
    public void Pick_UnknownFactsAreFalse()
    {
        DialogueFile file = Parse("Plain.\nDead! [if player_dead]\n");
        Assert.That(Pick(new DialoguePicker(), file, null, Noon), Is.EqualTo("Plain."));
    }

    [Test]
    public void Pick_OnceIsOncePerPlayer()
    {
        DialogueFile file = Parse("Only once. [if mood warm] [once]\nPlain.\n");
        var picker = new DialoguePicker();
        var kev = new DialogueFacts { PlayerId = "kev", Mood = "warm" };
        var ann = new DialogueFacts { PlayerId = "ann", Mood = "warm" };

        Assert.That(Pick(picker, file, kev, Noon), Is.EqualTo("Only once."));
        Assert.That(Enumerable.Range(0, 20).Select(seed => Pick(picker, file, kev, Noon, seed)), Does.Not.Contain("Only once."));
        Assert.That(Pick(picker, file, ann, Noon), Is.EqualTo("Only once."));
    }

    [Test]
    public void Pick_CooldownHoldsALineBackUntilItEnds()
    {
        DialogueFile file = Parse("Shiny! [if recent loot] [cooldown 11m]\nPlain.\n");
        var picker = new DialoguePicker();
        var looted = new DialogueFacts { PlayerId = "p", Recent = new HashSet<string> { "loot" } };

        Assert.That(Pick(picker, file, looted, Noon), Is.EqualTo("Shiny!"));
        Assert.That(Pick(picker, file, looted, Noon.AddMinutes(10)), Is.EqualTo("Plain."));
        Assert.That(Pick(picker, file, looted, Noon.AddMinutes(11)), Is.EqualTo("Shiny!"));
    }

    [Test]
    public void Pick_SkipsRecentLinesWhileOthersAreLeft()
    {
        DialogueFile file = Parse("One.\nTwo.\nThree.\n");
        var picker = new DialoguePicker();
        var facts = new DialogueFacts { PlayerId = "p" };

        string[] picks = Enumerable.Range(0, 3).Select(seed => Pick(picker, file, facts, Noon, seed)).ToArray();
        Assert.That(picks, Is.EquivalentTo(new[] { "One.", "Two.", "Three." }), "three picks from three lines repeat nothing");
        Assert.That(Pick(picker, file, facts, Noon), Is.Not.Null, "with every line recent it still answers");
    }

    [Test]
    public void Facts_CheckEachKind()
    {
        var facts = new DialogueFacts
        {
            PlayerDead = false, InCombat = true, Health = 25, GroupSize = 3, Mood = "cool", TimeOfDay = "night",
            Zone = "Camelot City", Recent = new HashSet<string> { "kill" }, Nearby = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Kobaldjovi" },
        };
        DialogueFile file = Parse("x [if not player_dead, in_combat, health < 30, group_size >= 2, mood cool, time_of_day night, zone camelot city, recent kill]\n" +
            "y [if nearby kobaldjovi]\nz [if health > 30]\n");
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.RuleFor("x").Conditions.All(facts.Holds), Is.True);
        Assert.That(file.RuleFor("y").Conditions.All(facts.Holds), Is.True);
        Assert.That(file.RuleFor("z").Conditions.All(facts.Holds), Is.False);
    }

    [TestCase(7u, "morning")]
    [TestCase(13u, "day")]
    [TestCase(19u, "evening")]
    [TestCase(23u, "night")]
    [TestCase(2u, "night")]
    public void TimeOfDay_FollowsTheGameClock(uint hour, string expected)
    {
        Assert.That(DialogueFacts.TimeOfDayFor(hour * 3_600_000), Is.EqualTo(expected));
    }

    [Test]
    public void EventLog_CountsDeathsPerDay_AndKeepsEventsRecentForFiveMinutes()
    {
        PlayerEventLog.Clear();
        PlayerEventLog.Note("p", PlayerEventLog.eEvent.Death, Noon);
        PlayerEventLog.Note("p", PlayerEventLog.eEvent.Death, Noon.AddMinutes(1));
        PlayerEventLog.Note("p", PlayerEventLog.eEvent.Loot, Noon.AddMinutes(2));

        Assert.That(PlayerEventLog.DeathsToday("p", Noon.AddHours(1)), Is.EqualTo(2));
        Assert.That(PlayerEventLog.DeathsToday("p", Noon.AddDays(1)), Is.EqualTo(0), "a new day starts at zero");
        Assert.That(PlayerEventLog.Recent("p", Noon.AddMinutes(5)), Is.EquivalentTo(new[] { "death", "loot" }));
        Assert.That(PlayerEventLog.Recent("p", Noon.AddMinutes(7)), Is.EquivalentTo(new[] { "loot" }));
        Assert.That(PlayerEventLog.Recent("someone else", Noon), Is.Empty);
        PlayerEventLog.Clear();
    }

    [Test]
    public void BuiltInFiles_HaveNoRuleErrors_AndPersoperHasHerTestLines()
    {
        Assert.That(DialogueLibrary.All.Values.SelectMany(file => file.Errors), Is.Empty);
        Assert.That(DialogueLibrary.RuleFor("Hold still, {0}, I'm coming!")?.Conditions.Single().Fact, Is.EqualTo("player_dead"));
    }
}
