using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.GS.PacketHandler;

namespace DOL.GS;

/// <summary>
/// Coordinates scarce, short bot conversations. Faction text stays broad;
/// local text is allowed to mention the speaker's live task and surroundings.
/// </summary>
public static class AutonomousBotChatCoordinator
{
    private static readonly object Sync = new();
    private static readonly Dictionary<eRealm, long> LastFactionAmbient = new();
    private static readonly Dictionary<ushort, long> LastLocalAmbient = new();
    private static readonly Dictionary<eRealm, long> LastFactionPlayerResponse = new();
    private static readonly Dictionary<ushort, long> LastLocalPlayerResponse = new();
    private static readonly Dictionary<int, long> LastPrivateReply = new();
    private const int PrivateReplyCooldownMs = 3_000;
    private static readonly Dictionary<Group, long> NextGroupTalk = new();
    private const int MaxGroupTalkEntries = 256;
    private const int PlainGroupTalkGapMs = 120_000;

    /// <summary>The private channels a player can use to talk to one personality bot.</summary>
    public enum ePrivateChannel
    {
        Tell,
        Whisper,
        Group,
    }

    private static readonly string[] FactionOpeners =
    [
        "How fares everyone across the realm?", "Any groups forming today?", "The roads seem lively today.",
        "Anyone heading out for a longer hunt?", "I may look for company later.", "Good hunting, everyone.",
        "Has the frontier been active?", "Anyone trading useful equipment today?", "Stay sharp out there.",
        "Which hunting grounds are treating everyone well?", "Any useful class advice to share today?",
        "Has anyone found a dependable camp for their level?", "The realm feels lively today.",
        "If anyone needs a group, say where you are headed.", "Remember to visit your trainer after leveling.",
        "Are any dungeon groups planning a careful run?", "Safe roads and good hunting to everyone.",
        "What is everyone working on today?",
    ];

    private static readonly string[] FactionReplies =
    [
        "Things seem steady from where I am.", "I have seen a few adventurers moving around.",
        "I may be available once I finish up here.", "That sounds worth keeping in mind.",
        "Safe travels if you head that way.", "I have heard similar talk around the realm.",
        "There should be others interested before long.", "I will keep an eye out while I finish this task.",
        "Sharing the location may help someone nearby.", "A balanced group should handle that well.",
        "Checking your trainer and equipment is always worthwhile.", "Good luck with the next pull.",
    ];

    public static bool TryStartAmbient(GameBot starter, AutonomousBotChat.Context context)
    {
        if (!Eligible(starter))
            return false;

        bool factionAudience = HasFactionAudience(starter.Realm);
        // A bot grouped with a player talks in group chat, never starts nearby small talk.
        bool groupAudience = IsGroupedWithPlayer(starter);
        bool localAudience = !groupAudience && HasLocalAudience(starter);
        if (!factionAudience && !localAudience && !groupAudience)
            return false;

        bool faction = factionAudience && (!localAudience && !groupAudience || Random.Shared.NextDouble() < 0.24);
        if (!faction && groupAudience)
        {
            if (!IsGroupTurn(starter.Group))
                return false;
            string line = KobaldjoviIdentity.Is(starter)
                ? KobaldjoviIdentity.AmbientLine(Random.Shared, out _)
                : BuildAmbientOpening(starter, context, false);
            if (!SpeakInGroup(starter, line))
                return false;
            MarkGroupTurn(starter.Group, PlainGroupTalkGapMs);
            return true;
        }
        lock (Sync)
        {
            if (faction)
            {
                if (LastFactionAmbient.TryGetValue(starter.Realm, out long last) && GameLoop.GameLoopTime - last < 180_000)
                    return false;
                LastFactionAmbient[starter.Realm] = GameLoop.GameLoopTime;
            }
            else
            {
                if (LastLocalAmbient.TryGetValue(starter.CurrentRegionID, out long last) && GameLoop.GameLoopTime - last < 90_000)
                    return false;
                LastLocalAmbient[starter.CurrentRegionID] = GameLoop.GameLoopTime;
            }
        }

        // Kobaldjovi mostly sings; a song gets a reaction instead of small talk.
        bool isSong = false;
        string opening = KobaldjoviIdentity.Is(starter)
            ? KobaldjoviIdentity.AmbientLine(Random.Shared, out isSong)
            : BuildAmbientOpening(starter, context, faction);
        if (faction)
            BroadcastFaction(starter.Name, starter.Realm, opening);
        else if (!starter.Say(opening))
            return false;

        if (isSong)
            ScheduleSongReaction(starter, faction);
        else
            ScheduleBotReplies(starter, opening, faction, false);
        return true;
    }

