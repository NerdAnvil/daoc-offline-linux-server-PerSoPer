using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.Commands;

namespace DOL.GS
{
    /// <summary>
    /// A bot's line about how a pull cons to the player (grindred, grindpurple, grinddeep): before a /grind pull, a
    /// /pull and a pull order in chat (PlayerLedPullCoordinator). At most one every <see cref="CooldownMs"/> per player,
    /// said by a random bot of the group, not the one that spoke last while another has a line.
    /// </summary>
    public static class PullConLines
    {
        public const long CooldownMs = 180_000;
        public const int MaxSpeakers = 8;
        private const int MaxRemembered = 256;

        private static readonly object Sync = new();
        private static readonly Dictionary<string, (long Next, int LastSpeaker)> State = new(StringComparer.Ordinal);

        /// <summary>The bots in the order they are asked for a line: random, the last speaker at the end.</summary>
        public static IEnumerable<T> SpeakerOrder<T>(IEnumerable<T> bots, Func<T, int> idOf, int lastSpeaker, Random random) =>
            bots.OrderBy(bot => idOf(bot) == lastSpeaker ? 1 : 0).ThenBy(_ => random.Next());

        /// <summary>Says a con line for a pull on <paramref name="target"/> when it cons red or higher and the cooldown is over.</summary>
        public static void BeforePull(GamePlayer player, GameLiving target)
        {
            // Bots without lines (plain bots, /spawn helpers) have nothing to say: skip the con check then.
            if (player?.Group == null || target == null ||
                !player.Group.GetMembersInTheGroup().OfType<GameBot>().Any(bot => BotPersonalities.For(bot) != null) ||
                PlayerCompanionGrind.LineFor(PlayerCompanionGrind.ConOf(
                    player.GetConLevel(target), target.Level - player.Level)) is not { } kind || kind == eAutonomousChatIntent.GrindGrey)
                return;
            long now = GameLoop.GameLoopTime;
            int last;
            lock (Sync)
            {
                if (State.TryGetValue(player.InternalID ?? player.Name, out var state) && now < state.Next)
                    return;
                last = state.LastSpeaker;
            }
            if (Say(player, kind, last) is { } speaker)
                lock (Sync)
                {
                    if (State.Count >= MaxRemembered) State.Clear();
                    State[player.InternalID ?? player.Name] = (now + CooldownMs, speaker.ObjectID);
                }
        }

        /// <summary>One bot of the player's group says a line of this kind; the bot that said it, or null.</summary>
        public static GameBot Say(GamePlayer player, eAutonomousChatIntent kind, int lastSpeaker = 0)
        {
            GameBot[] bots = player?.Group?.GetMembersInTheGroup().OfType<GameBot>().Where(bot => bot.IsAlive).ToArray();
            if (bots == null) return null;
            return SpeakerOrder(bots, bot => bot.ObjectID, lastSpeaker, Random.Shared).Take(MaxSpeakers)
                .FirstOrDefault(bot => AutonomousBotChatCoordinator.SayGrindLine(bot, player, kind));
        }
    }
}
