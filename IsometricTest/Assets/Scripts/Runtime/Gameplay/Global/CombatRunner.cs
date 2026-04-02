using Runtime.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            var attackerTile = attacker.CurrentState.Position;
            var targetTile = target.CurrentState.Position;

            if (!attacker.IsTileWithinReach(targetTile, false))
                return;
            
            int distance = GetManhattanDistance(attackerTile, targetTile);

            bool targetDied = ApplyDamage(attacker, target);

            bool attackerDied = false;
            if (distance <= target.CurrentState.Range)
            {
                attackerDied = ApplyDamage(target, attacker);
            }

            var formerTargetPosition = target.CurrentState.Position;

            if (targetDied)
            {
                target.Remove();    
            }

            if (attackerDied)
            {
                attacker.Remove();
                return;
            }

            attacker.TryMoveToTile(formerTargetPosition);
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
            return attacker.Blueprint.Attack - target.Blueprint.Defense;
        }
    }
}