    /// <summary>
    /// Sometimes one listener reacts to Kobaldjovi's song (Persoper first, if she can hear it),
    /// and sometimes he answers that once. At most two lines, never a loop.
    /// </summary>
    private static void ScheduleSongReaction(GameBot singer, bool faction)
    {
        if (Random.Shared.NextDouble() >= KobaldjoviIdentity.SongReactionChance)
            return;
        GameBot[] listeners = AutonomousBotRegistry.Snapshot().Where(Eligible)
            .Concat(PersonalityCompanions())
            .Where(bot => bot != singer && bot.Realm == singer.Realm)
            .Where(bot => faction || bot.CurrentRegion == singer.CurrentRegion && bot.IsWithinRadius(singer, WorldMgr.SAY_DISTANCE))
            .Distinct()
            .OrderByDescending(bot => BotPersonalities.For(bot) != null)
            .ThenBy(_ => Random.Shared.Next())
            .ToArray();
        if (listeners.Length == 0)
            return;

        GameBot reactor = listeners[0];
        string reaction = KobaldjoviIdentity.SongReaction(BotPersonalities.For(reactor), Random.Shared);
        Schedule(reactor, 2_000 + Random.Shared.Next(1_500), () => Speak(reactor, reaction, faction));
        if (Random.Shared.NextDouble() < KobaldjoviIdentity.ThanksChance)
        {
            string thanks = KobaldjoviIdentity.ThanksFor(reactor.Name, Random.Shared);
            Schedule(singer, 6_000 + Random.Shared.Next(1_500), () => Speak(singer, thanks, faction));
        }
    }

    private static void Speak(GameBot speaker, string line, bool faction)
    {
        if (faction && HasFactionAudience(speaker.Realm))
            BroadcastFaction(speaker.Name, speaker.Realm, line);
        else if (!faction && HasLocalAudience(speaker))
            speaker.Say(line);
    }

    public static void OnPlayerFactionChat(GamePlayer player, string message)
    {
        if (player == null || string.IsNullOrWhiteSpace(message))
            return;
        BroadcastFaction(player.Name, player.Realm, message);
        lock (Sync)
        {
            if (LastFactionPlayerResponse.TryGetValue(player.Realm, out long last) && GameLoop.GameLoopTime - last < 12_000)
                return;
            LastFactionPlayerResponse[player.Realm] = GameLoop.GameLoopTime;
        }
        SchedulePlayerReplies(player, message, true);
    }

    public static void OnPlayerLocalChat(GamePlayer player, string message)
    {
        if (player == null || string.IsNullOrWhiteSpace(message) || player.CurrentRegion == null)
            return;
        // /say heal requests reach healers in the player's own group only.
        if (PersonalityHeals.TryHandle(player, message, null, null))
            return;
        if (AutonomousChatIntentModel.Predict(message) == eAutonomousChatIntent.IgnoreOutOfWorld)
            return;
        lock (Sync)
        {
            if (LastLocalPlayerResponse.TryGetValue(player.CurrentRegionID, out long last) && GameLoop.GameLoopTime - last < 10_000)
                return;
            LastLocalPlayerResponse[player.CurrentRegionID] = GameLoop.GameLoopTime;
        }
        SchedulePlayerReplies(player, message, false);
    }

    /// <summary>
    /// The online personality bot a /send names, by full name or personality name
    /// ("Kobaldjovi", "Persoper"), in the sender's realm; null when none matches.
    /// </summary>
    public static GameBot FindPersonalityBot(string typedName, eRealm realm)
    {
        if (string.IsNullOrWhiteSpace(typedName))
            return null;
        return AutonomousBotRegistry.Snapshot().Where(Eligible)
            .Concat(PersonalityCompanions())
            .FirstOrDefault(bot => bot.Realm == realm && BotPersonalities.For(bot) is { } personality &&
                IsPersonalityName(bot.Name, personality.Name, typedName));
    }

    /// <summary>True when what a player typed names this bot: its full name or its personality name.</summary>
    public static bool IsPersonalityName(string botName, string personalityName, string typedName) =>
        !string.IsNullOrWhiteSpace(typedName) &&
        (string.Equals(typedName.Trim(), botName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(typedName.Trim(), personalityName, StringComparison.OrdinalIgnoreCase));

    /// <summary>A player's group message: one personality bot in the group answers, the one named if any.</summary>
    public static void OnPlayerGroupChat(GamePlayer player, string message)
    {
        if (player?.Group == null || string.IsNullOrWhiteSpace(message))
            return;
        if (PersonalityHeals.TryHandle(player, message, null, ePrivateChannel.Group))
            return;
        GameBot[] members = player.Group.GetMembersInTheGroup().OfType<GameBot>()
            .Where(bot => BotPersonalities.For(bot) != null)
            .ToArray();
        if (members.Length == 0)
            return;
        GameBot[] mentioned = members.Where(bot => IsMentioned(message, bot)).ToArray();
        GameBot[] pool = mentioned.Length > 0 ? mentioned : members;
        OnPlayerPrivateChat(player, pool[Random.Shared.Next(pool.Length)], message, ePrivateChannel.Group);
    }

    /// <summary>
    /// A tell, whisper or group line to one personality bot: it answers on the same
    /// channel in its own voice. Plain bots stay silent. Returns false when nothing is sent.
    /// </summary>
    public static bool OnPlayerPrivateChat(GamePlayer player, GameBot bot, string message, ePrivateChannel channel)
    {
        // A heal request to a healer is answered by casting, not by chat.
        if (PersonalityHeals.TryHandle(player, message, bot, channel))
            return true;
        BotPersonality personality = BotPersonalities.For(bot);
        if (player == null || personality == null || string.IsNullOrWhiteSpace(message))
            return false;
        lock (Sync)
        {
            if (LastPrivateReply.TryGetValue(bot.ObjectID, out long last) && GameLoop.GameLoopTime - last < PrivateReplyCooldownMs)
                return false;
            LastPrivateReply[bot.ObjectID] = GameLoop.GameLoopTime;
        }

        eAutonomousChatIntent intent = AutonomousChatIntentModel.Predict(message);
        string reply = PersonalitySpecialReply(bot, player, message, intent, true)
            ?? (AutonomousChatKnowledge.TryAnswer(player, message, out string knowledge)
                ? knowledge
                : MoodTinted(bot, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared)));
        Schedule(bot, 1_200 + Random.Shared.Next(1_000), () => Deliver(bot, player, channel, reply));
        return true;
    }

