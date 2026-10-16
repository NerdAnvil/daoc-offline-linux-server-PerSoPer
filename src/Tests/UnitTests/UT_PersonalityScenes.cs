using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PersonalityScenes
{
    private static readonly LineSlots Full = new("Myrkwood Forest", "a grass snake", 12, "Thane", "a Runed Helm");
    private static readonly LineSlots Empty = new(null, "", 0, null, null);
    private static readonly string[] Names = ["Alda", "Bryn", "Cato"];
    private static readonly HashSet<string> SceneTokens = ["{a}", "{b}", "{c}", "{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly HashSet<string> TopicTokens = ["{0}", "{zone}", "{monster}", "{level}", "{class}", "{item}"];
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}");

    private static IEnumerable<(string Pair, SceneScript Script)> AllScripts() =>
        PairScenes.All.SelectMany(pair => pair.Scenes.Select(script => ($"{pair.Lead}/{pair.Rival}", script)));

    // --- Sizes and pacing ---

    [Test]
    public void SizeFor_Is70Short_25Medium_5Large()
    {
        Assert.That(PersonalityScenes.SizeFor(0), Is.EqualTo(eSceneSize.Short));
        Assert.That(PersonalityScenes.SizeFor(0.69), Is.EqualTo(eSceneSize.Short));
        Assert.That(PersonalityScenes.SizeFor(0.70), Is.EqualTo(eSceneSize.Medium));
        Assert.That(PersonalityScenes.SizeFor(0.94), Is.EqualTo(eSceneSize.Medium));
        Assert.That(PersonalityScenes.SizeFor(0.95), Is.EqualTo(eSceneSize.Large));
        Assert.That(PersonalityScenes.SizeFor(0.999), Is.EqualTo(eSceneSize.Large));
    }

    [Test]
    public void MaxLines_Are4_8_12()
    {
        Assert.That(PersonalityScenes.MaxLines(eSceneSize.Short), Is.EqualTo(4));
        Assert.That(PersonalityScenes.MaxLines(eSceneSize.Medium), Is.EqualTo(8));
        Assert.That(PersonalityScenes.MaxLines(eSceneSize.Large), Is.EqualTo(12));
        Assert.That(PersonalityScenes.MaxLines(eSceneSize.Large), Is.LessThanOrEqualTo(PersonalityScenes.MaxSceneLines));
    }

    [Test]
    public void ContinueChance_FollowsVolumeAndChattiness()
    {
        Assert.That(PersonalityScenes.ContinueChance(80, 80, 0), Is.EqualTo(0));
        Assert.That(PersonalityScenes.ContinueChance(80, 70, 100), Is.GreaterThan(PersonalityScenes.ContinueChance(12, 50, 100)));
        Assert.That(PersonalityScenes.ContinueChance(80, 70, 50), Is.LessThan(PersonalityScenes.ContinueChance(80, 70, 100)));
        Assert.That(PersonalityScenes.ContinueChance(500, 500, 500), Is.InRange(0.0, 1.0));
    }

    [Test]
    public void LineDelay_StaysBetween2AndAHalfAnd8Seconds()
    {
        var random = new Random(3);
        foreach (string line in new[] { null, "", "Hi.", new string('x', 500) })
            Assert.That(PersonalityScenes.LineDelayMs(line, random), Is.InRange(2_500, 7_800));
        Assert.That(PersonalityScenes.LineDelayMs(new string('x', 120), new Random(1)),
            Is.GreaterThan(PersonalityScenes.LineDelayMs("Hi.", new Random(1))));
    }

    // --- Pairs and scripts ---

    [Test]
    public void Pairs_AreTheThreeRelationships_InEitherOrder()
    {
        Assert.That(PairScenes.For("hardcore", "casual")?.Lead, Is.EqualTo("hardcore"));
        Assert.That(PairScenes.For("casual", "hardcore")?.Lead, Is.EqualTo("hardcore"));
        Assert.That(PairScenes.For("leeroy", "minmaxer")?.Lead, Is.EqualTo("minmaxer"));
        Assert.That(PairScenes.For("loner", "joker")?.Lead, Is.EqualTo("joker"));
        Assert.That(PairScenes.For("hardcore", "joker"), Is.Null);
        Assert.That(PairScenes.For("joker", "joker"), Is.Null);
        Assert.That(PairScenes.RivalsOf("loner"), Is.EquivalentTo(new[] { "joker" }));
        Assert.That(PairScenes.RivalsOf("nobody"), Is.Empty);
        foreach (TypePair pair in PairScenes.All)
            Assert.That(PersonalityLines.All.Keys, Does.Contain(pair.Lead).And.Contain(pair.Rival));
    }

    [Test]
    public void EveryScript_IsWellFormed_AndFitsItsSize()
    {
        foreach (var (pair, script) in AllScripts())
        {
            string where = $"{pair} {script.Size}: {script.Lines.FirstOrDefault()?.Text}";
            Assert.That(script.Lines.Select(line => line.Speaker), Is.All.InRange(0, 2), where);
            Assert.That(script.Lines.Count, Is.InRange(PersonalityScenes.MinLines(script.Size), PersonalityScenes.MaxLines(script.Size)), where);
            Assert.That(script.Lines[0].Speaker, Is.EqualTo(0), where + " (A opens)");
            Assert.That(script.Lines.Select(line => line.Speaker), Does.Contain(1), where + " (B speaks)");
            for (int i = 1; i < script.Lines.Count; i++)
                Assert.That(script.Lines[i].Speaker, Is.Not.EqualTo(script.Lines[i - 1].Speaker), where + " (same speaker twice)");
            foreach (SceneLine line in script.Lines)
            foreach (Match m in Placeholder.Matches(line.Text))
                Assert.That(SceneTokens, Does.Contain(m.Value), $"{where} -> {line.Text}");
            List<SceneLine> filled = PersonalityScenes.FillScript(script, Names, Full);
            Assert.That(filled, Is.Not.Null, where);
            Assert.That(filled.Select(line => line.Text), Is.All.Not.Contain("{").And.Not.Contain("}"), where);
        }
    }

    [Test]
    public void EveryPair_HasAShortScene_WithoutAThirdSpeakerOrCheek()
    {
        foreach (TypePair pair in PairScenes.All)
            Assert.That(pair.Scenes.Any(s => s.Size == eSceneSize.Short && s.Speakers == 2 && !s.Cheeky), pair.Lead);
    }

    [Test]
    public void PickScript_FallsBackToSmaller_AndRespectsThirdAndToneDown()
    {
        SceneScript shortTwo = SceneScript.Of(eSceneSize.Short, "A: hi {b}", "B: hi {a}");
        SceneScript mediumThree = SceneScript.Of(eSceneSize.Medium, "A: 1", "B: 2", "A: 3", "C: 4", "B: 5");
        SceneScript cheekyShort = SceneScript.CheekyOf(eSceneSize.Short, "A: cheeky", "B: cheeky back");
        var random = new Random(4);

        Assert.That(PersonalityScenes.PickScript([shortTwo, mediumThree], eSceneSize.Large, true, true, random), Is.SameAs(mediumThree));
        Assert.That(PersonalityScenes.PickScript([shortTwo, mediumThree], eSceneSize.Medium, false, true, random), Is.SameAs(shortTwo));
        for (int i = 0; i < 50; i++)
            Assert.That(PersonalityScenes.PickScript([shortTwo, cheekyShort], eSceneSize.Short, false, true, random), Is.SameAs(shortTwo));
        Assert.That(PersonalityScenes.PickScript([cheekyShort], eSceneSize.Short, false, false, random), Is.SameAs(cheekyShort));
        Assert.That(PersonalityScenes.PickScript([], eSceneSize.Short, true, false, random), Is.Null);
        Assert.That(PersonalityScenes.PickScript(null, eSceneSize.Short, true, false, random), Is.Null);
    }

    [Test]
    public void FillScript_FillsNamesAndSlots_OrSkipsTheScene()
    {
        SceneScript script = SceneScript.Of(eSceneSize.Short, "A: {b}, {zone} again?", "B: Yes, {a}.");
        List<SceneLine> lines = PersonalityScenes.FillScript(script, Names, Full);
        Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { "Bryn, Myrkwood Forest again?", "Yes, Alda." }));
        Assert.That(lines.Select(line => line.Speaker), Is.EqualTo(new[] { 0, 1 }));

        Assert.That(PersonalityScenes.FillScript(script, Names, Empty), Is.Null, "unknown slot");
        SceneScript withC = SceneScript.Of(eSceneSize.Short, "A: hi", "B: hi", "C: me too, {a}");
        Assert.That(PersonalityScenes.FillScript(withC, ["Alda", "Bryn"], Full), Is.Null, "no third speaker");
        Assert.That(PersonalityScenes.FillScript(withC, ["Alda", "Bryn", " "], Full), Is.Null, "blank third name");
        SceneScript tooLong = SceneScript.Of(eSceneSize.Short, "A: 1", "B: 2", "A: 3", "B: 4", "A: 5");
        Assert.That(PersonalityScenes.FillScript(tooLong, Names, Full), Is.Null, "over the size cap");
        Assert.That(SceneScript.Of(eSceneSize.Short, "X: bad").Lines[0].Speaker, Is.EqualTo(-1));
    }

    // --- Topic scenes ---

    [Test]
    public void EveryType_HasSceneLines_WithOnlyKnownPlaceholders()
    {
        Assert.That(SceneLines.All.Keys, Is.EquivalentTo(PersonalityLines.All.Keys));
        foreach (var (key, lines) in SceneLines.All)
        {
            Assert.That(lines.Closers, Is.Not.Empty, key);
            IEnumerable<string> all = lines.Comebacks.Values.SelectMany(pool => pool).Concat(lines.Closers).Concat(lines.CheekyClosers ?? []);
            foreach (string line in all)
            {
                foreach (Match m in Placeholder.Matches(line))
                    Assert.That(TopicTokens, Does.Contain(m.Value), $"{key}: {line}");
                Assert.That(PersonalityReactions.Fill(line, Full, "Bryn"), Does.Not.Contain("{"), $"{key}: {line}");
            }
        }
    }

    [Test]
    public void EveryType_HasComebacksForEveryTopic()
    {
        foreach (var (key, lines) in SceneLines.All)
        foreach (eChatTopic topic in Enum.GetValues<eChatTopic>())
            Assert.That(lines.Comebacks.GetValueOrDefault(topic), Has.Length.GreaterThanOrEqualTo(3), $"{key} {topic}");
    }

    [Test]
    public void PlanTopicScene_StaysInSize_AlternatesSpeakers_AndNeverRepeats()
    {
        var random = new Random(5);
        string[] keys = PersonalityLines.All.Keys.ToArray();
        foreach (string a in keys)
        foreach (string b in keys)
        foreach (eSceneSize size in Enum.GetValues<eSceneSize>())
        for (int i = 0; i < 10; i++)
        {
            bool third = i % 2 == 0;
            var voices = new List<SceneVoice> { new("Alda", a, Full), new("Bryn", b, Full) };
            if (third)
                voices.Add(new SceneVoice("Cato", keys[i % keys.Length], Full));
            List<SceneLine> lines = PersonalityScenes.PlanTopicScene(size, eChatTopic.Xp, voices, toneDown: true, random);
            string where = $"{a}/{b} {size} third={third}";
            // The opener is line 1, so a Short scene adds at most 3 and topic scenes never go past Medium.
            Assert.That(lines.Count, Is.LessThanOrEqualTo(size == eSceneSize.Short ? 3 : 7), where);
            Assert.That(lines.Count, Is.GreaterThanOrEqualTo(1), where + " (B always replies)");
            Assert.That(lines[0].Speaker, Is.EqualTo(1), where);
            Assert.That(lines.Select(line => line.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(lines.Count), where);
            Assert.That(lines.Select(line => line.Text), Is.All.Not.Contain("{"), where);
            int previous = 0;
            foreach (SceneLine line in lines)
            {
                Assert.That(line.Speaker, Is.Not.EqualTo(previous), where + " (same speaker twice)");
                previous = line.Speaker;
            }
            if (!third)
                Assert.That(lines.Select(line => line.Speaker), Is.All.LessThan(2), where);
        }
    }

    [Test]
    public void PlanTopicScene_AddressesTheOtherByName()
    {
        // Every Hardcore Xp comeback names {0}; when A comes back it names B.
        var voices = new List<SceneVoice> { new("Alda", "hardcore", Full), new("Bryn", "casual", Full) };
        var random = new Random(6);
        for (int i = 0; i < 50; i++)
        {
            List<SceneLine> lines = PersonalityScenes.PlanTopicScene(eSceneSize.Short, eChatTopic.Xp, voices, true, random);
            if (lines.Count >= 2)
                Assert.That(lines[1].Text, Does.Contain("Bryn"));
        }
    }

    [Test]
    public void PlanTopicScene_NeedsTwoVoices_AndTypedLines()
    {
        var random = new Random(7);
        Assert.That(PersonalityScenes.PlanTopicScene(eSceneSize.Short, eChatTopic.Xp, null, true, random), Is.Empty);
        Assert.That(PersonalityScenes.PlanTopicScene(eSceneSize.Short, eChatTopic.Xp, [new("Alda", "joker", Full)], true, random), Is.Empty);
        Assert.That(PersonalityScenes.PlanTopicScene(eSceneSize.Medium, eChatTopic.Xp,
            [new("Alda", "joker", Full), new("Bryn", "nobody", Full)], true, random), Is.Empty);
    }

    [Test]
    public void CheekyClosers_OnlyWithToneDownOff()
    {
        var cheeky = SceneLines.All.Values.SelectMany(lines => lines.CheekyClosers ?? []).ToHashSet();
        Assume.That(cheeky, Is.Not.Empty);
        var random = new Random(8);
        foreach (string a in SceneLines.All.Keys)
        foreach (string b in SceneLines.All.Keys)
        for (int i = 0; i < 40; i++)
        {
            var voices = new List<SceneVoice> { new("Alda", a, Full), new("Bryn", b, Full) };
            foreach (SceneLine line in PersonalityScenes.PlanTopicScene(eSceneSize.Medium, eChatTopic.Gear, voices, toneDown: true, random))
                Assert.That(cheeky.Any(raw => PersonalityReactions.Fill(raw, Full, "Alda") == line.Text ||
                    PersonalityReactions.Fill(raw, Full, "Bryn") == line.Text), Is.False, line.Text);
        }
    }
}
