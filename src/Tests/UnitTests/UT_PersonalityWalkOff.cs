using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B3c: the walk-off gag after a bad joke. The guards (never in or near a fight, never mid-task), the
/// once-a-session roll, the 24 h cooldown, the rejoin rules and the far formation spot are checked here without a
/// server; the leave, walk and rejoin themselves are tools/check-walkoff-ingame.sh.
/// </summary>
[TestFixture]
public class UT_PersonalityWalkOff
{
    [SetUp]
    public void Reset() => PersonalityWalkOff.Clear();

    // A world bot standing quietly in the group of the player, who leads it; nothing going on.
    private static PersonalityWalkOff.Situation Calm() => new(
        InGroupWithPlayer: true, Alive: true, InWorld: true, SameRegionNear: true,
        InCombat: false, Attacking: false, Casting: false, HasAggro: false, MsSinceHit: long.MaxValue,
        Riding: false, OnStableRoute: false, Resting: false, Stealthed: false, InDungeon: false,
        LeadsGroup: false, GroupFighting: false, PlayerTrading: false, QuitsGroup: true, PlayerLeads: true);

    [Test]
    public void ACalmBotInTheGroup_IsNotBlocked() => Assert.That(PersonalityWalkOff.Blocked(Calm()), Is.Null);

    [TestCase("NotGrouped")]
    [TestCase("Dead")]
    [TestCase("NotInWorld")]
    [TestCase("Far")]
    [TestCase("InCombat")]
    [TestCase("Attacking")]
    [TestCase("Aggro")]
    [TestCase("Casting")]
    [TestCase("HitLately")]
    [TestCase("GroupFighting")]
    [TestCase("Riding")]
    [TestCase("StableRoute")]
    [TestCase("Resting")]
    [TestCase("Stealthed")]
    [TestCase("Dungeon")]
    [TestCase("Leader")]
    [TestCase("Trading")]
    [TestCase("PlayerNotLeader")]
    public void EachGuard_BlocksTheGag(string guard)
    {
        PersonalityWalkOff.Situation s = Calm();
        s = guard switch
        {
            "NotGrouped" => s with { InGroupWithPlayer = false },
            "Dead" => s with { Alive = false },
            "NotInWorld" => s with { InWorld = false },
            "Far" => s with { SameRegionNear = false },
            "InCombat" => s with { InCombat = true },
            "Attacking" => s with { Attacking = true },
            "Aggro" => s with { HasAggro = true },
            "Casting" => s with { Casting = true },
            "HitLately" => s with { MsSinceHit = PersonalityWalkOff.RecentlyHitMs - 1 },
            "GroupFighting" => s with { GroupFighting = true },
            "Riding" => s with { Riding = true },
            "StableRoute" => s with { OnStableRoute = true },
            "Resting" => s with { Resting = true },
            "Stealthed" => s with { Stealthed = true },
            "Dungeon" => s with { InDungeon = true },
            "Leader" => s with { LeadsGroup = true },
            "Trading" => s with { PlayerTrading = true },
            "PlayerNotLeader" => s with { PlayerLeads = false },
            _ => throw new ArgumentException(guard),
        };
        Assert.That(PersonalityWalkOff.Blocked(s), Is.Not.Null, guard);
    }

    [Test]
    public void BeingHitLongAgo_IsNotAFight() =>
        Assert.That(PersonalityWalkOff.Blocked(Calm() with { MsSinceHit = PersonalityWalkOff.RecentlyHitMs }), Is.Null);

    // A companion only walks off, so it doesn't need the player to lead the group (it never has to be invited back).
    [Test]
    public void ACompanion_StaysGrouped_SoTheLeaderRuleIsOnlyForWorldBots() =>
        Assert.That(PersonalityWalkOff.Blocked(Calm() with { QuitsGroup = false, PlayerLeads = false }), Is.Null);

    [Test]
    public void ASession_HasAboutATwoPercentChance()
    {
        Assert.That(PersonalityWalkOff.SessionChance, Is.EqualTo(0.02));
        Assert.That(PersonalityWalkOff.RollSession(new FixedRandom(0.0199)), Is.True);
        Assert.That(PersonalityWalkOff.RollSession(new FixedRandom(0.02)), Is.False);
        Random random = new(21);
        int won = Enumerable.Range(0, 20_000).Count(_ => PersonalityWalkOff.RollSession(random));
        Assert.That(won / 20_000.0, Is.EqualTo(0.02).Within(0.005));
    }

    [Test]
    public void AfterTheGag_NoneForTwentyFourHours()
    {
        DateTime now = new(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);
        string Ago(TimeSpan ago) => new DateTimeOffset(now - ago).ToUnixTimeSeconds().ToString();
        Assert.That(PersonalityWalkOff.Cooldown, Is.EqualTo(TimeSpan.FromHours(24)));
        Assert.That(PersonalityWalkOff.CoolingDown(Ago(TimeSpan.FromMinutes(5)), now), Is.True);
        Assert.That(PersonalityWalkOff.CoolingDown(Ago(TimeSpan.FromHours(23.9)), now), Is.True);
        Assert.That(PersonalityWalkOff.CoolingDown(Ago(TimeSpan.FromHours(24)), now), Is.False);
        Assert.That(PersonalityWalkOff.CoolingDown(null, now), Is.False, "never had it");
        Assert.That(PersonalityWalkOff.CoolingDown("0", now), Is.False, "/dialogue walkoff reset");
        Assert.That(PersonalityWalkOff.CoolingDown("not a time", now), Is.False);
        Assert.That(PersonalityWalkOff.CoolingDown(long.MaxValue.ToString(), now), Is.True, "a far-future time doesn't throw");
    }