    /// <summary>Sends a bot's line to a player on a private channel: a tell, a whisper or group chat.</summary>
    public static void Deliver(GameBot bot, GamePlayer player, ePrivateChannel channel, string line)
    {
        if (player?.ObjectState != GameObject.eObjectState.Active || string.IsNullOrEmpty(line))
            return;
        switch (channel)
        {
            case ePrivateChannel.Tell:
                ChatUtil.SendSendMessage(player.Client, "Social.ReceiveMessage.Private.Sends", bot.Name, line);
                break;
            case ePrivateChannel.Whisper:
                bot.Whisper(player, line);
                break;
            case ePrivateChannel.Group:
                if (bot.Group != null && bot.Group == player.Group)
                    bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                break;
        }
    }

    /// <summary>
    /// A personality-type bot speaks one of its own lines, in faction chat or nearby, when
    /// someone can hear it; nearby bots may answer (in their own voices if they have one).
    /// Paced by PersonalityChatter, outside the plain bots' shared chat slots.
    /// </summary>
    public static bool SpeakAsPersonality(GameBot bot, string line, bool faction, eChatTopic? topic = null)
    {
        if (!Eligible(bot) || string.IsNullOrWhiteSpace(line))
            return false;
        if (faction)
        {
            if (!HasFactionAudience(bot.Realm))
                return false;
            BroadcastFaction(bot.Name, bot.Realm, line);
        }
        else if (!HasLocalAudience(bot) || !bot.Say(line))
            return false;
        ScheduleBotReplies(bot, line, faction, false, topic);
        return true;
    }

    /// <summary>
    /// A personality-type bot reacts to an event (a ding, a death, a drop): in group chat
    /// when a player is in its group, else nearby when a player can hear it. Nobody answers.
    /// A dead bot may still speak (allowDead), as players do.
    /// </summary>
    public static bool SpeakReaction(GameBot bot, string line, bool allowDead = false)
    {
        if (bot?.IsAutonomousWorldBot != true || string.IsNullOrWhiteSpace(line) ||
            bot.ObjectState != GameObject.eObjectState.Active || bot.IsCasting || !allowDead && !bot.IsAlive)
            return false;
        if (IsGroupedWithPlayer(bot))
        {
            bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
            return true;
        }
        return HasLocalAudience(bot) && bot.Say(line);
    }

    /// <summary>A personality-type bot's reaction in faction chat (a ding, or cheering one). Nobody answers.</summary>
    public static bool SpeakFactionReaction(GameBot bot, string line)
    {
        if (bot?.IsAutonomousWorldBot != true || string.IsNullOrWhiteSpace(line) ||
            bot.ObjectState != GameObject.eObjectState.Active || !HasFactionAudience(bot.Realm))
            return false;
        BroadcastFaction(bot.Name, bot.Realm, line);
        return true;
    }

    /// <summary>
    /// A bot grouped with a player opens a conversation in group chat; its groupmate bots
    /// may answer there (typed bots in their own voice, on the same topic when there is one).
    /// </summary>
    public static bool SpeakInGroup(GameBot bot, string line, eChatTopic? topic = null)
    {
        if (!Eligible(bot) || string.IsNullOrWhiteSpace(line) || !IsGroupedWithPlayer(bot))
            return false;
        bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
        ScheduleGroupReplies(bot, line, topic);
        return true;
    }

    /// <summary>True when this group may start its next group-chat conversation.</summary>
    public static bool IsGroupTurn(Group group)
    {
        if (group == null)
            return false;
        lock (Sync)
            return !NextGroupTalk.TryGetValue(group, out long next) || GameLoop.GameLoopTime >= next;
    }

