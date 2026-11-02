using System.Linq;

namespace DOL.GS.Commands
{
    /// <summary>
    /// The bot dialogue files from in game: "/dialogue" shows where they came from and their
    /// problems, "/dialogue reload" reads the release folder's dialogue/ again (DialogueLibrary),
    /// "/dialogue scene joker-loner knock-knock" plays one scene of scenes.dlg now (PairScenes),
    /// "/dialogue ask fav_music" has Persoper put that question to the GM now (PersoperAsks),
    /// "/dialogue answers" lists the GM's own saved answers (PlayerAnswers).
    /// "/dialogue gather", "/dialogue joke Shnacksize", "/dialogue ambient Shnacksize", "/dialogue walkoff always" and "/dialogue forget" set up in-game checks of
    /// bot-to-bot and ambient lines (tools/check-groans-ingame.sh, tools/check-night-lines-ingame.sh);
    /// "/dialogue state Shnacksize" shows what a bot's answers about itself are filled with (tools/check-state-ingame.sh).
    /// daoc-admin has the same as "dialogue" and "dialogue reload".
    /// </summary>
    [CmdAttribute("&dialogue", ePrivLevel.GM, "Shows or reloads the bot dialogue files, plays a scene, or has Persoper ask a question",
        "/dialogue [reload]", "/dialogue scene <pair> <scene>   (e.g. /dialogue scene joker-loner knock-knock)",
        "/dialogue ask [question]   (e.g. /dialogue ask fav_music; Persoper must be summoned)",
        "/dialogue answers   (your own saved answers to the bots' questions)",
        "/dialogue gather   (moves the bots in your group beside you)",
        "/dialogue joke <bot>   (a bot in your group tells a joke there now; its groupmates answer)",
        "/dialogue ambient <bot>   (a bot in your group says one of its own ambient lines there now)",
        "/dialogue state <bot>   (what a bot in your group would say about itself: zone, level, class, what it's doing)",
        "/dialogue walkoff always|normal|reset   (test: every groan at your bad joke gets the walk-off gag, or only rarely; reset forgets your 24 h cooldown)",
        "/dialogue forget   (forgets lines said: cooldowns, [once] and recent lines start fresh)")]
    public sealed class DialogueCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        /// <summary>Problems listed in chat at most; the rest are in server.log and daoc-admin.</summary>
        private const int MaxProblemsShown = 10;

