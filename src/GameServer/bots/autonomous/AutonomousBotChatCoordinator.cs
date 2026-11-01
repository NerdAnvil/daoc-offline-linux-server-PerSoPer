using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.GS.PacketHandler;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// Coordinates scarce, short bot conversations. Faction text stays broad;
/// local text is allowed to mention the speaker's live task and surroundings.
/// </summary>
public static class AutonomousBotChatCoordinator
{
    private static readonly Logger Log = LoggerManager.Create(typeof(AutonomousBotChatCoordinator));
    private static readonly object Sync = new();
    private static readonly Dictionary<eRealm, long> LastFactionAmbient = new();
    private static readonly Dictionary<ushort, long> LastLocalAmbient = new();
    private static readonly Dictionary<eRealm, long> LastFactionPlayerResponse = new();
    private static readonly Dictionary<ushort, long> LastLocalPlayerResponse = new();
    private static readonly Dictionary<int, long> LastPrivateReply = new();
    private const int PrivateReplyCooldownMs = 3_000;
    // Short reactions (lol, sorry, brb, back, nah): how often the one bot that may react does.
    public const double ReactionChance = 0.25;
    public const double NamedReactionChance = 0.5;
    // A named bot answers even while riding; in a cast or a fight it waits a few tries for it to end, then answers
    // anyway. Now and then one other bot chips in a few seconds after it.
    public const int NamedReplyRetries = 3;
    private const int NamedRetryDelayMs = 1_500;
    public const double ChipInChance = 0.15;
    private static readonly Dictionary<Group, long> NextGroupTalk = new();
    private const int MaxGroupTalkEntries = 256;
    private const int PlainGroupTalkGapMs = 120_000;
    /// <summary>Groups whose chatter is paused (a pull just started), until when.</summary>
    private static readonly Dictionary<Group, long> QuietUntil = new();
    private const int PullQuietMs = 20_000;

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
                if (LastFactionAmbient.TryGetValue(starter.Realm, out long last) && GameLoop.GameLoopTime - last < 180_000 ||
                    PersonalityScenes.HeldMs(PersonalityScenes.FactionKey(starter.Realm)) > 0)
                    return false;
                LastFactionAmbient[starter.Realm] = GameLoop.GameLoopTime;
            }
            else
            {
                if (LastLocalAmbient.TryGetValue(starter.CurrentRegionID, out long last) && GameLoop.GameLoopTime - last < 90_000 ||
                    PersonalityScenes.HeldMs(PersonalityScenes.NearbyKey(starter.CurrentRegionID)) > 0)
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
            BotFaction(starter, opening);
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
            BotFaction(speaker, line);
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
        // A short line while Persoper waits on a question is the answer, not chat for the others.
        if (PersoperAsks.TryAnswer(player, message))
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
    /// The online personality bot a /send names, by full name, personality name or nickname
    /// ("Kobaldjovi", "Persoper", "Jovi"), in the sender's realm; null when none matches.
    /// </summary>
    public static GameBot FindPersonalityBot(string typedName, eRealm realm)
    {
        if (string.IsNullOrWhiteSpace(typedName))
            return null;
        return AutonomousBotRegistry.Snapshot().Where(CanAnswerNamed)
            .Concat(PersonalityCompanions(evenBusy: true))
            .FirstOrDefault(bot => bot.Realm == realm && BotPersonalities.For(bot) is { } personality &&
                IsPersonalityName(bot.Name, personality.Name, typedName, personality.Nicknames));
    }

    /// <summary>True when what a player typed names this bot: its full name, its personality name or a nickname.</summary>
    public static bool IsPersonalityName(string botName, string personalityName, string typedName, IReadOnlyList<string> nicknames = null) =>
        !string.IsNullOrWhiteSpace(typedName) &&
        (string.Equals(typedName.Trim(), botName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(typedName.Trim(), personalityName, StringComparison.OrdinalIgnoreCase) ||
         (nicknames ?? []).Any(nickname => string.Equals(typedName.Trim(), nickname, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// A player's group message: one personality bot in the group answers, the one named if any;
    /// after a named one, another member now and then chips in.
    /// </summary>
    public static void OnPlayerGroupChat(GamePlayer player, string message)
    {
        if (player?.Group == null || string.IsNullOrWhiteSpace(message))
            return;
        if (PersonalityHeals.TryHandle(player, message, null, ePrivateChannel.Group))
            return;
        if (PersoperAsks.TryAnswer(player, message))
            return;
        GameBot[] members = player.Group.GetMembersInTheGroup().OfType<GameBot>()
            .Where(bot => BotPersonalities.For(bot) != null)
            .ToArray();
        if (members.Length == 0)
            return;
        GameBot[] mentioned = members.Where(bot => IsMentioned(message, bot)).ToArray();
        // A line naming a bot outside the group isn't the members' to answer.
        if (OthersStayQuiet(mentioned, NamedInRealm(player, message)))
            return;
        GameBot[] pool = mentioned.Length > 0 ? mentioned : members;
        GameBot answerer = pool[Random.Shared.Next(pool.Length)];
        GameBot chipIn = mentioned.Length > 0
            ? ChipIn(members.Where(bot => bot != answerer && (Eligible(bot) || IsPersonalityCompanion(bot))).ToArray(), Random.Shared)
            : null;
        OnPlayerPrivateChat(player, answerer, message, ePrivateChannel.Group, chipIn == null ? null : said =>
            ScheduleChipIn(player, chipIn, message, said, line => Deliver(chipIn, player, ePrivateChannel.Group, line)));
    }

    /// <summary>
    /// A tell, whisper or group line to one personality bot: it answers on the same
    /// channel in its own voice. Plain bots stay silent. Returns false when nothing is sent.
    /// <paramref name="afterSaid"/> runs with the line once it has been said (not for a short reaction).
    /// </summary>
    public static bool OnPlayerPrivateChat(GamePlayer player, GameBot bot, string message, ePrivateChannel channel, Action<string> afterSaid = null)
    {
        // A heal request to a healer is answered by casting, not by chat.
        if (PersonalityHeals.TryHandle(player, message, bot, channel))
            return true;
        if (PersoperAsks.TryAnswer(player, message, bot))
            return true;
        BotPersonality personality = BotPersonalities.For(bot);
        if (player == null || personality == null || string.IsNullOrWhiteSpace(message))
            return false;
        lock (Sync)
        {
            if (LastPrivateReply.TryGetValue(bot.ObjectID, out long last) && GameLoop.GameLoopTime - last < PrivateReplyCooldownMs)
                return false;
        }
        // A "lol" or "brb" in group chat gets a short reaction now and then; a tell or whisper is named.
        eAutonomousChatIntent read = AutonomousChatIntentModel.Read(message);
        string reaction = IsShortReaction(read)
            ? ShortReaction(personality, read, channel != ePrivateChannel.Group || IsMentioned(message, bot), player.Name, Random.Shared, DialogueFactSource.For(bot, player))
            : null;
        if (IsShortReaction(read) && reaction == null)
            return false;
        lock (Sync)
            LastPrivateReply[bot.ObjectID] = GameLoop.GameLoopTime;
        if (reaction != null)
        {
            Schedule(bot, 1_200 + Random.Shared.Next(1_000), () => Deliver(bot, player, channel, reaction), named: true);
            return true;
        }

        eAutonomousChatIntent intent = NotWhenGrouped(AutonomousChatIntentModel.Predict(message), bot.Group != null && bot.Group == player.Group);
        string reply = PersonalitySpecialReply(bot, player, message, intent, true)
            ?? (AutonomousChatKnowledge.TryAnswer(player, message, out string knowledge)
                ? knowledge
                : MoodTinted(bot, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared, DialogueFactSource.For(bot, player))));
        Schedule(bot, 1_200 + Random.Shared.Next(1_000), () =>
        {
            if (Deliver(bot, player, channel, reply))
                afterSaid?.Invoke(reply);
        }, named: true);
        return true;
    }

    /// <summary>Sends a bot's line to a player on a private channel: a tell, a whisper or group chat. False when not sent.</summary>
    public static bool Deliver(GameBot bot, GamePlayer player, ePrivateChannel channel, string line)
    {
        if (player?.ObjectState != GameObject.eObjectState.Active || string.IsNullOrEmpty(line))
            return false;
        switch (channel)
        {
            case ePrivateChannel.Tell:
                ChatUtil.SendSendMessage(player.Client, "Social.ReceiveMessage.Private.Sends", bot.Name, line);
                return true;
            case ePrivateChannel.Whisper:
                bot.Whisper(player, line);
                return true;
            case ePrivateChannel.Group:
                if (bot.Group == null || bot.Group != player.Group)
                    return false;
                bot.Group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                return true;
        }
        return false;
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
            BotFaction(bot, line);
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
        BotFaction(bot, line);
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

    /// <summary>True when a world bot is free to say its next scene line (not fighting, casting or riding).</summary>
    public static bool CanSpeakInScene(GameBot bot) => Eligible(bot);

    /// <summary>
    /// One line of a PersonalityScenes scene, in its channel. False when the bot can't talk
    /// now, nobody can hear it, or it left the group; the scene then stops.
    /// </summary>
    public static bool SpeakSceneLine(GameBot bot, string line, eSceneChannel channel, Group group)
    {
        if (!Eligible(bot) || string.IsNullOrWhiteSpace(line))
            return false;
        switch (channel)
        {
            case eSceneChannel.Faction:
                if (!HasFactionAudience(bot.Realm))
                    return false;
                // A scene's own line: only bots in the scene that holds faction chat (if one does) may speak.
                if (PersonalityScenes.Hushes(PersonalityScenes.FactionKey(bot.Realm), bot))
                    return false;
                BroadcastFaction(bot.Name, bot.Realm, line);
                return true;
            case eSceneChannel.Group:
                if (group == null || bot.Group != group || !IsGroupedWithPlayer(bot) || IsQuiet(group))
                    return false;
                group.SendMessageToGroupMembers(bot, line, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                return true;
            default:
                return HasLocalAudience(bot) && bot.Say(line);
        }
    }

    /// <summary>
    /// A pull just started: the group's bot chatter stops for a while (queued answers and
    /// scene lines are dropped), so no line seems to answer the pull order.
    /// </summary>
    public static void QuietGroup(Group group, int ms = PullQuietMs)
    {
        if (group == null)
            return;
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            if (QuietUntil.Count >= MaxGroupTalkEntries)
            {
                foreach (Group old in QuietUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                    QuietUntil.Remove(old);
            }
            QuietUntil[group] = now + ms;
        }
    }

    private static bool IsQuiet(Group group)
    {
        if (group == null)
            return false;
        lock (Sync)
            return QuietUntil.TryGetValue(group, out long until) && GameLoop.GameLoopTime < until;
    }

    /// <summary>True when this group may start its next group-chat conversation.</summary>
    public static bool IsGroupTurn(Group group)
    {
        if (group == null || PersonalityScenes.HeldMs(group) > 0 || IsQuiet(group))
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
        // A typed groupmate may carry the topic on as a scene instead of the usual answers.
        if (PersonalityScenes.TryTopicScene(starter, mates, topic, eSceneChannel.Group, group))
            return;
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
                if (responder.Group == group && IsGroupedWithPlayer(responder) && !IsQuiet(group))
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
        eAutonomousChatIntent intent = BotIntent(opening, topic, responder.Group != null && responder.Group == starter.Group);
        DialogueFacts facts = DialogueFactSource.ForBot(responder);
        // A situation line that holds (a groan with Shnacksize nearby) beats the usual topic answer.
        return personality.ConditionalReply(intent, starter.Name, Random.Shared, facts)
            ?? (topic is { } opened ? PersonalityReactions.TopicReplyFor(responder, personality, opened, starter.Name) : null)
            // Always toned down: the cheeky (flirty) lines are for players, not for other bots.
            ?? personality.Reply(intent, starter.Name, toneDown: true, Random.Shared, facts);
    }

    /// <summary>
    /// A bot's line it can't place is small talk, not nonsense: it gets a banter reply rather than
    /// the "you've lost me" fallback, which is meant for players.
    /// </summary>
    public static eAutonomousChatIntent BotToBot(eAutonomousChatIntent intent) =>
        intent == eAutonomousChatIntent.Unknown ? eAutonomousChatIntent.Banter : intent;

    /// <summary>
    /// What a bot answers another bot's line as: the opener's topic when it has a social one (a
    /// joke gets a joke back, a gripe sympathy), else what the words say.
    /// </summary>
    public static eAutonomousChatIntent BotIntent(string opening, eChatTopic? topic, bool sameGroup) =>
        topic is { } opened && AutonomousChatIntentModel.ForTopic(opened) is { } social ? social
            : BotToBot(NotWhenGrouped(AutonomousChatIntentModel.Predict(opening), sameGroup));

    /// <summary>
    /// Someone already in its group isn't asking it to group: a line that sounds like
    /// grouping gets a banter reply instead of "count me in".
    /// </summary>
    public static eAutonomousChatIntent NotWhenGrouped(eAutonomousChatIntent intent, bool sameGroup) =>
        sameGroup && intent == eAutonomousChatIntent.Grouping ? eAutonomousChatIntent.Banter : intent;

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
        BotFaction(seller, opening);
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
            eAutonomousChatIntent.Thank => "Glad to help. Good hunting.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep faction chat focused on the realm.",
            _ => FactionReplies[random.Next(FactionReplies.Length)],
        };
    }

    public static string GenerateLocalReply(GameBot bot, string incoming, Random random = null)
    {
        string zone = bot?.CurrentZone?.Description ?? "this area";
        return BuildLocalReply(AutonomousChatIntentModel.Predict(incoming), bot?.PersistentRecord, zone,
            bot?.Group != null, bot?.IsMoving ?? false, bot?.InCombat ?? false, random);
    }

    /// <summary>
    /// The words of a nearby-chat reply. The saved task (activity, target, destination) is only
    /// quoted while it is still true: the destination while the bot is moving, the target while it
    /// is fighting. A bot standing still says it is resting here instead of an old trip.
    /// </summary>
    public static string BuildLocalReply(eAutonomousChatIntent intent, OfflineWorldBotRecord record, string zone,
        bool inGroup, bool moving, bool fighting, Random random = null)
    {
        random ??= Random.Shared;
        bool current = moving || fighting;
        string activity = current ? Clean(record?.Activity, "taking a short rest") : "taking a short rest";
        string target = fighting ? Clean(record?.TargetName, "the creatures nearby") : "the creatures nearby";
        string destination = moving ? Clean(record?.TravelDestination, zone) : zone;

        return intent switch
        {
            eAutonomousChatIntent.Greeting => random.Next(2) == 0 ? $"Hello. I am keeping an eye on {target}." : $"Greetings. I am {LowerFirst(activity)}.",
            eAutonomousChatIntent.Banter => random.Next(2) == 0 ? "Good luck on the next pull." : "Keep your weapon ready and stay with the group.",
            eAutonomousChatIntent.Abuse => random.Next(3) switch { 0 => "Let us keep this helpful.", 1 => "I am focusing on my current task.", _ => "Safe travels when you move on." },
            eAutonomousChatIntent.Grouping or eAutonomousChatIntent.Help => !inGroup ? $"I am working around {target} for now, but I could group." : "I am already moving with a group right now.",
            eAutonomousChatIntent.Travel => moving ? $"I am headed toward {destination}." : $"I am not going anywhere right now. I am staying around {destination}.",
            eAutonomousChatIntent.Dungeon => $"We should clear one pull at a time if we head deeper. I am {LowerFirst(activity)}.",
            eAutonomousChatIntent.RvR => "If I head to the frontier, I will stay with the realm group.",
            eAutonomousChatIntent.Trading => "I will check my real inventory and the Realm Exchange when I return to town.",
            eAutonomousChatIntent.Grinding => $"I am focused on {target} at the moment.",
            eAutonomousChatIntent.Farewell => "Safe travels. I will keep working here.",
            eAutonomousChatIntent.Thank => "You are welcome. I will keep working here.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep the conversation focused on the realm.",
            _ => random.Next(3) switch
            {
                0 => $"I am focused on {target} at the moment.",
                1 => $"Right now I am {LowerFirst(activity)}.",
                _ => moving ? $"I will be around {destination} for a while." : $"I am staying around {destination} for now.",
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

    /// <summary>
    /// A bot's ordinary line in faction chat. Dropped (false) while a scene holds the realm's faction
    /// chat, even from bots in the scene, so it isn't talked over. Scene lines go through SpeakSceneLine;
    /// players and realm notices use BroadcastFaction.
    /// </summary>
    private static bool BotFaction(GameBot bot, string message)
    {
        if (PersonalityScenes.HeldMs(PersonalityScenes.FactionKey(bot.Realm)) > 0)
            return false;
        BroadcastFaction(bot.Name, bot.Realm, message);
        return true;
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
        // A bot named in the line hears it even while riding a stable route or casting (bug #7).
        GameBot[] eligible = AutonomousBotRegistry.Snapshot()
            .Where(bot => Eligible(bot) || CanAnswerNamed(bot) && IsMentioned(message, bot))
            .Concat(PersonalityCompanions(evenBusy: true).Where(bot => IsPersonalityCompanion(bot) || IsMentioned(message, bot)))
            .Where(bot => bot.Realm == player.Realm)
            .Where(bot => faction || bot.CurrentRegion == player.CurrentRegion && bot.IsWithinRadius(player, WorldMgr.SAY_DISTANCE))
            .Distinct()
            .OrderByDescending(bot => BotPersonalities.For(bot) != null)
            .ThenByDescending(bot => Relevance(bot, message))
            .ThenBy(_ => Random.Shared.Next())
            .ToArray();
        GameBot[] mentioned = eligible.Where(bot => IsMentioned(message, bot)).ToArray();
        bool directed = mentioned.Length > 0;
        // A line for a bot that can't hear it (too far, another zone) isn't the others' to answer.
        if (OthersStayQuiet(mentioned, NamedInRealm(player, message)))
        {
            LogUnheardNames(player, message, faction);
            return;
        }
        // The named bot answers alone, first; now and then another personality bot in reach chips in after it.
        GameBot chipIn = null;
        if (directed)
        {
            chipIn = ChipIn(eligible.Where(bot => !mentioned.Contains(bot) && BotPersonalities.For(bot) != null &&
                (Eligible(bot) || IsPersonalityCompanion(bot))).ToArray(), Random.Shared);
            eligible = mentioned;
        }
        eAutonomousChatIntent read = AutonomousChatIntentModel.Read(message);
        if (IsShortReaction(read))
        {
            ScheduleShortReaction(player, eligible.FirstOrDefault(bot => BotPersonalities.For(bot) != null), read, directed, faction);
            return;
        }
        int count = directed || hasKnowledge || intent is eAutonomousChatIntent.IgnoreOutOfWorld or eAutonomousChatIntent.Abuse
            ? Math.Min(1, eligible.Length)
            : RollResponseCount(eligible.Length);
        // Persoper answers her own owner in faction chat first, on top of the usual one or two.
        if (faction && !directed && PersonalityCompanions().FirstOrDefault(bot => bot.Owner == player) is { } own && eligible.Contains(own))
            (eligible, count) = CompanionFirst(eligible, own, count);
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
                    ? MoodTinted(responder, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared, DialogueFactSource.For(responder, player)))
                    : directed
                        ? GenerateDirectedReply(message)
                        : faction ? GenerateFactionReply(message) : GenerateLocalReply(responder, message);
            if (!TryAddDistinctResponse(scheduledResponses, response, out response))
                continue;
            Schedule(responder, 1_300 + scheduledIndex++ * 1_800 + Random.Shared.Next(1_200), () =>
            {
                if (SayTo(responder, response, faction) && directed && chipIn != null)
                    ScheduleChipIn(player, chipIn, message, response, line => SayTo(chipIn, line, faction));
            }, named: directed);
        }
    }

    /// <summary>
    /// True when a line names someone (<paramref name="named"/>, wherever they are) but no named bot can hear it
    /// (<paramref name="namedHearers"/>): the bots that can hear it then stay quiet instead of answering in its place.
    /// </summary>
    public static bool OthersStayQuiet<T>(IReadOnlyCollection<T> namedHearers, IReadOnlyCollection<T> named) =>
        named is { Count: > 0 } && namedHearers is not { Count: > 0 };

    /// <summary>The bots of the player's realm (world bots and Persoper) that a line names, wherever they are.</summary>
    public static GameBot[] NamedInRealm(GamePlayer player, string message) =>
        AutonomousBotRegistry.Snapshot().Concat(PersonalityCompanions(evenBusy: true))
            .Where(bot => bot != null && bot.Realm == player.Realm && IsMentioned(message, bot))
            .Distinct()
            .ToArray();

    /// <summary>Logs why a world bot the player named didn't hear the line (bug #7: too far, riding, another region...).</summary>
    private static void LogUnheardNames(GamePlayer player, string message, bool faction)
    {
        foreach (GameBot bot in NamedInRealm(player, message).Take(4))
            Log.Info($"AUTONOMOUS_CHAT_NAMED_UNHEARD bot=\"{bot.Name}\" player=\"{player.Name}\" channel={(faction ? "faction" : "say")} " +
                     $"sameRealm={bot.Realm == player.Realm} sameRegion={bot.CurrentRegion == player.CurrentRegion} " +
                     $"distance={(bot.CurrentRegion == player.CurrentRegion ? bot.GetDistanceTo(player).ToString() : "-")} " +
                     $"canAnswer={CanAnswerNamed(bot)} eligible={Eligible(bot)} riding={bot.IsOnStableMasterRoute} horse={bot.IsOnHorse} " +
                     $"helper={bot.IsTemporaryGroupHelper} worldBot={bot.IsAutonomousWorldBot} grouped={bot.Group != null && bot.Group == player.Group}");
    }

    /// <summary>A bot's line in faction chat or /say, when someone can hear it. False when not said.</summary>
    private static bool SayTo(GameBot bot, string line, bool faction) =>
        faction ? HasFactionAudience(bot.Realm) && BotFaction(bot, line) : HasLocalAudience(bot) && bot.Say(line);

    /// <summary>
    /// The bot that chips in after a named bot answered, from the others in reach: one at most and only now
    /// and then (<see cref="ChipInChance"/>), else null.
    /// </summary>
    public static T ChipIn<T>(IReadOnlyList<T> others, Random random) where T : class
    {
        random ??= Random.Shared;
        if (others == null || others.Count == 0 || random.NextDouble() >= ChipInChance)
            return null;
        return others[random.Next(others.Count)];
    }

    /// <summary>
    /// What a bot that chips in says to the player's line: its own voice (mood-tinted, not counted toward its memory
    /// of the player as the named bot's answer is); null for a bot without a personality.
    /// </summary>
    public static string ChipInLine(GameBot bot, GamePlayer player, string message)
    {
        if (BotPersonalities.For(bot) is not { } personality || player == null || string.IsNullOrWhiteSpace(message))
            return null;
        eAutonomousChatIntent intent = AutonomousChatIntentModel.Predict(message);
        return MoodTinted(bot, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared, DialogueFactSource.For(bot, player)));
    }

    /// <summary>
    /// A chip-in a few seconds after the named bot's <paramref name="namedLine"/>: the other bot's own answer to
    /// the player's line, never the same words. Dropped when it is busy by then.
    /// </summary>
    private static void ScheduleChipIn(GamePlayer player, GameBot bot, string message, string namedLine, Func<string, bool> speak)
    {
        string line = ChipInLine(bot, player, message);
        if (line == null)
            return;
        HashSet<string> said = new(StringComparer.OrdinalIgnoreCase) { namedLine?.Trim() ?? string.Empty };
        if (!TryAddDistinctResponse(said, line, out line))
            return;
        Schedule(bot, 2_500 + Random.Shared.Next(1_500), () => speak(line));
    }

    /// <summary>True for the lines that get a short reaction from one bot now and then, never a full answer.</summary>
    public static bool IsShortReaction(eAutonomousChatIntent intent) =>
        intent is eAutonomousChatIntent.Laugh or eAutonomousChatIntent.Sorry or eAutonomousChatIntent.Disagree or
            eAutonomousChatIntent.Away or eAutonomousChatIntent.Back;

    /// <summary>
    /// A personality's short reaction from its "~ reply laugh" (sorry, disagree, away, back) lines, or
    /// null most of the time and always when it has none: a "lol" gets a "heh" now and then, never a joke.
    /// </summary>
    public static string ShortReaction(BotPersonality personality, eAutonomousChatIntent intent, bool named, string speakerName, Random random, DialogueFacts facts = null)
    {
        random ??= Random.Shared;
        if (personality?.Replies.GetValueOrDefault(intent) is not { Length: > 0 } ||
            random.NextDouble() >= (named ? NamedReactionChance : ReactionChance))
            return null;
        return personality.OwnReply(intent, speakerName, random, facts);
    }

    /// <summary>One bot (the named one, else the first personality bot in reach) may react; plain bots stay quiet.</summary>
    private static void ScheduleShortReaction(GamePlayer player, GameBot responder, eAutonomousChatIntent intent, bool named, bool faction)
    {
        if (responder == null || ShortReaction(BotPersonalities.For(responder), intent, named, player.Name, Random.Shared, DialogueFactSource.For(responder, player)) is not { } reaction)
            return;
        Schedule(responder, 1_300 + Random.Shared.Next(1_200), () => SayTo(responder, reaction, faction), named);
    }

    /// <summary>The responders with the companion first and then the first <paramref name="count"/> others: one more answer.</summary>
    public static (T[] Responders, int Count) CompanionFirst<T>(T[] eligible, T companion, int count) where T : class
    {
        T[] others = eligible.Where(bot => bot != companion).ToArray();
        int keep = Math.Clamp(count, 0, others.Length);
        return (others.Take(keep).Prepend(companion).ToArray(), keep + 1);
    }

    /// <summary>
    /// A personality bot's answer to a player's line, as in /say and faction chat: a special reply,
    /// a known answer, or its own voice (mood-tinted). A "lol" or "brb" gets a short reaction now and
    /// then and null otherwise. Null for a bot without a personality.
    /// </summary>
    public static string PersonalityAnswer(GameBot bot, GamePlayer player, string message)
    {
        if (BotPersonalities.For(bot) is not { } personality || player == null || string.IsNullOrWhiteSpace(message))
            return null;
        eAutonomousChatIntent read = AutonomousChatIntentModel.Read(message);
        if (IsShortReaction(read))
            return ShortReaction(personality, read, IsMentioned(message, bot), player.Name, Random.Shared, DialogueFactSource.For(bot, player));
        eAutonomousChatIntent intent = AutonomousChatIntentModel.Predict(message);
        return PersonalitySpecialReply(bot, player, message, intent, IsMentioned(message, bot))
            ?? (AutonomousChatKnowledge.TryAnswer(player, message, out string knowledge)
                ? knowledge
                : MoodTinted(bot, player, personality.Reply(intent, player.Name, PersoperProperties.TONE_DOWN, Random.Shared, DialogueFactSource.For(bot, player))));
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
        // A typed answerer may carry a typed bot's topic on as a scene instead of the usual answers.
        if (!fromPlayer && !hasKnowledge &&
            PersonalityScenes.TryTopicScene(starter, eligible, topic, faction ? eSceneChannel.Faction : eSceneChannel.Nearby, null))
            return;
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
                    BotFaction(responder, response);
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
                    BotFaction(starter, closing);
                else if (!faction && HasLocalAudience(starter))
                    starter.Say(closing);
            });
        }
    }

    /// <summary>
    /// Runs a bot's line after <paramref name="delay"/> ms, or drops it when the bot is busy or gone by then.
    /// A <paramref name="named"/> bot's answer also comes while it rides, and mid-fight after a few tries.
    /// </summary>
    private static void Schedule(GameBot speaker, int delay, Action action, bool named = false, int retriesLeft = NamedReplyRetries)
    {
        _ = new ECSGameTimer(speaker, timer =>
        {
            bool present = named ? CanAnswerNamed(speaker) : Eligible(speaker) || IsPersonalityCompanion(speaker);
            switch (NextReplyStep(present, speaker.InCombat || speaker.IsCasting, named, retriesLeft))
            {
                case eReplyStep.Say:
                    action();
                    break;
                case eReplyStep.Retry:
                    Schedule(speaker, NamedRetryDelayMs, action, true, retriesLeft - 1);
                    break;
            }
            return 0;
        }, delay);
    }

    public enum eReplyStep
    {
        Drop,
        Say,
        Retry,
    }

    /// <summary>
    /// What a due reply does: said when the bot is there and free; a named bot's answer waits for a cast or a
    /// fight to end up to <see cref="NamedReplyRetries"/> times and is then said anyway; anything else is dropped.
    /// </summary>
    public static eReplyStep NextReplyStep(bool present, bool busy, bool named, int retriesLeft) =>
        !present ? eReplyStep.Drop
        : !busy ? eReplyStep.Say
        : !named ? eReplyStep.Drop
        : retriesLeft > 0 ? eReplyStep.Retry
        : eReplyStep.Say;

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
        ContainsMention(message, bot.Name) || BotPersonalities.For(bot) is { } personality &&
        (ContainsMention(message, personality.Name) || personality.Nicknames.Any(nickname => ContainsMention(message, nickname)));

    /// <summary>Persoper when she can talk; <paramref name="evenBusy"/> also while she casts or fights (to answer her name).</summary>
    private static IEnumerable<GameBot> PersonalityCompanions(bool evenBusy = false)
    {
        GameBot persoper = DOL.GS.Commands.PersoperCommandHandler.Active;
        return persoper != null && (evenBusy ? CanAnswerNamed(persoper) : IsPersonalityCompanion(persoper)) ? [persoper] : [];
    }

    /// <summary>
    /// A bot that can answer its name: an active world bot (riding, casting or fighting included; the reply
    /// waits, see Schedule) or a personality companion. Unnamed chat still needs <see cref="Eligible"/>.
    /// </summary>
    public static bool CanAnswerNamed(GameBot bot) =>
        bot is { IsAlive: true, ObjectState: GameObject.eObjectState.Active } &&
        (bot.IsAutonomousWorldBot && !bot.IsTemporaryGroupHelper || bot.IsTemporaryGroupHelper && BotPersonalities.For(bot) != null);

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
            eAutonomousChatIntent.Thank => "You are welcome.",
            eAutonomousChatIntent.Abuse => "Let us keep the conversation helpful.",
            eAutonomousChatIntent.IgnoreOutOfWorld => "Let us keep the conversation focused on the realm.",
            _ => Random.Shared.Next(3) switch { 0 => "What did you need?", 1 => "Perhaps after this task.", _ => "Yes?" },
        };
    }

    private static string Clean(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().TrimEnd('.', '!', '?');
    private static string LowerFirst(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
