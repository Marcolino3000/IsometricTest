using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Actions;

namespace Runtime.Gameplay.Global
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            // The whole exchange - strike, retaliation, removals - is written as one console message,
            // so the counter-strike reads under the strike that provoked it. Costs nothing while
            // GameRules.LogCombatCalculations is off.
            CombatLog.BeginAttack();

            // Everyone the exchange brings down, in the order they fell. Collected rather than
            // removed on the spot because an area effect can fell somebody who is neither of the
            // two, and because the counter-strike is still decided against the board as it stood.
            var fallen = new List<Unit>();

            ApplyDamage(attacker, target, isRetaliation: false, fallen);

            // Whether the target strikes back is a rule question, so it is answered by CombatRules
            // (match rules first, then reach) rather than decided here.
            if (CombatRules.CanRetaliate(target, attacker))
                ApplyDamage(target, attacker, isRetaliation: true, fallen);

            foreach (var unit in fallen)
            {
                CombatLog.Removed(unit);
                unit.Remove();
            }

            CombatLog.EndAttack();
        }

        private static bool ApplyDamage(Unit attacker, Unit target, bool isRetaliation, List<Unit> fallen)
        {
            // Damage is resolved through CombatRules so unit and terrain traits (defence, crits,
            // terrain damage bonuses) are all folded in consistently.
            var damage = CombatRules.CalculateDamage(attacker, target, isRetaliation);

            // Asked before the blow lands, and deliberately: an area effect's conditions read the
            // board as it stood when the swing started, so "only what is already damaged" means
            // damaged beforehand rather than damaged by this very strike. The pairs are carried below.
            var areaHits = CombatRules.PlanAreaEffects(attacker, target, isRetaliation);

            // Here rather than at the action, because this is the one place a blow is struck: a
            // retaliation is a strike of its own with the roles swapped, so it draws itself for free.
            // The flinch is said with the swing rather than off the health, which moves for a heal
            // and for an undo as well - and does not move at all for a hit that was fully absorbed.
            attacker.PlayAttackAnimation();
            target.PlayHitAnimation();

            // Whatever a trait had to say about this strike goes to the unit before the health does:
            // the popup is raised by the health change below, which knows only how much was lost.
            // Collected before any area effect resolves, since each of those opens a strike of its own.
            target.NoteNextDamage(StrikeNotes.Collect());

            var healthBefore = target.CurrentState.Health;
            target.CurrentState.Health -= damage;

            // The health setter raises the popup for anything it moves, and a strike everything was
            // taken out of moves nothing - so the 0 is asked for here, and only here.
            if (damage == 0)
                target.ShowAbsorbedHit();

            CombatLog.Applied(healthBefore, target.CurrentState.Health);

            var died = target.CurrentState.Health <= 0;

            if (died)
                fallen.Add(target);

            ApplyAreaEffects(attacker, areaHits, isRetaliation, fallen);

            return died;
        }

        /// <summary>
        /// Spends the swing's area effects on the units they caught. Each is resolved as its own hit
        /// - through the same damage rules, so defence and terrain still apply - but never through
        /// <see cref="ApplyDamage"/>: an area effect must not carry area effects of its own, or a
        /// cleave would chain across the board, and it provokes no retaliation, which stays the
        /// primary pair's business.
        /// </summary>
        private static void ApplyAreaEffects(Unit attacker, List<EffectHit> hits, bool isRetaliation,
            List<Unit> fallen)
        {
            if (hits.Count == 0)
                return;

            CombatLog.Note($"area effects catch {hits.Count} further unit(s)");

            foreach (var hit in hits)
            {
                var victim = hit.Victim;

                // It may have gone down to the blow itself or to an earlier effect in this same list.
                if (victim == null || !victim.IsAlive || victim.CurrentState.Health <= 0)
                    continue;

                var damage = CombatRules.AreaDamage(hit.Effect, attacker, victim, isRetaliation);

                victim.PlayHitAnimation();

                // The effect names itself over the unit it caught, so a number appearing on somebody
                // who was never attacked explains where it came from.
                StrikeNotes.Add(hit.Effect.Note);
                victim.NoteNextDamage(StrikeNotes.Collect());

                var healthBefore = victim.CurrentState.Health;
                victim.CurrentState.Health -= damage;

                if (damage == 0)
                    victim.ShowAbsorbedHit();

                CombatLog.Applied(healthBefore, victim.CurrentState.Health);

                if (victim.CurrentState.Health <= 0 && !fallen.Contains(victim))
                    fallen.Add(victim);
            }
        }
    }
}
