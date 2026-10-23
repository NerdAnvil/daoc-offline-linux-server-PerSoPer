using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Step 6 of the named bots: the three Roleplayers (Hunterson, FitzHunter, MacHunter) speak their realm's old tongue.</summary>
[TestFixture]
public class UT_RoleplayerVoice
{
    private static readonly string[] Names = ["Hunterson", "FitzHunter", "MacHunter"];
    private static readonly HashSet<string> Slots = ["{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");
    private static readonly Regex Norse = new(@"\b(odin|thor|loki|valhalla|valkyries?|mead|skalds?|skol|jarls?|norns|runes?|yggdrasil|allfather)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Knightly = new(@"\b(forsooth|thee|thou|thy|thine|hath|doth|prithee|verily|hark|m'lord|m'lady)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Gaelic = new(@"\b(beannacht|slainte|sidhe|failte|a chara|a chairde|dia dhuit|slan|mallacht|och|comhghairdeas|seanchai|pooka|go n-eiri|bothar|tir na nog|glimmerach|rath-rinna|brannaghmor)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Blessing = new(@"bless|beannacht|slainte", RegexOptions.IgnoreCase);
    // Thine goes before a vowel sound, thy before a consonant, and "is thine" stands alone; FitzHunter gets some of it backwards.
    private static readonly Regex ThyThineRight = new(@"\bthy\s+(?![aeiou])[a-z]|\bthine\s+[aeiou]", RegexOptions.IgnoreCase);
    private static readonly Regex ThyThineWrong = new(@"\bthine\s+(?![aeiouh])[a-z]|\bthy\s+[aeiou]|\bis thy\b", RegexOptions.IgnoreCase);

    private static string[] OwnSpoken(string name)
    {
        (TypeLines lines, TypeTalk talk) = PersonalityLines.Own["named:" + name.ToLowerInvariant()];
        return talk.Ambient.Concat(talk.FallbackReplies).Concat(talk.Replies.Values.SelectMany(pool => pool))
            .Concat(lines.Openers.Values.SelectMany(pool => pool))
            .Concat(lines.Ding).Concat(lines.Grats).Concat(lines.OwnDeath).Concat(lines.OtherDeath).Concat(lines.Drop).ToArray();
    }

    /// <summary>Every line of the type block, straight from the file.</summary>
    private static string[] TypeSpoken() =>
        DialogueLibrary.For("roleplayer").Block("roleplayer").SelectMany(section => section.Lines).ToArray();

    [Test]
    public void Roleplayer_IsNamedOnly_NeverRolledForGeneratedBots()
    {
        Assert.That(PersonalityTypes.Templates.ContainsKey("roleplayer"), Is.False);
        Assert.That(PersonalityTypes.Keys, Does.Not.Contain("roleplayer"));
        Assert.That(PersonalityTypes.Normalise("roleplayer"), Is.Null);
        Assert.That(PersonalityLines.All.ContainsKey("roleplayer"), Is.False);
        Assert.That(PersonalityTypeVoices.NamedOnly.ContainsKey("roleplayer"), Is.True);
        var random = new Random(7);
        for (int i = 0; i < 500; i++)
            Assert.That(PersonalityTypes.Roll(random), Is.Not.EqualTo("roleplayer"));
    }

    [Test]
    public void RoleplayerFile_LoadsWithoutErrors_AndHoldsOnlyTheTypeAndItsThreeBots()
    {
        Assert.That(PersonalityLines.Errors, Is.Empty);
        DialogueFile file = DialogueLibrary.For("roleplayer");
        Assert.That(file, Is.Not.Null);
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.BlockNames, Is.EquivalentTo(Names.Select(name => name.ToLowerInvariant()).Append("roleplayer")));
    }

    [TestCase("Hunterson")]
    [TestCase("FitzHunter")]
    [TestCase("MacHunter")]
    public void Named_IsTheRoleplayerUnderItsOwnNameAndMemoryKey(string name)
    {
        NamedBotLineup.Entry entry = NamedBotLineup.FindByName(name);
        BotPersonality named = PersonalityTypes.Named(entry);
        BotPersonality roleplayer = PersonalityTypeVoices.NamedOnly["roleplayer"];

        Assert.That(named, Is.Not.Null);
        Assert.That(named.Name, Is.EqualTo(name));
        Assert.That(named.Key, Is.EqualTo("named:" + name.ToLowerInvariant()));
        Assert.That(named.TypeKey, Is.EqualTo("roleplayer"));
        Assert.That(named.Matches(null), Is.False);
        Assert.That(named.Patience, Is.SameAs(roleplayer.Patience));
        Assert.That(named.Ambient, Is.SupersetOf(roleplayer.Ambient));
        Assert.That(named.Ambient.Length, Is.GreaterThan(roleplayer.Ambient.Length));
        TypeLines lines = PersonalityReactions.LinesFor(named);
        Assert.That(lines, Is.Not.Null);
        Assert.That(lines.Openers.Values.Sum(pool => pool.Length), Is.GreaterThan(PersonalityLines.Own[named.Key].Lines.Openers.Values.Sum(pool => pool.Length)));
    }

    [Test]
    public void FitzHunter_SaysForsooth_AndMixesUpThyAndThine_MostlyRight()
    {
        string[] spoken = OwnSpoken("FitzHunter");
        Assert.That(spoken.Count(line => line.Contains("forsooth", StringComparison.OrdinalIgnoreCase)), Is.GreaterThanOrEqualTo(5));
        Assert.That(PersonalityLines.Own["named:fitzhunter"].Talk.Replies[eAutonomousChatIntent.Greeting], Has.Some.Contains("Forsooth"));
        int right = spoken.Sum(line => ThyThineRight.Matches(line).Count);
        int wrong = spoken.Sum(line => ThyThineWrong.Matches(line).Count);
        Assert.That(wrong, Is.GreaterThanOrEqualTo(4), "mix-ups");
        Assert.That(right, Is.GreaterThan(wrong), "mostly right");
    }

    [Test]
    public void MacHunter_BlessesInGaelicSoundingWords()
    {
        string[] spoken = OwnSpoken("MacHunter");
        Assert.That(spoken.Count(line => Gaelic.IsMatch(line)), Is.GreaterThanOrEqualTo(10));
        TypeTalk talk = PersonalityLines.Own["named:machunter"].Talk;
        foreach (eAutonomousChatIntent intent in new[] { eAutonomousChatIntent.Greeting, eAutonomousChatIntent.Farewell })
        {
            Assert.That(talk.Replies[intent], Has.Some.Matches<string>(line => Blessing.IsMatch(line)), intent.ToString());
            Assert.That(talk.Replies[intent], Has.All.Matches<string>(line => Gaelic.IsMatch(line)), intent.ToString());
        }
    }

    [Test]
    public void Hunterson_SpeaksNorse()
    {
        string[] spoken = OwnSpoken("Hunterson");
        Assert.That(spoken.Count(line => Norse.IsMatch(line)), Is.GreaterThanOrEqualTo(10));
    }

    [Test]
    public void EachRealmKeepsItsOwnTongue_AndTheTypeSpeaksNone()
    {
        Assert.That(OwnSpoken("Hunterson"), Has.None.Matches<string>(line => Knightly.IsMatch(line) || Gaelic.IsMatch(line)));
        Assert.That(OwnSpoken("FitzHunter"), Has.None.Matches<string>(line => Gaelic.IsMatch(line)));
        Assert.That(OwnSpoken("MacHunter"), Has.None.Matches<string>(line => Knightly.IsMatch(line)));
        string[] type = TypeSpoken();
        Assert.That(type, Is.Not.Empty);
        Assert.That(type, Has.None.Matches<string>(line => Knightly.IsMatch(line) || Gaelic.IsMatch(line) || Norse.IsMatch(line)));
    }

    [Test]
    public void TypeVoice_AsksPeopleToPlayAlong_AndGrumblesWhenCalledOut()
    {
        TypeTalk talk = PersonalityLines.TalkFor("roleplayer");
        Assert.That(talk.Ambient.Count(line => Regex.IsMatch(line, "character|roleplay|lore", RegexOptions.IgnoreCase)), Is.GreaterThanOrEqualTo(3));
        Assert.That(talk.Replies[eAutonomousChatIntent.Abuse], Has.Some.Contains("roleplaying"));
        Assert.That(talk.FallbackReplies, Is.Not.Empty);
        Assert.That(PersonalityTypeVoices.NamedOnly["roleplayer"].CheekyReplies, Is.Null);
    }

    [Test]
    public void OwnLines_AreShort_UseOnlyTheirPlaceholders_AndAreNew()
    {
        TypeTalk typeTalk = PersonalityLines.TalkFor("roleplayer");
        var typeLines = new HashSet<string>(typeTalk.Ambient.Concat(typeTalk.FallbackReplies)
            .Concat(typeTalk.Replies.Values.SelectMany(pool => pool)), StringComparer.OrdinalIgnoreCase);
        foreach (string name in Names)
        {
            (TypeLines lines, TypeTalk talk) = PersonalityLines.Own["named:" + name.ToLowerInvariant()];
            var all = new List<(string Where, string Line, bool SlotsOk)>();
            all.AddRange(talk.Ambient.Select(line => ($"{name} ambient", line, false)));
            all.AddRange(talk.FallbackReplies.Select(line => ($"{name} fallback", line, false)));
            all.AddRange(talk.Replies.SelectMany(pair => pair.Value.Select(line => ($"{name} reply {pair.Key}", line, false))));
            all.AddRange(lines.Openers.SelectMany(pair => pair.Value.Select(line => ($"{name} opener {pair.Key}", line, true))));
            all.AddRange(lines.Ding.Concat(lines.OwnDeath).Concat(lines.Drop).Select(line => ($"{name} event", line, true)));
            all.AddRange(lines.Grats.Concat(lines.OtherDeath).Select(line => ($"{name} event", line, false)));

            Assert.That(talk.Ambient.Length, Is.GreaterThanOrEqualTo(5), $"{name} ambient");
            Assert.That(lines.Openers.Values.Sum(pool => pool.Length), Is.GreaterThanOrEqualTo(6), $"{name} openers");
            Assert.That(talk.Replies.ContainsKey(eAutonomousChatIntent.Greeting), $"{name} greeting");
            Assert.That(talk.Replies.ContainsKey(eAutonomousChatIntent.Farewell), $"{name} farewell");
            Assert.That(lines.Ding, Is.Not.Empty, $"{name} ding");
            Assert.That(lines.OwnDeath, Is.Not.Empty, $"{name} own_death");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (where, line, slotsOk) in all)
            {
                Assert.That(line.Length, Is.InRange(1, 149), $"{where}: {line}");
                Assert.That(line.All(c => c < 128), $"{where}: non-ASCII letter in {line}");
                Assert.That(seen.Add(line), $"{where}: said twice: {line}");
                Assert.That(typeLines, Does.Not.Contain(line), $"{where}: copies a roleplayer line");
                foreach (Match m in Placeholder.Matches(line))
                    Assert.That(m.Value == "{0}" || slotsOk && Slots.Contains(m.Value), $"{where}: {m.Value} in {line}");
                Assert.That(Placeholder.Replace(line, ""), Does.Not.Contain("{").And.Not.Contain("}"), $"{where}: stray brace");
            }
        }
    }
}
