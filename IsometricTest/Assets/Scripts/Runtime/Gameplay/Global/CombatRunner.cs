using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            bool targetDied = ApplyDamage(attacker, target);

            bool attackerDied = false;

            // The target only strikes back if the attacker is within the targets attack range.
            if (IsInAttackRange(target, attacker))
                attackerDied = ApplyDamage(target, attacker);

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
            return distance <= attacker.CurrentState.AttackAction.Condition.Range;
        }

        private static int GetManhattanDistance(Tile attackerTile, Tile targetTile)
        {
            int dx = Mathf.Abs(attackerTile.Position.x - targetTile.Position.x);
            int dy = Mathf.Abs(attackerTile.Position.y - targetTile.Position.y);
            return dx + dy;
        }


        private static bool ApplyDamage(Unit attacker, Unit target)
        {
            target.CurrentState.Health -= CalculateDamage(attacker, target);

            return target.CurrentState.Health <= 0;
        }

        private static int CalculateDamage(Unit attacker, Unit target)
        {
            //subtract target's defense
            return attacker.CurrentState.AttackAction.Effect.Damage;
        }
    }
}