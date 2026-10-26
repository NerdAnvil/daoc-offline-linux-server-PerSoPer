using System;
using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_BotPersonality
{
    private static readonly eAutonomousChatIntent[] Intents = Enum.GetValues<eAutonomousChatIntent>();

    [Test]
    public void EveryPersonality_RepliesToEveryIntentWithTheSpeakersName()
    {
        foreach (BotPersonality personality in BotPersonalities.All)
        foreach (eAutonomousChatIntent intent in Intents)
        foreach (bool toneDown in new[] { true, false })
        {
            var random = new Random(7);
            for (int i = 0; i < 20; i++)
            {
                string reply = personality.Reply(intent, "Tester", toneDown, random);
                Assert.That(reply, Is.Not.Empty, $"{personality.Name} {intent}");
                Assert.That(reply, Does.Not.Contain("{"), $"{personality.Name} {intent}: unfilled placeholder");
            }
        }
    }

    [Test]
    public void SmallTalkWithoutItsOwnLines_GetsBanter_NotTheFallback()
    {
        foreach (BotPersonality personality in BotPersonalities.All)
        foreach (eAutonomousChatIntent intent in Intents.Where(AutonomousChatIntentModel.IsSmallTalk))
        {
            if (personality.Replies.ContainsKey(intent) || !personality.Replies.TryGetValue(eAutonomousChatIntent.Banter, out string[] banter))
                continue;
            string[] fallback = personality.FallbackReplies.Except(banter).Select(line => string.Format(line, "Tester")).ToArray();
            var random = new Random(5);
            for (int i = 0; i < 20; i++)
                Assert.That(fallback, Does.Not.Contain(personality.Reply(intent, "Tester", true, random)), $"{personality.Name} {intent}");
        }
    }

    [Test]
    public void Replies_DontTalkAboutTheSpeakerAsSomeoneElse()
    {
        string[] lines = ["Ask Eaglefang, {0}.", "Mostly from Jovi, {0}.", "Good one, {0}."];
        Assert.That(BotPersonality.NotNaming(lines, "Eaglefang"), Is.EqualTo(new[] { "Mostly from Jovi, {0}.", "Good one, {0}." }));
        Assert.That(BotPersonality.NotNaming(lines, "Kobaldjovi"), Is.EqualTo(new[] { "Ask Eaglefang, {0}.", "Good one, {0}." }));
        Assert.That(BotPersonality.NotNaming(lines, "Persoper Mendsalot"), Is.EqualTo(lines));
        Assert.That(BotPersonality.NotNaming(["Ask Eaglefang, {0}."], "Eaglefang"), Is.EqualTo(new[] { "Ask Eaglefang, {0}." }), "never leaves nothing");
    }

    [Test]
    public void Reply_UsesFriendWhenSpeakerIsUnknown()
    {
        string reply = KobaldjoviIdentity.Personality.Reply(eAutonomousChatIntent.Greeting, null, true, new Random(1));
        Assert.That(reply, Does.Not.Contain("{0}"));
    }

    [Test]
    public void ToneDown_NeverUsesCheekyLines()
    {
        BotPersonality persoper = PersoperIdentity.Personality;
        string[] cheeky = persoper.CheekyReplies.Values.SelectMany(lines => lines)
            .Select(line => string.Format(line, "Tester")).ToArray();
        var random = new Random(3);
        foreach (eAutonomousChatIntent intent in Intents)
        {
            for (int i = 0; i < 50; i++)
                Assert.That(cheeky, Does.Not.Contain(persoper.Reply(intent, "Tester", true, random)));
        }
    }

    [TestCase("Persoper Healsalot", "Persoper", "persoper", true)]
    [TestCase("Persoper Healsalot", "Persoper", "Persoper Healsalot", true)]
    [TestCase("Persoper Healsalot", "Persoper", " PERSOPER ", true)]
    [TestCase("Kobaldjovi", "Kobaldjovi", "kobaldjovi", true)]
    [TestCase("Kobaldjovi", "Kobaldjovi", "Kobald", false)]
    [TestCase("Persoper Healsalot", "Persoper", "Healsalot", false)]
    [TestCase("Kobaldjovi", "Kobaldjovi", "", false)]
    [TestCase("Kobaldjovi", "Kobaldjovi", null, false)]
    public void TellName_MatchesFullOrPersonalityName(string botName, string personalityName, string typed, bool expected)
    {
        Assert.That(AutonomousBotChatCoordinator.IsPersonalityName(botName, personalityName, typed), Is.EqualTo(expected));
    }

    [Test]
    public void Personalities_HaveDistinctNames()
    {
        Assert.That(BotPersonalities.All.Select(p => p.Name), Is.Unique);
        Assert.That(BotPersonalities.For(null), Is.Null);
    }
}
