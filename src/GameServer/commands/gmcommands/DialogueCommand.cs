using System.Linq;

namespace DOL.GS.Commands
{
    /// <summary>
    /// The bot dialogue files from in game: "/dialogue" shows where they came from and their
    /// problems, "/dialogue reload" reads the release folder's dialogue/ again (DialogueLibrary),
    /// "/dialogue scene joker-loner knock-knock" plays one scene of scenes.dlg now (PairScenes),
    /// "/dialogue ask fav_music" has Persoper put that question to the GM now (PersoperAsks),
    /// "/dialogue answers" lists the GM's own saved answers (PlayerAnswers).
    /// daoc-admin has the same as "dialogue" and "dialogue reload".
    /// </summary>
    [CmdAttribute("&dialogue", ePrivLevel.GM, "Shows or reloads the bot dialogue files, plays a scene, or has Persoper ask a question",
        "/dialogue [reload]", "/dialogue scene <pair> <scene>   (e.g. /dialogue scene joker-loner knock-knock)",
        "/dialogue ask [question]   (e.g. /dialogue ask fav_music; Persoper must be summoned)",
        "/dialogue answers   (your own saved answers to the bots' questions)")]
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
    }
}
