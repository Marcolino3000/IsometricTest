namespace Runtime
{
    public class CombatRunner
    {
        public static void ResolveCombat(Unit attacker, Unit target)
        {
            bool targetDied = ApplyDamage(attacker, target);
            bool attackerDied = ApplyDamage(target, attacker);

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