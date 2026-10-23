using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Step 5 of the named bots: the three Crafters (Muckaduck and her realm twins) have a voice.</summary>
[TestFixture]
public class UT_CrafterVoice
{
    private static readonly string[] Names = ["Muckaduck", "Muckaducke", "Muckaduckeen"];
    private static readonly HashSet<string> Slots = ["{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");
    private static readonly Regex Riches = new("gold|coin|rich|fortune|wealth|profit|vault|pouch", RegexOptions.IgnoreCase);

    [Test]
    public void Crafter_IsNamedOnly_NeverRolledForGeneratedBots()
    {
        Assert.That(PersonalityTypes.Templates.ContainsKey("crafter"), Is.False);
        Assert.That(PersonalityTypes.Keys, Does.Not.Contain("crafter"));
        Assert.That(PersonalityTypes.Normalise("crafter"), Is.Null);
        Assert.That(PersonalityLines.All.ContainsKey("crafter"), Is.False);
        Assert.That(PersonalityTypeVoices.NamedOnly.ContainsKey("crafter"), Is.True);
        var random = new Random(7);
        for (int i = 0; i < 500; i++)
            Assert.That(PersonalityTypes.Roll(random), Is.Not.EqualTo("crafter"));
    }

    [Test]
    public void CrafterFile_LoadsWithoutErrors_AndHoldsOnlyTheTypeAndItsThreeBots()
    {
        Assert.That(PersonalityLines.Errors, Is.Empty);
        DialogueFile file = DialogueLibrary.For("crafter");
        Assert.That(file, Is.Not.Null);
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.BlockNames, Is.EquivalentTo(Names.Select(name => name.ToLowerInvariant()).Append("crafter")));
    }

    [TestCase("Muckaduck")]
    [TestCase("Muckaducke")]
    [TestCase("Muckaduckeen")]
    public void Named_IsTheCrafterUnderItsOwnNameAndMemoryKey(string name)
    {
        NamedBotLineup.Entry entry = NamedBotLineup.FindByName(name);
        BotPersonality named = PersonalityTypes.Named(entry);
        BotPersonality crafter = PersonalityTypeVoices.NamedOnly["crafter"];

        Assert.That(named, Is.Not.Null);
        Assert.That(named.Name, Is.EqualTo(name));
        Assert.That(named.Key, Is.EqualTo("named:" + name.ToLowerInvariant()));
        Assert.That(named.TypeKey, Is.EqualTo("crafter"));
        Assert.That(named.Matches(null), Is.False);
        Assert.That(named.Patience, Is.SameAs(crafter.Patience));
        Assert.That(named.Ambient, Is.SupersetOf(crafter.Ambient));
        Assert.That(named.Ambient.Length, Is.GreaterThan(crafter.Ambient.Length));
        TypeLines lines = PersonalityReactions.LinesFor(named);
        Assert.That(lines, Is.Not.Null);
        Assert.That(lines.Openers.Values.Sum(pool => pool.Length), Is.GreaterThan(PersonalityLines.Own[named.Key].Lines.Openers.Values.Sum(pool => pool.Length)));
    }

    [TestCase("Muckaduck")]
    [TestCase("Muckaducke")]
    [TestCase("Muckaduckeen")]
    public void CraftRequest_GetsAnExcuse_NeverAnOffer(string name)
    {
        BotPersonality named = PersonalityTypes.Named(NamedBotLineup.FindByName(name));
        foreach (eAutonomousChatIntent intent in new[] { eAutonomousChatIntent.Trading, eAutonomousChatIntent.Help })
        {
            string[] pool = named.Replies[intent];
            Assert.That(pool, Is.Not.Empty, $"{name} {intent}");
            // Her own replies sit on top of the type's, and each one declines in some way.
            string[] own = PersonalityLines.Own[named.Key].Talk.Replies[intent];
            Assert.That(own, Is.Not.Empty, $"{name} {intent} own");
        }
        string[] trading = PersonalityLines.Own[named.Key].Talk.Replies[eAutonomousChatIntent.Trading];
        Assert.That(trading, Has.Some.Matches<string>(line => line.Contains("craft", StringComparison.OrdinalIgnoreCase)));
        Assert.That(trading, Has.None.Matches<string>(line => Regex.IsMatch(line, @"\b(sure|yes|of course|happy to)\b", RegexOptions.IgnoreCase)));
    }

    [TestCase("Muckaduck")]
    [TestCase("Muckaducke")]
    [TestCase("Muckaduckeen")]
    public void Riches_AreInTheirOwnAmbientAndOpeners(string name)
    {
        (TypeLines lines, TypeTalk talk) = PersonalityLines.Own["named:" + name.ToLowerInvariant()];
        string[] spoken = talk.Ambient.Concat(lines.Openers.Values.SelectMany(pool => pool)).ToArray();
        Assert.That(spoken.Count(line => Riches.IsMatch(line)), Is.GreaterThanOrEqualTo(3), name);
        Assert.That(spoken.Count(line => line.Contains("craft", StringComparison.OrdinalIgnoreCase)), Is.GreaterThanOrEqualTo(1), name);
    }

    [Test]
    public void TypeVoice_BoastsAboutRiches_AndDeclinesCrafting()
    {
        TypeTalk talk = PersonalityLines.TalkFor("crafter");
        Assert.That(talk.Ambient.Count(line => Riches.IsMatch(line)), Is.GreaterThanOrEqualTo(3));
        Assert.That(talk.Replies.Keys, Does.Contain(eAutonomousChatIntent.Greeting));
        Assert.That(talk.Replies[eAutonomousChatIntent.Trading], Has.Some.Contains("Craft you something"));
        Assert.That(talk.FallbackReplies, Is.Not.Empty);
        Assert.That(PersonalityTypeVoices.NamedOnly["crafter"].CheekyReplies, Is.Null);
    }

    [Test]
    public void OwnLines_AreShort_UseOnlyTheirPlaceholders_AndAreNew()
    {
        TypeTalk typeTalk = PersonalityLines.TalkFor("crafter");
        var typeLines = new HashSet<string>(typeTalk.Ambient.Concat(typeTalk.FallbackReplies)
            .Concat(typeTalk.Replies.Values.SelectMany(pool => pool)), StringComparer.OrdinalIgnoreCase);
        foreach (string name in Names)
        {
            (TypeLines lines, TypeTalk talk) = PersonalityLines.Own["named:" + name.ToLowerInvariant()];
            var all = new List<(string Where, string Line, bool SlotsOk)>();
            all.AddRange(talk.Ambient.Select(line => ($"{name} ambient", line, false)));
            all.AddRange(talk.Replies.SelectMany(pair => pair.Value.Select(line => ($"{name} reply {pair.Key}", line, false))));
            all.AddRange(lines.Openers.SelectMany(pair => pair.Value.Select(line => ($"{name} opener {pair.Key}", line, true))));
            all.AddRange(lines.Ding.Concat(lines.OwnDeath).Concat(lines.Drop).Select(line => ($"{name} event", line, true)));

            Assert.That(talk.Ambient.Length, Is.GreaterThanOrEqualTo(5), $"{name} ambient");
            Assert.That(lines.Openers.Values.Sum(pool => pool.Length), Is.GreaterThanOrEqualTo(6), $"{name} openers");
            Assert.That(talk.Replies.ContainsKey(eAutonomousChatIntent.Greeting), $"{name} greeting");
            Assert.That(lines.Ding, Is.Not.Empty, $"{name} ding");
            Assert.That(lines.OwnDeath, Is.Not.Empty, $"{name} own_death");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (where, line, slotsOk) in all)
            {
                Assert.That(line.Length, Is.InRange(1, 149), $"{where}: {line}");
                Assert.That(seen.Add(line), $"{where}: said twice: {line}");
                Assert.That(typeLines, Does.Not.Contain(line), $"{where}: copies a crafter line");
                foreach (Match m in Placeholder.Matches(line))
                    Assert.That(m.Value == "{0}" || slotsOk && Slots.Contains(m.Value), $"{where}: {m.Value} in {line}");
                Assert.That(Placeholder.Replace(line, ""), Does.Not.Contain("{").And.Not.Contain("}"), $"{where}: stray brace");
            }
        }
    }
}
