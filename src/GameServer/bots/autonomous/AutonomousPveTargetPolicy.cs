using System;
using System.Linq;
using DOL.AI.Brain;

namespace DOL.GS
{
    /// <summary>Execution of an assigned PvE goal, separate from difficulty planning.</summary>
    public static class AutonomousPveTargetPolicy
    {
        // The caller separately checks life, region, attack legality and route.
        // Any real monster level is allowed, including grey or purple targets
        // and level 0 creatures (blue to a level 1; used by starter camps).
        public static bool IsAssignedTarget(string assignedName, string monsterName, int monsterLevel) =>
            monsterLevel >= 0 && !string.IsNullOrWhiteSpace(assignedName) &&
            string.Equals(assignedName, monsterName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Vertical gap beyond which a flying monster cannot be pulled from the bot's floor.</summary>
        public const int UnreachableFlyerHeight = 400;

        // Flying spawns such as the Gripklosa griffon gliders hover thousands of
        // units over the camp floor: melee could never reach them and pulls timed
        // out (1,195 times in one run). Low flyers (lights, elementals) still count.
        // A high flyer that attacks the bot is fought normally by its defense.
        public static bool IsUnreachableFlyer(GameNPC.eFlags flags, int monsterZ, int botZ) =>
            (flags & GameNPC.eFlags.FLYING) != 0 && Math.Abs(monsterZ - botZ) > UnreachableFlyerHeight;

        // Owner 2026-10-25: a solo Shadowblade at a hill cat camp picked the cat the
        // owner was already fighting (it was the nearest) and joined in, or ran up
        // after it died and ran off again. A monster that is fighting anyone outside
        // the bot's own group is someone else's pull and is left alone. This only
        // gates new pulls: a monster that turns on the bot is still fought.
        public static bool IsEngagedByOutsider(GameBot bot, GameNPC npc)
        {
            if (!npc.InCombat)
                return false;
            // A realm raid or warband is several parties sharing one fight, so sister
            // parties count as the bot's own side (null scope: a solo bot).
            object scope = AutonomousRealmRaid.SupportScope(bot);
            bool targetIsOutsider = npc.TargetObject is GameLiving target && IsOutsider(bot, scope, target);
            bool outsiderDamagedIt = false;
            if (!targetIsOutsider)
                lock (npc.XpGainersLock)
                    outsiderDamagedIt = npc.XPGainers.Keys.Any(living => IsOutsider(bot, scope, living));
            return IsEngagedByOutsider(true, targetIsOutsider, outsiderDamagedIt);
        }

        // A pet counts as its owner, so a player's pet fighting a monster claims it too.
        private static bool IsOutsider(GameBot bot, object scope, GameLiving living)
        {
            if (living is GameNPC { Brain: IControlledBrain pet } && pet.GetLivingOwner() is GameLiving owner)
                living = owner;
            bool sameSide = bot.Group != null && living.Group == bot.Group ||
                scope != null && living is GameBot other && AutonomousRealmRaid.SupportScope(other) == scope;
            return IsOutsider(living == bot, sameSide);
        }

        public static bool IsOutsider(bool isSelf, bool sameSide) => !isSelf && !sameSide;

        public static bool IsEngagedByOutsider(bool monsterInCombat, bool targetIsOutsider, bool outsiderDamagedIt) =>
            monsterInCombat && (targetIsOutsider || outsiderDamagedIt);
    }
}
