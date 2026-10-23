using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>Steps 7 to 9 of the named bots: Homeowner, Herbal Doctor and Gruff Doc, nine bots in all.</summary>
[TestFixture]
public class UT_LastThreeVoices
{
    private static readonly string[] Types = ["homeowner", "herbaldoctor", "gruffdoc"];

    private static IEnumerable<NamedBotLineup.Entry> Bots(string type) => NamedBotLineup.All.Where(entry => entry.TypeKey == type);

    private static string[] OwnSpoken(string name)
    {
        (TypeLines lines, TypeTalk talk) = PersonalityLines.Own["named:" + name.ToLowerInvariant()];
        return talk.Ambient.Concat(talk.FallbackReplies).Concat(talk.Replies.Values.SelectMany(pool => pool))
            .Concat(lines.Openers.Values.SelectMany(pool => pool))
            .Concat(lines.Ding).Concat(lines.Grats).Concat(lines.OwnDeath).Concat(lines.OtherDeath).Concat(lines.Drop).ToArray();
    }

    [TestCase("homeowner")]
    [TestCase("herbaldoctor")]
    [TestCase("gruffdoc")]
    public void Type_IsNamedOnly_NeverRolledForGeneratedBots(string type)
    {
        Assert.That(PersonalityTypes.Templates.ContainsKey(type), Is.False);
        Assert.That(PersonalityTypes.Keys, Does.Not.Contain(type));
        Assert.That(PersonalityTypes.Normalise(type), Is.Null);
        Assert.That(PersonalityLines.All.ContainsKey(type), Is.False);
        Assert.That(PersonalityTypeVoices.NamedOnly.ContainsKey(type), Is.True);
    }

    [TestCase("homeowner")]
    [TestCase("herbaldoctor")]
    [TestCase("gruffdoc")]
    public void File_LoadsWithoutErrors_AndHoldsOnlyTheTypeAndItsThreeBots(string type)
    {
        Assert.That(PersonalityLines.Errors, Is.Empty);
        DialogueFile file = DialogueLibrary.For(type);
        Assert.That(file, Is.Not.Null);
        Assert.That(file.Errors, Is.Empty);
        Assert.That(file.BlockNames, Is.EquivalentTo(Bots(type).Select(entry => entry.Name.ToLowerInvariant()).Append(type)));
        Assert.That(Bots(type).Count(), Is.EqualTo(3));
    }

    [Test]
    public void Named_IsTheTypeUnderItsOwnNameAndMemoryKey_ForAllNineBots()
    {
        foreach (string type in Types)
            foreach (NamedBotLineup.Entry entry in Bots(type))
            {
                BotPersonality named = PersonalityTypes.Named(entry);
                BotPersonality template = PersonalityTypeVoices.NamedOnly[type];

                Assert.That(named, Is.Not.Null, entry.Name);
                Assert.That(named.Name, Is.EqualTo(entry.Name));
                Assert.That(named.Key, Is.EqualTo("named:" + entry.Name.ToLowerInvariant()));
                Assert.That(named.TypeKey, Is.EqualTo(type));
                Assert.That(named.Matches(null), Is.False);
                Assert.That(named.Patience, Is.SameAs(template.Patience));
                Assert.That(named.Ambient, Is.SupersetOf(template.Ambient));
                Assert.That(named.Ambient.Length, Is.GreaterThan(template.Ambient.Length), entry.Name);
                Assert.That(PersonalityReactions.LinesFor(named), Is.Not.Null);
                Assert.That(OwnSpoken(entry.Name), Is.Not.Empty, entry.Name);
            }
    }

    [Test]
    public void Homeowner_SaysDontchaKnow_AndInvitesYouOver()
    {
        foreach (NamedBotLineup.Entry entry in Bots("homeowner"))
        {
            string[] spoken = PersonalityTypes.Named(entry).Ambient.Concat(PersonalityLines.NamedTalkFor("named:" + entry.Name.ToLowerInvariant()).Replies.Values.SelectMany(pool => pool)).ToArray();
            Assert.That(spoken, Has.Some.Matches<string>(line => line.Contains("dontcha know", System.StringComparison.OrdinalIgnoreCase)), entry.Name);
            Assert.That(spoken, Has.Some.Matches<string>(line => line.Contains("house", System.StringComparison.OrdinalIgnoreCase)), entry.Name);
        }
    }

    [Test]
    public void HerbalDoctor_SaysBub_AndAsksIfYouReallyGetBuffing()
    {
        foreach (NamedBotLineup.Entry entry in Bots("herbaldoctor"))
        {
            string[] spoken = PersonalityTypes.Named(entry).Ambient.Concat(PersonalityLines.NamedTalkFor("named:" + entry.Name.ToLowerInvariant()).Replies.Values.SelectMany(pool => pool)).ToArray();
            Assert.That(spoken, Has.Some.Matches<string>(line => line.Contains("bub", System.StringComparison.OrdinalIgnoreCase)), entry.Name);
            Assert.That(OwnSpoken(entry.Name), Has.Some.Matches<string>(line => line.Contains("buff", System.StringComparison.OrdinalIgnoreCase)), entry.Name);
        }
    }

    [Test]
    public void GruffDoc_GrumblesLikeAMedic()
    {
        foreach (NamedBotLineup.Entry entry in Bots("gruffdoc"))
        {
            string[] spoken = PersonalityTypes.Named(entry).Ambient.Concat(PersonalityLines.NamedTalkFor("named:" + entry.Name.ToLowerInvariant()).Replies.Values.SelectMany(pool => pool)).ToArray();
            Assert.That(spoken, Has.Some.Matches<string>(line => line.Contains("patch", System.StringComparison.OrdinalIgnoreCase) || line.Contains("Sit still", System.StringComparison.OrdinalIgnoreCase)), entry.Name);
            Assert.That(OwnSpoken(entry.Name), Is.Not.Empty, entry.Name);
        }
    }

    [Test]
    public void BuildLines_NameThePlayer_NeverPrintALiteralZero()
    {
        foreach ((string type, BotPersonality personality) in PersonalityTypeVoices.NamedOnly.Concat(PersonalityTypes.Templates))
            foreach (eSpecType spec in System.Enum.GetValues<eSpecType>())
            {
                if (personality.Build.Phrase(spec) == null)
                    continue;
                string[] lines = personality.Build.Lines(spec).Select(line => string.Format(line, "Zed")).ToArray();
                Assert.That(lines, Has.None.Matches<string>(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"\b0\b")), type);
            }
    }
}