        public void OnCommand(GameClient client, string[] args)
        {
            if (client?.Player == null)
                return;

            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
            if (verb == "scene" && args.Length == 4)
            {
                ChatUtil.SendSystemMessage(client, PersonalityScenes.PlayNamedScene(client.Player.Realm, args[2], args[3]));
                return;
            }
            if (verb == "answers" && args.Length == 2)
            {
                // The GM's own saved answers, read through the cache (the database on a first look).
                var answers = PlayerAnswers.Cache.For(client.Player.InternalID);
                ChatUtil.SendSystemMessage(client, answers.Count == 0
                    ? "Saved answers: none."
                    : "Saved answers: " + string.Join(", ", answers.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
                return;
            }
            if (verb == "ask" && args.Length <= 3)
            {
                GameBot persoper = PersoperCommandHandler.Active;
                string result = persoper == null
                    ? "Persoper isn't summoned. Use /persoper first."
                    : PersoperAsks.Ask(persoper, client.Player, args.Length == 3 ? args[2] : null, null, System.DateTime.UtcNow);
                ChatUtil.SendSystemMessage(client, result ?? "Persoper asks. Answer in chat.");
                return;
            }
            if (verb == "gather" && args.Length == 2)
            {
                int moved = GroupBots(client.Player).Count(bot => BotGroupInvite.TryMoveBesidePlayer(bot, client.Player));
                ChatUtil.SendSystemMessage(client, $"Gathered {moved} bot(s) beside you.");
                return;
            }
            if (verb == "joke" && args.Length == 3)
            {
                ChatUtil.SendSystemMessage(client, TellJoke(client.Player, args[2]));
                return;
            }
            if (verb == "state" && args.Length == 3)
            {
                ChatUtil.SendSystemMessage(client, BotState(client.Player, args[2]));
                return;
            }
            if (verb == "ambient" && args.Length == 3)
            {
                ChatUtil.SendSystemMessage(client, SayAmbient(client.Player, args[2]));
                return;
            }
            if (verb == "walkoff" && args.Length == 3 && args[2].Equals("reset", System.StringComparison.OrdinalIgnoreCase))
            {
                PlayerAnswers.Cache.Set(client.Player.InternalID, PersonalityWalkOff.CooldownKey, "0");
                ChatUtil.SendSystemMessage(client, "Walk-off: your 24 h cooldown is forgotten.");
                return;
            }
            if (verb == "walkoff" && args.Length == 3 && args[2].ToLowerInvariant() is "always" or "normal")
            {
                PersonalityWalkOff.Always = args[2].Equals("always", System.StringComparison.OrdinalIgnoreCase);
                ChatUtil.SendSystemMessage(client, PersonalityWalkOff.Always
                    ? "Walk-off: every groan at your joke gets the gag now (no session roll, no cooldown)."
                    : "Walk-off: back to the normal rare chance and cooldown.");
                return;
            }
            if (verb == "forget" && args.Length == 2)
            {
                DialoguePicker.Shared.Clear();
                ChatUtil.SendSystemMessage(client, "Forgot the lines said: cooldowns and [once] lines start fresh.");
                return;
            }
            if (verb is not ("status" or "reload"))
            {
                DisplaySyntax(client);
                return;
            }

            DialogueLoadReport report = verb == "reload" ? DialogueLibrary.Reload() : DialogueLibrary.LastLoad;
            // Sent as they are: dialogue lines hold {0}, which a formatted message would trip on.
            ChatUtil.SendSystemMessage(client, (verb == "reload" ? "Reloaded. " : "") + report.Summary);
            if (report.FromFolder.Count > 0)
                ChatUtil.SendSystemMessage(client, "From the folder: " + string.Join(", ", report.FromFolder));
            foreach (string problem in report.Errors.Take(MaxProblemsShown))
                ChatUtil.SendSystemMessage(client, problem);
            if (report.Errors.Count > MaxProblemsShown)
                ChatUtil.SendSystemMessage(client, $"...and {report.Errors.Count - MaxProblemsShown} more in logs/server.log.");
        }

        /// <summary>The bots in the player's group (a group holds 8 at most).</summary>
        private static GameBot[] GroupBots(GamePlayer player) =>
            player.Group?.GetMembersInTheGroup().OfType<GameBot>().Take(ServerProperties.Properties.GROUP_MAX_MEMBER).ToArray() ?? [];

        /// <summary>The bot in the player's group with this name or personality key, or null.</summary>
        private static GameBot GroupBot(GamePlayer player, string name) =>
            GroupBots(player).FirstOrDefault(member => string.Equals(member.Name, name, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(BotPersonalities.For(member)?.Key, name, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>What a bot in the group would fill its answers about itself with (PersonalityStateAnswers), on one line.</summary>
        private static string BotState(GamePlayer player, string name)
        {
            GameBot bot = GroupBot(player, name);
            if (bot == null)
                return $"No bot called {name} in your group.";
            StateSlots slots = PersonalityStateAnswers.SlotsFor(bot);
            return $"State {bot.Name}: zone={slots.Zone}; level={slots.Level}; class={slots.ClassName}; doing={PersonalityStateAnswers.DoingOf(bot)}; " +
                   $"grouped={(bot.Group?.MemberCount > 1 ? "yes" : "no")}; groupsize={slots.GroupSize}; leader={slots.Leader}; " +
                   $"monster={slots.Monster}; destination={slots.Destination}";
        }

        /// <summary>The named bot says a line from its own ambient list in group chat now (night lines at night).</summary>
        private static string SayAmbient(GamePlayer player, string name)
        {
            GameBot bot = GroupBot(player, name);
            if (bot == null)
                return $"No bot called {name} in your group.";
            string line = PersonalityReactions.OwnAmbientLine(bot, BotPersonalities.For(bot), System.Random.Shared);
            if (line == null)
                return $"{bot.Name} has no ambient lines.";
            return AutonomousBotChatCoordinator.SpeakInGroup(bot, line, null)
                ? $"{bot.Name} says an ambient line."
                : $"{bot.Name} can't talk now (fighting, casting or riding?).";
        }

        /// <summary>The named bot (name or personality key) tells a joke in group chat now, as if it had opened a topic itself.</summary>
        private static string TellJoke(GamePlayer player, string name)
        {
            GameBot bot = GroupBot(player, name);
            if (bot == null)
                return $"No bot called {name} in your group.";
            string joke = PersonalityReactions.TopicOpener(bot, eChatTopic.Fun, System.Random.Shared);
            if (joke == null)
                return $"{bot.Name} has no jokes.";
            return AutonomousBotChatCoordinator.SpeakInGroup(bot, joke, eChatTopic.Fun)
                ? $"{bot.Name} tells a joke."
                : $"{bot.Name} can't talk now (fighting, casting or riding?).";
        }
    }
}
