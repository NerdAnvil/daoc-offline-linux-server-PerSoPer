using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DaocServer.Admin;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_NamedBots
{
    private static readonly HashSet<string> NoKeys = [];
    private static readonly Dictionary<string, long> NoRoster = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NoPlayers = new(StringComparer.OrdinalIgnoreCase);

    private static NamedBots.StartStep StepFor(IReadOnlyList<NamedBots.StartStep> plan, string key) =>
        plan.Single(step => step.Entry.Key == key);

    [Test]
    public void PlanStart_NewServer_CreatesAll33()
    {
        var plan = NamedBots.PlanStart(NoKeys, NoRoster, NoPlayers);
        Assert.That(plan, Has.Count.EqualTo(33));
        Assert.That(plan.All(step => step.Action == NamedBots.StartAction.Create), Is.True);
    }

    [Test]
    public void PlanStart_EntriesWithARow_AreLeftAlone()
    {
        // on, off and deleted rows all exist, so none of them is made again.
        var keys = new HashSet<string>(NamedBotLineup.All.Select(entry => entry.Key));
        var plan = NamedBots.PlanStart(keys, NoRoster, NoPlayers);
        Assert.That(plan.All(step => step.Action == NamedBots.StartAction.Keep), Is.True);

        var plan2 = NamedBots.PlanStart(new HashSet<string> { "joker-mid" }, NoRoster, NoPlayers);
        Assert.That(StepFor(plan2, "joker-mid").Action, Is.EqualTo(NamedBots.StartAction.Keep));
        Assert.That(plan2.Count(step => step.Action == NamedBots.StartAction.Create), Is.EqualTo(32));
    }

    [Test]
    public void PlanStart_GeneratedBotWithTheName_IsRenamedFirst()
    {
        var roster = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["shnacksize"] = 42 };
        var step = StepFor(NamedBots.PlanStart(NoKeys, roster, NoPlayers), "joker-mid");
        Assert.That(step.Action, Is.EqualTo(NamedBots.StartAction.RenameThenCreate));
        Assert.That(step.ClashBotId, Is.EqualTo(42));
    }

    [Test]
    public void PlanStart_PlayerWithTheName_IsNeverTouched()
    {
        // A player wins even when a bot holds the name too.
        var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SHNACKSIZE" };
        var roster = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Shnacksize"] = 42 };
        var step = StepFor(NamedBots.PlanStart(NoKeys, roster, players), "joker-mid");
        Assert.That(step.Action, Is.EqualTo(NamedBots.StartAction.SkipPlayerHasName));
    }

    [Test]
    public void IdentityFor_KeepsTheLineupAndRollsRandomGenderOnce()
    {
        NamedBotLineup.Entry joker = NamedBotLineup.FindByName("Shnacksize");
        var identity = NamedBots.IdentityFor(joker, new Random(1));
        Assert.That(identity.Name, Is.EqualTo("Shnacksize"));
        Assert.That(identity.Realm, Is.EqualTo(eRealm.Midgard));
        Assert.That(identity.CharacterClass, Is.EqualTo(eCharacterClass.Berserker));
        Assert.That(identity.Race, Is.EqualTo(eRace.Valkyn));
        Assert.That(identity.Gender, Is.EqualTo(eGender.Female));

        NamedBotLineup.Entry homeowner = NamedBotLineup.FindByName("Barbalicious");
        var genders = Enumerable.Range(0, 50).Select(seed => NamedBots.IdentityFor(homeowner, new Random(seed)).Gender).ToHashSet();
        Assert.That(genders, Is.EquivalentTo(new[] { eGender.Male, eGender.Female }));
    }

    [TestCase("Shnacksize", "joker-mid")]
    [TestCase("shnacksize", "joker-mid")]
    [TestCase("JOKER-MID", "joker-mid")]
    [TestCase("MacHunter", "roleplayer-hib")]
    public void Find_TakesNameOrKey(string typed, string key) =>
        Assert.That(NamedBots.Find(typed)?.Key, Is.EqualTo(key));

    [TestCase("Kobaldjovi")]
    [TestCase("joker")]
    [TestCase("")]
    public void Find_UnknownIsNull(string typed) => Assert.That(NamedBots.Find(typed), Is.Null);

    [Test]
    public void AdminCommand_BotsNamedList()
    {
        AdminRequest request = AdminCommandLine.Parse(AdminCommandLine.SplitLine("bots named list"));
        Assert.That(request.Op, Is.EqualTo(AdminOps.BotsNamedList));
        Assert.Throws<AdminUsageException>(() => AdminCommandLine.Parse(AdminCommandLine.SplitLine("bots named")));
        Assert.Throws<AdminUsageException>(() => AdminCommandLine.Parse(AdminCommandLine.SplitLine("bots named off")));
        Assert.Throws<AdminUsageException>(() => AdminCommandLine.Parse(AdminCommandLine.SplitLine("bots named pause joker")));
        Assert.Throws<AdminUsageException>(() => AdminCommandLine.Parse(AdminCommandLine.SplitLine("bots named on joker extra")));
    }

    [TestCase("bots named off joker", "joker", false)]
    [TestCase("bots named ON Shnacksize", "Shnacksize", true)]
    [TestCase("bots named off \"herbal doctor\"", "herbal doctor", false)]
    public void AdminCommand_BotsNamedOnOff(string line, string bot, bool on)
    {
        AdminRequest request = AdminCommandLine.Parse(AdminCommandLine.SplitLine(line));
        Assert.That(request.Op, Is.EqualTo(AdminOps.BotsNamedSet));
        Assert.That(request.GetString("bot"), Is.EqualTo(bot));
        Assert.That(request.GetBool("on"), Is.EqualTo(on));
    }

    [TestCase("joker", "joker-mid", "joker-alb", "joker-hib")]
    [TestCase("JOKER", "joker-mid", "joker-alb", "joker-hib")]
    [TestCase("Herbal Doctor", "herbaldoctor-mid", "herbaldoctor-alb", "herbaldoctor-hib")]
    [TestCase("min/maxer", "minmaxer-mid", "minmaxer-alb", "minmaxer-hib")]
    [TestCase("Shnacksize", "joker-mid")]
    [TestCase("joker-alb", "joker-alb")]
    public void FindAll_NameKeyOrType(string typed, params string[] keys) =>
        Assert.That(NamedBots.FindAll(typed).Select(entry => entry.Key), Is.EquivalentTo(keys));

    [TestCase("Kobaldjovi")]
    [TestCase("mixed")]
    [TestCase("")]
    [TestCase("!!")]
    public void FindAll_UnknownIsEmpty(string typed) => Assert.That(NamedBots.FindAll(typed), Is.Empty);

    [Test]
    public void AdminText_NamedOffSaysWhoChangedAndWhoNot()
    {
        var result = new NamedBotSetResult(["Shnacksize", "Shnacksizer"], ["Shnacksizey: already off."], "off", true);
        string text = AdminText.Format(AdminOps.BotsNamedSet, JsonSerializer.SerializeToNode(result, AdminJson.Options));
        Assert.That(text, Does.Contain("Turned off: Shnacksize, Shnacksizer"));
        Assert.That(text, Does.Contain("Shnacksizey: already off."));
        Assert.That(text, Does.Contain("log out"));
    }

    [Test]
    public void AdminText_NamedOnWithPopulationOffSaysSo()
    {
        var result = new NamedBotSetResult(["Shnacksize"], [], "on", false);
        string text = AdminText.Format(AdminOps.BotsNamedSet, JsonSerializer.SerializeToNode(result, AdminJson.Options));
        Assert.That(text, Does.Contain("Turned on: Shnacksize").And.Contain("Population is OFF"));
    }

    [Test]
    public void AdminText_NamedListShowsEachBotAndATotal()
    {
        var bots = new List<NamedBotInfo>
        {
            new("joker-mid", "Shnacksize", "joker", "Midgard", "Berserker", "on", 1, true),
            new("joker-alb", "Shnacksizer", "joker", "Albion", "Mercenary", "not created", 0, false),
        };
        string text = AdminText.Format(AdminOps.BotsNamedList, JsonSerializer.SerializeToNode(bots, AdminJson.Options));
        Assert.That(text, Does.Contain("Shnacksize").And.Contain("Berserker").And.Contain("not created"));
        Assert.That(text, Does.Contain("1 of 2 named bots on, 1 in the world."));
    }
}
