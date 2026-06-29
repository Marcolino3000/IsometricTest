using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            bool targetDied = ApplyDamage(attacker, target, isRetaliation: false);

            bool attackerDied = false;

            // The target only strikes back if the attacker is within the targets attack range.
            if (IsInAttackRange(target, attacker))
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

        private static bool IsInAttackRange(Unit attacker, Unit defender)
        {
            int distance = GetManhattanDistance(attacker.CurrentState.Position, defender.CurrentState.Position);
            // Effective range so a ranged unit retaliating from a hill benefits from its terrain bonus.
            return distance <= CombatRules.GetEffectiveAttackRange(attacker);
        }

        private static int GetManhattanDistance(Tile attackerTile, Tile targetTile)
        {
            int dx = Mathf.Abs(attackerTile.Position.x - targetTile.Position.x);
            int dy = Mathf.Abs(attackerTile.Position.y - targetTile.Position.y);
            return dx + dy;
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