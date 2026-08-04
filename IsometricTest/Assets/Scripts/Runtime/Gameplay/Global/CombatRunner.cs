using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Global
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            bool targetDied = ApplyDamage(attacker, target, isRetaliation: false);

            bool attackerDied = false;

            // Whether the target strikes back is a rule question, so it is answered by CombatRules
            // (match rules first, then reach) rather than decided here.
            if (CombatRules.CanRetaliate(target, attacker))
                attackerDied = ApplyDamage(target, attacker, isRetaliation: true);

            if (targetDied)
            {
                target.Remove();
            }

            if (attackerDied)
            {
                attacker.Remove();
            }
        }

        private static bool ApplyDamage(Unit attacker, Unit target, bool isRetaliation)
        {
            // Damage is resolved through CombatRules so unit and terrain traits (defence, crits,
            // terrain damage bonuses) are all folded in consistently.
            target.CurrentState.Health -= CombatRules.CalculateDamage(attacker, target, isRetaliation);

            return target.CurrentState.Health <= 0;
        }
    }
}
