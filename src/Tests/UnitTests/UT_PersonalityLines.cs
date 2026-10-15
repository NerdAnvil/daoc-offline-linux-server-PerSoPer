using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityLines
{
    private static readonly string[] Keys = ["hardcore", "casual", "minmaxer", "joker", "loner", "leeroy"];
    private static readonly HashSet<string> Allowed = ["{0}", "{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    private static IEnumerable<(string Key, string Section, string Line)> AllLines(string key, TypeLines t)
    {
        foreach (var (topic, lines) in t.Openers)
            foreach (string line in lines) yield return (key, $"Openers.{topic}", line);
        foreach (var (topic, lines) in t.TopicReplies)
            foreach (string line in lines) yield return (key, $"TopicReplies.{topic}", line);
        foreach (string line in t.Ding) yield return (key, "Ding", line);
        foreach (string line in t.Grats) yield return (key, "Grats", line);
        foreach (string line in t.OwnDeath) yield return (key, "OwnDeath", line);
        foreach (string line in t.OtherDeath) yield return (key, "OtherDeath", line);
        foreach (string line in t.Drop) yield return (key, "Drop", line);
        foreach (string line in t.Cheeky ?? Array.Empty<string>()) yield return (key, "Cheeky", line);
    }

    [Test]
    public void AllSixTypes_Present()
    {
        Assert.That(PersonalityLines.All.Keys, Is.EquivalentTo(Keys));
    }

    [Test]
    public void EveryType_HasEnoughLinesPerTopicAndEvent()
    {
        foreach (var (key, t) in PersonalityLines.All)
        {
            foreach (eChatTopic topic in Enum.GetValues<eChatTopic>())
            {
                Assert.That(t.Openers.TryGetValue(topic, out string[] openers) ? openers.Length : 0, Is.GreaterThanOrEqualTo(10), $"{key} Openers.{topic}");
                Assert.That(t.TopicReplies.TryGetValue(topic, out string[] replies) ? replies.Length : 0, Is.GreaterThanOrEqualTo(4), $"{key} TopicReplies.{topic}");
            }
            Assert.That(t.Ding.Length, Is.GreaterThanOrEqualTo(6), $"{key} Ding");
            Assert.That(t.Grats.Length, Is.GreaterThanOrEqualTo(6), $"{key} Grats");
            Assert.That(t.OwnDeath.Length, Is.GreaterThanOrEqualTo(6), $"{key} OwnDeath");
            Assert.That(t.OtherDeath.Length, Is.GreaterThanOrEqualTo(6), $"{key} OtherDeath");
            Assert.That(t.Drop.Length, Is.GreaterThanOrEqualTo(6), $"{key} Drop");
        }
    }

    [Test]
    public void OnlyJokerAndLeeroy_HaveCheekyLines()
    {
        foreach (var (key, t) in PersonalityLines.All)
        {
            if (key is "joker" or "leeroy")
                Assert.That(t.Cheeky, Is.Not.Empty, key);
            else
                Assert.That(t.Cheeky, Is.Null, key);
        }
    }

    [Test]
    public void NoDuplicates_WithinAType()
    {
        foreach (var (key, t) in PersonalityLines.All)
        {
            var duplicates = AllLines(key, t)
                .GroupBy(l => l.Line.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Assert.That(duplicates, Is.Empty, key);
        }
    }

    [Test]
    public void EveryLine_IsOneShortLine()
    {
        foreach (var (key, t) in PersonalityLines.All)
        foreach (var (_, section, line) in AllLines(key, t))
        {
            Assert.That(line, Is.Not.Empty.And.Not.Contain("\n"), $"{key} {section}");
            Assert.That(line.Length, Is.LessThan(150), $"{key} {section}: {line}");
        }
    }

    [Test]
    public void OnlyAllowedPlaceholders_AndNameOnlyWhereStated()
    {
        foreach (var (key, t) in PersonalityLines.All)
        foreach (var (_, section, line) in AllLines(key, t))
        {
            foreach (Match m in Placeholder.Matches(line))
                Assert.That(Allowed, Does.Contain(m.Value), $"{key} {section}: {line}");
            string rest = Placeholder.Replace(line, "");
            Assert.That(rest, Does.Not.Contain("{").And.Not.Contain("}"), $"{key} {section}: stray brace in {line}");

            bool namesOther = section.StartsWith("TopicReplies.") || section is "Grats" or "OtherDeath";
            if (namesOther)
                Assert.That(line, Does.Contain("{0}"), $"{key} {section}: {line}");
            else
                Assert.That(line, Does.Not.Contain("{0}"), $"{key} {section}: {line}");
        }
    }
}
