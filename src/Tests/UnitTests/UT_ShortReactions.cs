using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Intent Part B2: "lol", "sorry", "brb", "back" and "nah" get a short reaction from one bot now
/// and then, from that intent's own lines, and otherwise no answer at all.
/// </summary>
[TestFixture]
public class UT_ShortReactions
{
    private static readonly eAutonomousChatIntent[] Reactions =
    [
        eAutonomousChatIntent.Laugh, eAutonomousChatIntent.Sorry, eAutonomousChatIntent.Disagree,
        eAutonomousChatIntent.Away, eAutonomousChatIntent.Back,
    ];

    [TestCase("lol", eAutonomousChatIntent.Laugh)]
    [TestCase("haha", eAutonomousChatIntent.Laugh)]
    [TestCase("sorry", eAutonomousChatIntent.Sorry)]
    [TestCase("sry", eAutonomousChatIntent.Sorry)]
    [TestCase("nah", eAutonomousChatIntent.Disagree)]
    [TestCase("brb", eAutonomousChatIntent.Away)]
    [TestCase("afk a sec", eAutonomousChatIntent.Away)]
    [TestCase("back", eAutonomousChatIntent.Back)]
    [TestCase("Back!", eAutonomousChatIntent.Back)]
    [TestCase("ok back", eAutonomousChatIntent.Back)]
    [TestCase("im back", eAutonomousChatIntent.Back)]
    public void ShortLines_AreReadAsReactions(string line, eAutonomousChatIntent expected)
    {
        Assert.That(AutonomousChatIntentModel.Read(line), Is.EqualTo(expected));
        Assert.That(AutonomousBotChatCoordinator.IsShortReaction(expected), Is.True);
    }

    [TestCase("back home we'd call that a storm")]
    [TestCase("fall back")]
    public void BackInsideALine_IsNotComingBack(string line)
    {
        Assert.That(AutonomousChatIntentModel.Read(line), Is.Not.EqualTo(eAutonomousChatIntent.Back));
    }

    [Test]
    public void OtherIntents_AreNotReactions()
    {
        Assert.That(AutonomousBotChatCoordinator.IsShortReaction(eAutonomousChatIntent.Joke), Is.False);
        Assert.That(AutonomousBotChatCoordinator.IsShortReaction(eAutonomousChatIntent.Greeting), Is.False);
        Assert.That(AutonomousBotChatCoordinator.IsShortReaction(eAutonomousChatIntent.Agree), Is.False);
    }

    // Every type, Persoper, Kobaldjovi and every named character (mixed over its type) has a few brief lines for each.
    [Test]
    public void EveryPersonality_HasBriefLinesForEachReaction()
    {
        string[] names = PersonalityTypes.Templates.Keys.Concat(["Persoper", "Kobaldjovi"])
            .Concat(NamedBotLineup.All.Select(entry => entry.Name)).ToArray();
        foreach (string name in names)
        {
            BotPersonality personality = DialoguePreview.Find(name);
            Assert.That(personality, Is.Not.Null, name);
            foreach (eAutonomousChatIntent intent in Reactions)
            {
                string[] lines = personality.Replies.GetValueOrDefault(intent);
                Assert.That(lines, Is.Not.Null.And.Length.AtLeast(2), $"{name} {intent}");
                Assert.That(lines.Where(line => line.Length > 60), Is.Empty, $"{name} {intent}: keep reactions brief");
            }
        }
    }

    [Test]
    public void Laugh_IsAnsweredWithALaughLine_NeverAJoke()
    {
        BotPersonality persoper = DialoguePreview.Find("Persoper");
        string[] laughs = persoper.Replies[eAutonomousChatIntent.Laugh].Select(line => string.Format(line, "Tester")).ToArray();
        Random random = new(7);
        int said = 0;
        for (int i = 0; i < 200; i++)
        {
            string reaction = AutonomousBotChatCoordinator.ShortReaction(persoper, eAutonomousChatIntent.Laugh, true, "Tester", random, null);
            if (reaction == null)
                continue;
            said++;
            Assert.That(laughs, Does.Contain(reaction));
        }
        Assert.That(said, Is.GreaterThan(0));
    }

    // Rare, not every time: about 1 in 4, or 1 in 2 when the bot is named (or it's a tell).
    [TestCase(false, AutonomousBotChatCoordinator.ReactionChance)]
    [TestCase(true, AutonomousBotChatCoordinator.NamedReactionChance)]
    public void Reactions_ComeNowAndThen(bool named, double chance)
    {
        BotPersonality casual = DialoguePreview.Find("casual");
        Random random = new(11);
        const int tries = 2000;
        int said = Enumerable.Range(0, tries).Count(_ =>
            AutonomousBotChatCoordinator.ShortReaction(casual, eAutonomousChatIntent.Back, named, "Tester", random, null) != null);
        Assert.That(said / (double)tries, Is.EqualTo(chance).Within(0.06));
    }

    [Test]
    public void NoLinesForTheReaction_MeansSilence()
    {
        BotPersonality bare = new() { Key = "bare", Name = "Bare", Matches = _ => false, Replies = new Dictionary<eAutonomousChatIntent, string[]>(), FallbackReplies = ["You've lost me."] };
        Random random = new(3);
        for (int i = 0; i < 50; i++)
            Assert.That(AutonomousBotChatCoordinator.ShortReaction(bare, eAutonomousChatIntent.Laugh, true, "Tester", random, null), Is.Null);
        Assert.That(AutonomousBotChatCoordinator.ShortReaction(null, eAutonomousChatIntent.Laugh, true, "Tester", random, null), Is.Null);
    }
}