    [Test]
    public void TheCooldownKey_IsAValidSavedAnswer()
    {
        string saved = null;
        var cache = new PlayerAnswerCache(_ => new Dictionary<string, string>(), (_, _, answer) => saved = answer);
        Assert.That(cache.Set("player", PersonalityWalkOff.CooldownKey, "1793577600"), Is.True);
        Assert.That(saved, Is.EqualTo("1793577600"));
        Assert.That(cache.Set("player", PersonalityWalkOff.CooldownKey, "0"), Is.True, "reset");
    }

    [TestCase(true, true, true, false, true, false, null)]
    [TestCase(false, true, true, false, true, false, "the player left")]
    [TestCase(true, false, true, false, true, false, "another realm")]
    [TestCase(true, true, false, false, true, false, "the bot is down")]
    [TestCase(true, true, true, true, true, false, "the bot is in a group")]
    [TestCase(true, true, true, false, false, false, "the player doesn't lead the group")]
    [TestCase(true, true, true, false, true, true, "the group is full")]
    public void Rejoining_NeedsThePlayerHere_LeadingAGroupWithRoom(bool here, bool realm, bool up, bool inGroup, bool leads, bool full, string expected) =>
        Assert.That(PersonalityWalkOff.RejoinBlocked(here, realm, up, inGroup, leads, full), Is.EqualTo(expected));

    [Test]
    public void TheWalkOff_IsShort_AndRejoinTriesAreCapped()
    {
        Assert.That(PersonalityWalkOff.AwayMs, Is.InRange(5_000, 30_000));
        Assert.That(PersonalityWalkOff.RejoinTries, Is.InRange(1, 5));
        Assert.That(PersonalityWalkOff.RejoinTries * PersonalityWalkOff.RejoinGapMs, Is.LessThanOrEqualTo(20_000));
        Assert.That(PersonalityWalkOff.MaxTracked, Is.InRange(1, 1_000), "bounded store");
    }

    [Test]
    public void TheAwaySpot_IsFarOutInTheSameDirection_AndNeverCloser()
    {
        var normal = new AutonomousFormation.Offset(90, 40);
        var away = PersonalityWalkOff.Away(normal);
        Assert.That(away.AngleDegrees, Is.EqualTo(40));
        Assert.That(away.Distance, Is.EqualTo(PersonalityWalkOff.AwayDistance).And.GreaterThan(normal.Distance * 2));
        Assert.That(PersonalityWalkOff.Away(new(PersonalityWalkOff.AwayDistance + 50, 10)).Distance, Is.EqualTo(PersonalityWalkOff.AwayDistance + 50));
    }

    [Test]
    public void TheGagLines_AreReplyKinds_NeverReadFromChat()
    {
        foreach (eAutonomousChatIntent kind in new[] { eAutonomousChatIntent.JokeQuit, eAutonomousChatIntent.JokeReturn })
        {
            Assert.That(AutonomousChatIntentModel.IsJokeReplyKind(kind), Is.True, kind.ToString());
            Assert.That(AutonomousChatIntentModel.IsSmallTalk(kind), Is.False, kind.ToString());
        }
        foreach (string line in new[] { "I'm out", "I'm back", "joke quit", "that's it, bye" })
            Assert.That(AutonomousChatIntentModel.Read(line), Is.Not.EqualTo(eAutonomousChatIntent.JokeQuit).And.Not.EqualTo(eAutonomousChatIntent.JokeReturn), line);
    }

    // The gag needs to know a groan from a laugh.
    [Test]
    public void PunchlineReaction_SaysWhenItIsAGroan()
    {
        BotPersonality casual = DialoguePreview.Find("Casual");
        HashSet<string> groans = casual.Replies[eAutonomousChatIntent.Groan].Select(line => string.Format(line, "Tester")).ToHashSet();
        Random random = new(8);
        for (int i = 0; i < 200; i++)
        {
            (string line, bool groan) = PersonalityJokes.PunchlineReaction(casual, "Tester", random);
            Assert.That(groan, Is.EqualTo(groans.Contains(line)), line);
        }
    }

    // Persoper, the Casual type and Shempenstein can storm off and come back; lines may use the player's name.
    [TestCase("Persoper")]
    [TestCase("Casual")]
    [TestCase("Shempenstein")]
    public void SomePersonalities_HaveGagLines(string name)
    {
        BotPersonality personality = DialoguePreview.Find(name);
        foreach (eAutonomousChatIntent kind in new[] { eAutonomousChatIntent.JokeQuit, eAutonomousChatIntent.JokeReturn })
        {
            string[] lines = personality.Replies.GetValueOrDefault(kind);
            Assert.That(lines, Is.Not.Null.And.Length.AtLeast(3), $"{name} {kind}");
            foreach (string line in lines)
                Assert.That(string.Format(line, "Tester").Length, Is.InRange(10, 120), line);
        }
    }

    // Always returns the same roll, so the chance edge can be tested exactly.
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }
}
