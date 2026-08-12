using Runtime.Gameplay.Entities;

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

            bool targetDied = ApplyDamage(attacker, target, isRetaliation: false);

            bool attackerDied = false;

            // Whether the target strikes back is a rule question, so it is answered by CombatRules
            // (match rules first, then reach) rather than decided here.
            if (CombatRules.CanRetaliate(target, attacker))
                attackerDied = ApplyDamage(target, attacker, isRetaliation: true);

            if (targetDied)
            {
                CombatLog.Removed(target);
                target.Remove();
            }

            if (attackerDied)
            {
                CombatLog.Removed(attacker);
                attacker.Remove();
            }

            CombatLog.EndAttack();
        }

        private static bool ApplyDamage(Unit attacker, Unit target, bool isRetaliation)
        {
            // Damage is resolved through CombatRules so unit and terrain traits (defence, crits,
            // terrain damage bonuses) are all folded in consistently.
            var damage = CombatRules.CalculateDamage(attacker, target, isRetaliation);

            // Whatever a trait had to say about this strike goes to the unit before the health does:
            // the popup is raised by the health change below, which knows only how much was lost.
            target.NoteNextDamage(StrikeNotes.Collect());

            var healthBefore = target.CurrentState.Health;
            target.CurrentState.Health -= damage;

            CombatLog.Applied(healthBefore, target.CurrentState.Health);

            return target.CurrentState.Health <= 0;
        }
    }
}
