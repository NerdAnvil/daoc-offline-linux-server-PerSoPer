using System.Linq;

namespace DOL.GS.Commands
{
    /// <summary>
    /// The bot dialogue files from in game: "/dialogue" shows where they came from and their
    /// problems, "/dialogue reload" reads the release folder's dialogue/ again (DialogueLibrary).
    /// daoc-admin has the same as "dialogue" and "dialogue reload".
    /// </summary>
    [CmdAttribute("&dialogue", ePrivLevel.GM, "Shows or reloads the bot dialogue files", "/dialogue [reload]")]
    public sealed class DialogueCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        /// <summary>Problems listed in chat at most; the rest are in server.log and daoc-admin.</summary>
        private const int MaxProblemsShown = 10;

        public void OnCommand(GameClient client, string[] args)
        {
            if (client?.Player == null)
                return;

            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
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
