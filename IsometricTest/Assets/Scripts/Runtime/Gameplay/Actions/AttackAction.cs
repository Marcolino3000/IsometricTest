using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Global;

namespace Actions
{
    public class AttackAction : UnitAction<AttackCondition, AttackEffect>
    {
        public AttackAction(AttackCondition condition, AttackEffect effect, ActionContext context) : base(condition, effect, context) { }

        public override bool TestConditions()
        {
            if(Condition.Cost > Context.ActionPoints)
                return false;

            // Asked of the tile the strike is made from - the end of the approach path for a planned
            // attack, where the unit stands for an immediate one - since both the effective range
            // (terrain bonuses) and the line of fire are properties of where it shoots from.
            return CombatRules.CanAttackFrom(Context.Unit, Context.FromTile ?? Context.Unit.CurrentState.Position,
                Context.TargetTile);
        }

        public override void ExecuteEffects()
        {
            CombatRunner.ResolveCombat(Context.Unit, Context.TargetUnit);
        }
    }
}