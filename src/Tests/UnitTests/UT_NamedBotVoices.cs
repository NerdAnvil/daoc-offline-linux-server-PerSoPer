using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Step 4 of the named bots: the 18 named bots of the six types have their own voice.</summary>
[TestFixture]
public class UT_NamedBotVoices
{
    private static readonly HashSet<string> Slots = ["{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    private static IEnumerable<NamedBotLineup.Entry> Voiced =>
        NamedBotLineup.All.Where(entry => PersonalityLines.TypeKeys.Contains(entry.TypeKey));

    /// <summary>Every own line with where it is and which placeholders it may use: "slots", "name" ({0}) or none.</summary>
    private static IEnumerable<(string Where, string Line, bool SlotsOk, bool? NeedsName)> OwnLines(NamedBotLineup.Entry entry)
    {
        (TypeLines lines, TypeTalk talk) = PersonalityLines.Own[PersonalityLines.NamedKey(entry)];
        string who = entry.Name;
        foreach (string line in talk.Ambient) yield return ($"{who} ambient", line, false, false);
        foreach (string line in talk.FallbackReplies) yield return ($"{who} fallback", line, false, null);
        foreach (var (intent, pool) in talk.Replies)
            foreach (string line in pool) yield return ($"{who} reply {intent}", line, false, null);
        foreach (var (intent, pool) in talk.CheekyReplies ?? new Dictionary<eAutonomousChatIntent, string[]>())
            foreach (string line in pool) yield return ($"{who} cheeky_reply {intent}", line, false, null);
        foreach (var (topic, pool) in lines.Openers)
            foreach (string line in pool) yield return ($"{who} opener {topic}", line, true, false);
        foreach (var (topic, pool) in lines.TopicReplies)
            foreach (string line in pool) yield return ($"{who} topic_reply {topic}", line, true, true);
        foreach (string line in lines.Ding) yield return ($"{who} ding", line, true, false);
        foreach (string line in lines.Grats) yield return ($"{who} grats", line, true, true);
        foreach (string line in lines.OwnDeath) yield return ($"{who} own_death", line, true, false);
        foreach (string line in lines.OtherDeath) yield return ($"{who} other_death", line, true, true);
        foreach (string line in lines.Drop) yield return ($"{who} drop", line, true, false);
        foreach (string line in lines.Cheeky ?? []) yield return ($"{who} cheeky", line, true, false);
    }

    [Test]
    public void EighteenNamedBots_HaveTheirOwnLines()
    {
        Assert.That(Voiced.Count(), Is.EqualTo(18));
        foreach (NamedBotLineup.Entry entry in Voiced)
        {
            Assert.That(PersonalityLines.Own.ContainsKey(PersonalityLines.NamedKey(entry)), entry.Name);
            (TypeLines lines, TypeTalk talk) = PersonalityLines.Own[PersonalityLines.NamedKey(entry)];
            Assert.That(talk.Ambient.Length, Is.GreaterThanOrEqualTo(5), $"{entry.Name} ambient");
            Assert.That(lines.Openers.Values.Sum(pool => pool.Length), Is.GreaterThanOrEqualTo(6), $"{entry.Name} openers");
            Assert.That(talk.Replies.ContainsKey(eAutonomousChatIntent.Greeting), $"{entry.Name} greeting");
            Assert.That(lines.Ding, Is.Not.Empty, $"{entry.Name} ding");
            Assert.That(lines.OwnDeath, Is.Not.Empty, $"{entry.Name} own_death");
        }
        Assert.That(PersonalityLines.Errors, Is.Empty);
    }

    [Test]
    public void OwnLines_AreShort_UseOnlyTheirPlaceholders_AndAreNew()
    {
        foreach (NamedBotLineup.Entry entry in Voiced)
        {
            var typeLines = new HashSet<string>(TypeLinesOf(entry.TypeKey), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (where, line, slotsOk, needsName) in OwnLines(entry))
            {
                Assert.That(line.Length, Is.InRange(1, 149), $"{where}: {line}");
                Assert.That(seen.Add(line), $"{where}: said twice: {line}");
                Assert.That(typeLines, Does.Not.Contain(line), $"{where}: copies a {entry.TypeKey} line");
                foreach (Match m in Placeholder.Matches(line))
                    Assert.That(m.Value == "{0}" || slotsOk && Slots.Contains(m.Value), $"{where}: {m.Value} in {line}");
                Assert.That(Placeholder.Replace(line, ""), Does.Not.Contain("{").And.Not.Contain("}"), $"{where}: stray brace");
                if (needsName == true)
                    Assert.That(line, Does.Contain("{0}"), $"{where}: {line}");
                else if (needsName == false)
                    Assert.That(line, Does.Not.Contain("{0}"), $"{where}: {line}");
            }
        }
    }

    [TestCase("Shnacksize", "Swissland")]
    [TestCase("Shnacksizer", "Swissland")]
    [TestCase("Shnacksizey", "Swissland")]
    [TestCase("Redtornader", "red tornado")]
    [TestCase("Greenfogger", "green fog")]
    [TestCase("Sunrayder", "Sun Ray")]
    [TestCase("TheriousTham", "chicken")]
    public void SignatureTheme_IsInTheirOwnAmbientAndOpeners(string name, string theme)
    {
        NamedBotLineup.Entry entry = NamedBotLineup.FindByName(name);
        (TypeLines lines, TypeTalk talk) = PersonalityLines.Own[PersonalityLines.NamedKey(entry)];
        string[] spoken = talk.Ambient.Concat(lines.Openers.Values.SelectMany(pool => pool)).ToArray();
        Assert.That(spoken.Count(line => line.Contains(theme, StringComparison.OrdinalIgnoreCase)), Is.GreaterThanOrEqualTo(3), name);
    }

    [Test]
    public void Named_IsItsTypeUnderItsOwnNameAndMemoryKey()
    {
        NamedBotLineup.Entry entry = NamedBotLineup.FindByName("Shnacksize");
        BotPersonality named = PersonalityTypes.Named(entry);
        BotPersonality joker = PersonalityTypes.Templates["joker"];

        Assert.That(named.Name, Is.EqualTo("Shnacksize"));
        Assert.That(named.Key, Is.EqualTo("named:shnacksize"));
        Assert.That(named.TypeKey, Is.EqualTo("joker"));
        Assert.That(named.Chattiness, Is.EqualTo(joker.Chattiness));
        Assert.That(named.Patience, Is.SameAs(joker.Patience));
        Assert.That(named.Matches(null), Is.False);
        // Its own lines and its type's, in chat replies and ambient talk alike.
        Assert.That(named.Ambient, Is.SupersetOf(joker.Ambient));
        Assert.That(named.Ambient, Has.Some.Contains("Swissland"));
        Assert.That(named.Replies[eAutonomousChatIntent.Greeting], Is.SupersetOf(joker.Replies[eAutonomousChatIntent.Greeting]));
        Assert.That(named.Replies[eAutonomousChatIntent.Greeting], Has.Some.Contains("Swisshlish"));
        // Topic lines and event reactions come from the mixed pools.
        TypeLines lines = PersonalityReactions.LinesFor(named);
        Assert.That(lines.Openers[eChatTopic.Realm], Has.Some.Contains("Swissland"));
        Assert.That(lines.Openers[eChatTopic.Realm], Is.SupersetOf(PersonalityLines.All["joker"].Openers[eChatTopic.Realm]));
        Assert.That(lines.Ding, Has.Some.Contains("Swissland"));
        // A plain typed copy still gets only the type's lines.
        Assert.That(PersonalityReactions.LinesFor(joker with { Key = "joker#5", TypeKey = "joker" }), Is.SameAs(PersonalityLines.All["joker"]));
    }

    [Test]
    public void Named_IsNullWithoutAnEntry_AndEveryLineupBotHasAVoice()
    {
        foreach (NamedBotLineup.Entry entry in NamedBotLineup.All)
            Assert.That(PersonalityTypes.Named(entry), Is.Not.Null, entry.Name);
        Assert.That(PersonalityTypes.Named(null), Is.Null);
    }

    [Test]
    public void Mix_GivesOwnLinesAboutHalfThePool()
    {
        string[] type = Enumerable.Range(0, 12).Select(i => $"type {i}").ToArray();
        string[] own = ["own a", "own b", "own c"];
        string[] mixed = PersonalityLines.Mix(type, own);

        Assert.That(mixed, Is.SupersetOf(type).And.SupersetOf(own));
        double share = mixed.Count(line => line.StartsWith("own")) / (double)mixed.Length;
        Assert.That(share, Is.InRange(0.4, 0.6));
        Assert.That(PersonalityLines.Mix(type, []), Is.SameAs(type));
        Assert.That(PersonalityLines.Mix(type, null), Is.SameAs(type));
        Assert.That(PersonalityLines.Mix(null, own), Is.SameAs(own));
        // One own line never drowns a small type pool, and is never left out.
        Assert.That(PersonalityLines.Mix(["t"], ["o"]), Is.EqualTo(new[] { "t", "o" }));
    }

    [Test]
    public void FileBlocks_AreTheTypeOrItsNamedBots()
    {
        foreach (string key in PersonalityLines.TypeKeys)
        {
            var expected = NamedBotLineup.All.Where(entry => entry.TypeKey == key).Select(PersonalityLines.BlockName).Append(key);
            Assert.That(DialogueLibrary.For(key).BlockNames, Is.EquivalentTo(expected), key);
        }
    }

    private static IEnumerable<string> TypeLinesOf(string key)
    {
        TypeLines t = PersonalityLines.All[key];
        TypeTalk talk = PersonalityLines.Talk[key];
        return t.Openers.Values.Concat(t.TopicReplies.Values).SelectMany(pool => pool)
            .Concat(t.Ding).Concat(t.Grats).Concat(t.OwnDeath).Concat(t.OtherDeath).Concat(t.Drop).Concat(t.Cheeky ?? [])
            .Concat(talk.Ambient).Concat(talk.FallbackReplies).Concat(talk.Replies.Values.SelectMany(pool => pool))
            .Concat((talk.CheekyReplies ?? new Dictionary<eAutonomousChatIntent, string[]>()).Values.SelectMany(pool => pool));
    }
}