    /// <summary>The group's next conversation waits this long.</summary>
    public static void MarkGroupTurn(Group group, int gapMs)
    {
        if (group == null)
            return;
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            // Old groups are forgotten once their gap has passed; the table stays small.
            if (NextGroupTalk.Count >= MaxGroupTalkEntries)
            {
                foreach (Group old in NextGroupTalk.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                    NextGroupTalk.Remove(old);
            }
            NextGroupTalk[group] = now + gapMs;
        }
    }

    private static void ScheduleGroupReplies(GameBot starter, string opening, eChatTopic? topic)
    {
        Group group = starter.Group;
        GameBot[] mates = group.GetMembersInTheGroup().OfType<GameBot>()
            .Where(bot => bot != starter && (Eligible(bot) || IsPersonalityCompanion(bot)))
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();
        int count = RollResponseCount(mates.Length);
        HashSet<string> scheduledResponses = new(StringComparer.OrdinalIgnoreCase);
        int scheduledCount = 0;
        for (int index = 0; index < count; index++)
        {
            GameBot responder = mates[index];
            if (!TryAddDistinctResponse(scheduledResponses, BotReply(responder, starter, opening, topic, false), out string response))
                continue;
            Schedule(responder, 1_700 + scheduledCount++ * 2_100 + Random.Shared.Next(1_300), () =>
            {
                if (responder.Group == group && IsGroupedWithPlayer(responder))
                    group.SendMessageToGroupMembers(responder, response, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
            });
        }
    }

    /// <summary>A bot's answer to another bot's line: in its own voice when it has one, on the topic when there is one.</summary>
    private static string BotReply(GameBot responder, GameBot starter, string opening, eChatTopic? topic, bool faction)
    {
        BotPersonality personality = BotPersonalities.For(responder);
        if (personality == null)
            return faction ? GenerateFactionReply(opening) : GenerateLocalReply(responder, opening);
        return (topic is { } opened ? PersonalityReactions.TopicReplyFor(responder, personality, opened, starter.Name) : null)
            ?? personality.Reply(AutonomousChatIntentModel.Predict(opening), starter.Name, PersoperProperties.TONE_DOWN, Random.Shared);
    }

    /// <summary>True when the bot is in a group with at least one real player.</summary>
    public static bool IsGroupedWithPlayer(GameBot bot) =>
        bot?.Group is { } group && group.GetMembersInTheGroup().Any(member => member is GamePlayer);

    /// <summary>True when a message names this bot (full name, or its personality's name).</summary>
    public static bool IsMentionedPublic(string message, GameBot bot) => IsMentioned(message, bot);

    public static bool TryAdvertiseExchangeItem(GameBot seller, string exactItemName)
    {
        if (!Eligible(seller) || string.IsNullOrWhiteSpace(exactItemName) || !HasFactionAudience(seller.Realm))
            return false;
        lock (Sync)
        {
            if (LastFactionAmbient.TryGetValue(seller.Realm, out long last) && GameLoop.GameLoopTime - last < 180_000)
                return false;
            LastFactionAmbient[seller.Realm] = GameLoop.GameLoopTime;
        }

        string item = exactItemName.Trim();
        string opening = Random.Shared.Next(2) == 0
            ? $"Does anybody want to make me an offer on {item}?"
            : $"How much do u think {item} is worth?";
        BroadcastFaction(seller.Name, seller.Realm, opening);
        ScheduleBotReplies(seller, opening, true, false);
        return true;
    }

    public static string GenerateFactionReply(string incoming, Random random = null)
    {
        random ??= Random.Shared;
        if (incoming.Contains("offer on", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains("how much", StringComparison.OrdinalIgnoreCase) && incoming.Contains("worth", StringComparison.OrdinalIgnoreCase))
        {
            return random.Next(5) switch
            {
                0 => "Compare its level and bonuses before setting the price.",
                1 => "The Realm Exchange should show what similar gear is worth.",
                2 => "That may be useful to someone leveling nearby.",
                3 => "A merchant price can provide a reasonable minimum value.",
                _ => "I might offer a few silver if it suits my class.",
            };
        }
        if (incoming.Contains("mythic", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains(" broken", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains(" op", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains("best class", StringComparison.OrdinalIgnoreCase))
        {
            return random.Next(8) switch
            {
                0 => "Every class has strengths that show in the right group.",
                1 => "Which specialization are you considering?",
                2 => "That ability may work better with the right positioning.",
                3 => "It is worth asking someone who plays that class often.",
                4 => "Equipment and specialization can change how the class feels.",
                5 => "I would compare how it performs solo and in a group.",
                6 => "A careful pull usually tells you more than the delve text.",
                _ => "Try the class and see which playstyle suits you.",
            };
        }
        if (incoming.Contains("lag", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains(" dc", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains("disconnect", StringComparison.OrdinalIgnoreCase))
        {
            return random.Next(5) switch
            {
                0 => "Movement seems steady where I am.",
                1 => "I noticed a short delay too; it seems better now.",
                2 => "Give the area a moment to finish loading.",
                3 => "Hopefully it clears before your next pull.",
                _ => "Stay with your group until movement catches up.",
            };
        }
        return AutonomousChatIntentModel.Predict(incoming) switch
        {
            eAutonomousChatIntent.Greeting => random.Next(2) == 0 ? "Greetings. Good hunting out there." : "Hello there. The realm is awake today.",
            eAutonomousChatIntent.Banter => random.Next(2) == 0 ? "Good luck on your next pull." : "Save some of that energy for the frontier.",
            eAutonomousChatIntent.Abuse => random.Next(3) switch { 0 => "Let us keep faction chat helpful.", 1 => "I would rather focus on the realm.", _ => "Good hunting when you head back out." },
            eAutonomousChatIntent.Grouping or eAutonomousChatIntent.Help => random.Next(2) == 0 ? "Share your level and location so nearby adventurers can help." : "I may be free once I finish my current task.",
            eAutonomousChatIntent.Travel => random.Next(2) == 0 ? "A stable master may shorten the trip." : "Check the road signs and travel with your group.",
            eAutonomousChatIntent.RvR => random.Next(2) == 0 ? "I have heard the frontier has some movement." : "A few defenders may head out before long.",
            eAutonomousChatIntent.Trading => random.Next(2) == 0 ? "The Realm Exchange may have something useful." : "I have seen a little trade around the capital.",
            eAutonomousChatIntent.Dungeon => "A careful group should clear one pull at a time on the way inside.",
            eAutonomousChatIntent.Grinding => "Share the camp and level if you are looking for company.",
            eAutonomousChatIntent.Farewell => "Safe travels. Until next time.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep faction chat focused on the realm.",
            _ => FactionReplies[random.Next(FactionReplies.Length)],
        };
    }

    public static string GenerateLocalReply(GameBot bot, string incoming, Random random = null)
    {
        random ??= Random.Shared;
        string activity = Clean(bot?.PersistentRecord?.Activity, "taking a short rest");
        string target = Clean(bot?.PersistentRecord?.TargetName, "the creatures nearby");
        string destination = Clean(bot?.PersistentRecord?.TravelDestination, bot?.CurrentZone?.Description ?? "this area");

        return AutonomousChatIntentModel.Predict(incoming) switch
        {
            eAutonomousChatIntent.Greeting => random.Next(2) == 0 ? $"Hello. I am keeping an eye on {target}." : $"Greetings. I am {LowerFirst(activity)}.",
            eAutonomousChatIntent.Banter => random.Next(2) == 0 ? "Good luck on the next pull." : "Keep your weapon ready and stay with the group.",
            eAutonomousChatIntent.Abuse => random.Next(3) switch { 0 => "Let us keep this helpful.", 1 => "I am focusing on my current task.", _ => "Safe travels when you move on." },
            eAutonomousChatIntent.Grouping or eAutonomousChatIntent.Help => bot?.Group == null ? $"I am working around {target} for now, but I could group." : "I am already moving with a group right now.",
            eAutonomousChatIntent.Travel => $"I am headed toward {destination}.",
            eAutonomousChatIntent.Dungeon => $"We should clear one pull at a time if we head deeper. I am {LowerFirst(activity)}.",
            eAutonomousChatIntent.RvR => "If I head to the frontier, I will stay with the realm group.",
            eAutonomousChatIntent.Trading => "I will check my real inventory and the Realm Exchange when I return to town.",
            eAutonomousChatIntent.Grinding => $"I am focused on {target} at the moment.",
            eAutonomousChatIntent.Farewell => "Safe travels. I will keep working here.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep the conversation focused on the realm.",
            _ => random.Next(3) switch
            {
                0 => $"I am focused on {target} at the moment.",
                1 => $"Right now I am {LowerFirst(activity)}.",
                _ => $"I will be around {destination} for a while.",
            },
        };
    }

    public static int RollResponseCount(int eligibleCount, Random random = null)
    {
        if (eligibleCount <= 0)
            return 0;
        random ??= Random.Shared;
        return Math.Min(eligibleCount, random.NextDouble() < 0.32 ? 2 : 1);
    }

    public static bool ContainsMention(string message, string name)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(name))
            return false;
        int start = 0;
        while ((start = message.IndexOf(name, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int end = start + name.Length;
            bool left = start == 0 || !char.IsLetterOrDigit(message[start - 1]);
            bool right = end == message.Length || !char.IsLetterOrDigit(message[end]);
            if (left && right)
                return true;
            start = end;
        }
        return false;
    }

    public static void BroadcastFaction(string speakerName, eRealm realm, string message)
    {
        string safe = (message ?? string.Empty).Trim();
        if (safe.Length == 0)
            return;
        if (safe.Length > 240)
            safe = safe[..240];
        string formatted = $"({speakerName}) <Faction> {safe}";
        foreach (GamePlayer player in ClientService.Instance.GetPlayersOfRealm(realm))
            player.Out.SendMessage(formatted, eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
    }

    private static void SchedulePlayerReplies(GamePlayer player, string message, bool faction)
    {
        bool hasKnowledge = AutonomousChatKnowledge.TryAnswer(player, message, out string knowledge);
        eAutonomousChatIntent intent = AutonomousChatIntentModel.Predict(message);
        // Personality companions (Persoper) are not world bots but still answer players.
        GameBot[] eligible = AutonomousBotRegistry.Snapshot().Where(Eligible)
            .Concat(PersonalityCompanions())
            .Where(bot => bot.Realm == player.Realm)
            .Where(bot => faction || bot.CurrentRegion == player.CurrentRegion && bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE))
            .Distinct()
            .OrderByDescending(bot => BotPersonalities.For(bot) != null)
            .ThenByDescending(bot => Relevance(bot, message))
            .ThenBy(_ => Random.Shared.Next())
            .ToArray();
        GameBot[] mentioned = eligible.Where(bot => IsMentioned(message, bot)).ToArray();
        bool directed = mentioned.Length > 0;
        if (directed)
            eligible = mentioned;
        int count = directed || hasKnowledge || intent is eAutonomousChatIntent.IgnoreOutOfWorld or eAutonomousChatIntent.Abuse
            ? Math.Min(1, eligible.Length)
            : RollResponseCount(eligible.Length);
        HashSet<string> scheduledResponses = new(StringComparer.OrdinalIgnoreCase);
        int scheduledIndex = 0;
        for (int index = 0; index < count; index++)
        {
            GameBot responder = eligible[index];
            BotPersonality personality = BotPersonalities.For(responder);
            string response = personality != null && PersonalitySpecialReply(responder, player, message, intent, IsMentioned(message, responder)) is { } special
                ? special
                : hasKnowledge
                ? knowledge
                : personality != null
                    ? MoodTinted(responder, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared))
                    : directed
                        ? GenerateDirectedReply(message)
                        : faction ? GenerateFactionReply(message) : GenerateLocalReply(responder, message);
            if (!TryAddDistinctResponse(scheduledResponses, response, out response))
                continue;
            Schedule(responder, 1_300 + scheduledIndex++ * 1_800 + Random.Shared.Next(1_200), () =>
            {
                if (faction && HasFactionAudience(responder.Realm))
                    BroadcastFaction(responder.Name, responder.Realm, response);
                else if (!faction && HasLocalAudience(responder))
                    responder.Say(response);
            });
        }
    }

    private static void ScheduleBotReplies(GameBot starter, string opening, bool faction, bool fromPlayer, eChatTopic? topic = null)
    {
        bool hasKnowledge = AutonomousChatKnowledge.TryAnswer(starter.Realm, opening, out string knowledge);
        GameBot[] eligible = AutonomousBotRegistry.Snapshot()
            .Where(bot => bot != starter && Eligible(bot) && bot.Realm == starter.Realm)
            .Where(bot => faction || bot.CurrentRegion == starter.CurrentRegion && bot.IsWithinRadius(starter, WorldMgr.SAY_DISTANCE))
            .OrderByDescending(bot => Relevance(bot, opening))
            .ThenBy(_ => Random.Shared.Next())
            .ToArray();
        int count = hasKnowledge ? Math.Min(1, eligible.Length) : RollResponseCount(eligible.Length);
        HashSet<string> scheduledResponses = new(StringComparer.OrdinalIgnoreCase);
        int scheduledCount = 0;
        // Replies are dropped when the responder is busy by then; count the ones actually said.
        int delivered = 0;
        for (int index = 0; index < count; index++)
        {
            GameBot responder = eligible[index];
            string response = hasKnowledge ? knowledge : BotReply(responder, starter, opening, topic, faction);
            if (!TryAddDistinctResponse(scheduledResponses, response, out response))
                continue;
            Schedule(responder, 1_700 + scheduledCount++ * 2_100 + Random.Shared.Next(1_300), () =>
            {
                if (faction && HasFactionAudience(responder.Realm))
                {
                    BroadcastFaction(responder.Name, responder.Realm, response);
                    Interlocked.Increment(ref delivered);
                }
                else if (!faction && HasLocalAudience(responder) && responder.Say(response))
                    Interlocked.Increment(ref delivered);
            });
        }

        // A short optional closing line makes this read like a conversation,
        // while the channel cooldown prevents it from becoming a chat loop.
        // Only after someone actually answered (else the starter seems to answer itself),
        // and not for personality bots, whose plain closing line would be out of character.
        if (!fromPlayer && !hasKnowledge && scheduledCount > 0 && BotPersonalities.For(starter) == null &&
            Random.Shared.NextDouble() < 0.28)
        {
            string closing = faction ? "Fair enough. Safe travels." : "Understood. I will keep at it.";
            Schedule(starter, 5_500 + scheduledCount * 1_500, () =>
            {
                if (Volatile.Read(ref delivered) == 0)
                    return;
                if (faction && HasFactionAudience(starter.Realm))
                    BroadcastFaction(starter.Name, starter.Realm, closing);
                else if (!faction && HasLocalAudience(starter))
                    starter.Say(closing);
            });
        }
    }

    private static void Schedule(GameBot speaker, int delay, Action action)
    {
        _ = new ECSGameTimer(speaker, timer =>
        {
            if ((Eligible(speaker) || IsPersonalityCompanion(speaker)) && !speaker.InCombat && !speaker.IsCasting)
                action();
            return 0;
        }, delay);
    }

    public static bool TryAddDistinctResponse(HashSet<string> scheduled, string response, out string normalized)
    {
        normalized = response?.Trim() ?? string.Empty;
        return scheduled != null && normalized.Length > 0 && scheduled.Add(normalized);
    }

    /// <summary>
    /// Any personality with a memory: counts the message toward what it remembers of this
    /// player (once they have met), answers "what's your spec?" or "what do you remember?",
    /// and lets rudeness try its patience. Null means reply as usual.
    /// </summary>
    private static string PersonalitySpecialReply(GameBot bot, GamePlayer player, string message, eAutonomousChatIntent intent, bool askedIt)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        if (personality?.Memory == null || player == null)
            return null;
        PersonalityMemoryRecord memory = PersonalityMemoryStore.Load(personality, player);
        // Spec and memory questions count only when meant for it (named, or a tell/whisper/group line).
        string special = !askedIt ? null
            : PersonalityMemory.AsksMemory(message)
            ? PersonalityMemory.Recall(personality, memory, player.Name, DateTime.UtcNow)
            : PersonalityMemory.AsksSpec(message)
                ? PersonalityMemory.SpecAnnouncement(personality, bot.BotSpec?.SpecType ?? eSpecType.None, eSpecType.None, player.Name, Random.Shared)
                : null;
        // Only players it has met (summoned it, or grouped with it) are remembered.
        if (!PersonalityMemory.IsNew(memory))
        {
            PersonalityMemory.RecordChat(memory, intent, message);
            PersonalityMemoryStore.Save(memory);
        }
        if (intent == eAutonomousChatIntent.Abuse && askedIt)
            PersonalityAnnoyance.Irritate(player, bot, PersonalityAnnoyance.eIrritation.Rude);
        return special;
    }

    /// <summary>A personality's mood toward this player sometimes colours its reply.</summary>
    private static string MoodTinted(GameBot bot, GamePlayer player, string reply)
    {
        BotPersonality personality = BotPersonalities.For(bot);
        if (personality?.Memory == null || player == null)
            return reply;
        return PersonalityMemory.Tint(personality, PersonalityMemoryStore.Load(personality, player), reply, Random.Shared);
    }

    private static bool IsMentioned(string message, GameBot bot) =>
        ContainsMention(message, bot.Name) || BotPersonalities.For(bot) is { } personality && ContainsMention(message, personality.Name);

    private static IEnumerable<GameBot> PersonalityCompanions()
    {
        GameBot persoper = DOL.GS.Commands.PersoperCommandHandler.Active;
        return persoper != null && IsPersonalityCompanion(persoper) ? [persoper] : [];
    }

    private static bool IsPersonalityCompanion(GameBot bot) =>
        bot is { IsTemporaryGroupHelper: true, IsAlive: true, ObjectState: GameObject.eObjectState.Active, InCombat: false, IsCasting: false } &&
        BotPersonalities.For(bot) != null;

    private static bool Eligible(GameBot bot) => bot?.IsAutonomousWorldBot == true && !bot.IsTemporaryGroupHelper &&
        bot.IsAlive && bot.ObjectState == GameObject.eObjectState.Active && !bot.InCombat && !bot.IsCasting &&
        !bot.IsOnStableMasterRoute && !bot.IsOnHorse;

    private static bool HasFactionAudience(eRealm realm) => ClientService.Instance.GetPlayersOfRealm(realm).Count > 0;

    private static bool HasLocalAudience(GameBot bot) => bot?.CurrentRegion != null &&
        ClientService.Instance.GetPlayersOfRegion(bot.CurrentRegion)
            .Any(player => player.ObjectState == GameObject.eObjectState.Active && bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE));

    private static string BuildAmbientOpening(GameBot starter, AutonomousBotChat.Context context, bool faction)
    {
        if (Random.Shared.NextDouble() < 0.18)
        {
            string activity = Clean(starter.PersistentRecord?.Activity, string.Empty);
            string destination = Clean(starter.PersistentRecord?.TravelDestination, string.Empty);
            if (!string.IsNullOrWhiteSpace(destination) &&
                (activity.StartsWith("Walking to", StringComparison.OrdinalIgnoreCase) ||
                 activity.StartsWith("Approaching", StringComparison.OrdinalIgnoreCase)))
                return Random.Shared.Next(2) == 0
                    ? $"Anyone know where {destination} is?"
                    : $"Does anyone know where {destination} is at?";

            string target = Clean(starter.PersistentRecord?.TargetName, string.Empty);
            if (!string.IsNullOrWhiteSpace(target))
                return Random.Shared.Next(3) switch
                {
                    0 => $"Anyone know where {target} is?",
                    1 => $"Does anyone know where {target} is at?",
                    _ => $"Has anybody ever found {target}?",
                };

            if (!string.IsNullOrWhiteSpace(destination))
                return Random.Shared.Next(3) switch
                {
                    0 => $"Anyone know where {destination} is?",
                    1 => $"How do I get to {destination}?",
                    _ => $"Which stable master takes me to {destination}?",
                };
        }

        if (faction)
        {
            string situation = $"{starter.PersistentRecord?.Activity} {starter.PersistentRecord?.CurrentGoal} {starter.CurrentZone?.Description}";
            bool pvp = situation.Contains("rvr", StringComparison.OrdinalIgnoreCase) ||
                       situation.Contains("pvp", StringComparison.OrdinalIgnoreCase) ||
                       situation.Contains("frontier", StringComparison.OrdinalIgnoreCase) || starter.CurrentZone?.IsRvR == true;
            return BuildFactionTaskOpening(starter, context, pvp);
        }
        return AutonomousBotChat.Generate(context);
    }

    private static string BuildFactionTaskOpening(GameBot starter, AutonomousBotChat.Context context, bool pvp)
    {
        string className = Clean(context.ClassName, starter.ClassName);
        string zone = Clean(context.ZoneName, starter.CurrentZone?.Description ?? "my current area");
        string target = Clean(context.MonsterName, string.Empty);
        string destination = Clean(starter.PersistentRecord?.TravelDestination, string.Empty);
        int choice = Random.Shared.Next(10);

        if (pvp)
        {
            return (choice % 4) switch
            {
                0 => $"I am watching the frontier around {zone}. Where are defenders gathering?",
                1 => $"Any {className}s have advice for supporting a frontier group?",
                2 => "If anyone sees enemy movement, share the location so the realm can respond.",
                _ => "Good luck to everyone heading into the frontier. Stay with your group.",
            };
        }

        if (!string.IsNullOrWhiteSpace(target) && choice < 4)
        {
            return choice switch
            {
                0 => $"I am hunting {target} around {zone}. Has anyone found a steady camp nearby?",
                1 => $"These {target} are keeping me busy in {zone}. Anyone else working nearby?",
                2 => starter.Group?.MemberCount > 1
                    ? $"Our group is taking careful pulls of {target} in {zone}."
                    : $"I am taking careful pulls of {target} in {zone}.",
                _ => $"Has anyone learned a reliable way to handle {target} around {zone}?",
            };
        }

        if (!string.IsNullOrWhiteSpace(destination) && choice < 7)
        {
            return (choice % 3) switch
            {
                0 => $"I am heading toward {destination}. Any advice on the safest route?",
                1 => $"Does anyone know which stable master is best for reaching {destination}?",
                _ => $"Safe travels to anyone heading toward {destination}.",
            };
        }

        return choice switch
        {
            7 => $"Any other {className}s have advice for adventurers around level {starter.Level}?",
            8 => $"How are the hunting grounds around {zone} treating everyone?",
            9 when starter.Group?.MemberCount > 1 => $"Our group is working around {zone}. Are any other parties nearby?",
            _ => FactionOpeners[Random.Shared.Next(FactionOpeners.Length)],
        };
    }

    private static int Relevance(GameBot bot, string incoming)
    {
        string haystack = $"{bot.PersistentRecord?.Activity} {bot.PersistentRecord?.CurrentGoal} {bot.PersistentRecord?.TargetName} {bot.PersistentRecord?.TravelDestination}";
        return (incoming ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('?', '!', '.', ',', '\'', '"'))
            .Where(word => word.Length >= 4)
            .Count(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateDirectedReply(string incoming)
    {
        if (incoming.Contains("stop", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains("quiet", StringComparison.OrdinalIgnoreCase) ||
            incoming.Contains("shut", StringComparison.OrdinalIgnoreCase))
        {
            return Random.Shared.Next(4) switch
            {
                0 => "Understood. I will focus on my task.",
                1 => "All right. Safe travels.",
                2 => "No problem. I will keep faction chat clear.",
                _ => "Okay. I will get back to work.",
            };
        }
        return AutonomousChatIntentModel.Predict(incoming) switch
        {
            eAutonomousChatIntent.Greeting => "Greetings. How can I help?",
            eAutonomousChatIntent.Abuse => "Let us keep the conversation helpful.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep the conversation focused on the realm.",
            _ => Random.Shared.Next(3) switch { 0 => "What did you need?", 1 => "Perhaps after this task.", _ => "Yes?" },
        };
    }

    private static string Clean(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().TrimEnd('.', '!', '?');
    private static string LowerFirst(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
