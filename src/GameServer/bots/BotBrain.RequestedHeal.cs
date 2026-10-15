using System.Collections.Generic;
using System.Linq;
using DOL.GS;

namespace DOL.AI.Brain
{
    /// <summary>
    /// Heals a player asked for (PersonalityHeals): one single-target heal, cast now,
    /// outside the normal heal check. Kept in its own file so the brain's own healing
    /// (CheckHeals) is unchanged.
    /// </summary>
    public partial class BotBrain
    {
        public PersonalityHeals.eHealOutcome CastRequestedHeal(PersonalityHeals.eHealSize size, GameLiving target, bool panic)
        {
            if (target == null || !target.IsAlive)
                return PersonalityHeals.eHealOutcome.OutOfReach;
            if (Body.IsSilenced || Body.IsStunned || Body.IsMezzed)
                return PersonalityHeals.eHealOutcome.CantCast;

            // Every single-target heal it knows (no group, area or over-time heals).
            Spell[] heals = (BotBody.HealSpells ?? []).Concat(BotBody.InstantHealSpells ?? [])
                .Where(spell => spell.SpellType == eSpellType.Heal && spell.Target == eSpellTarget.REALM && spell.Radius == 0)
                .Distinct()
                .ToArray();
            if (heals.Length == 0)
                return PersonalityHeals.eHealOutcome.NoHeal;

            var usable = heals.Where(CheckHealSpell)
                .Select(spell => new PersonalityHeals.HealOption<Spell>(spell, BotBody.PowerCost(spell), GameBot.HealAmount(spell, target), spell.IsInstantCast))
                .ToList();
            if (usable.Count == 0)
                return heals.All(spell => BotBody.Mana < BotBody.PowerCost(spell))
                    ? PersonalityHeals.eHealOutcome.NoPower
                    : PersonalityHeals.eHealOutcome.Cooldown;

            Spell heal = PersonalityHeals.Choose(usable, size);
            if (!BotBody.IsWithinRadius(target, BotBody.castingComponent.CalculateSpellRange(heal)))
                return ApproachSupportTarget(target)
                    ? PersonalityHeals.eHealOutcome.Approaching
                    : PersonalityHeals.eHealOutcome.OutOfReach;
            _supportReach.Reached(target);

            if (!heal.IsInstantCast)
            {
                if (BotBody.IsCasting)
                {
                    // A panic call cuts through anything; a normal request waits for a heal in progress.
                    bool castingHeal = BotBody.castingComponent.SpellHandler?.Spell?.IsHealing == true;
                    if (castingHeal && !panic)
                        return PersonalityHeals.eHealOutcome.Busy;
                    BotBody.StopCurrentSpellcast();
                }
                else if (BotBody.IsAttacking)
                    BotBody.StopAttack();
            }

            GameObject oldTarget = BotBody.TargetObject;
            BotBody.TargetObject = target;
            bool started = BotBody.CastSpell(heal, m_mobSpellLine, false);
            if (!started || heal.IsInstantCast)
                BotBody.TargetObject = oldTarget;
            return started ? PersonalityHeals.eHealOutcome.Cast : PersonalityHeals.eHealOutcome.CantCast;
        }
    }
}
