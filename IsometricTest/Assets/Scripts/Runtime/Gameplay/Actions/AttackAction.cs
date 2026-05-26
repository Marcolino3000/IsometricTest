using Runtime.Gameplay.Actions;

namespace Actions
{
    public class AttackAction : UnitAction<AttackCondition, AttackEffect>
    {
        public AttackAction(AttackCondition condition, AttackEffect effect, ActionContext context) : base(condition, effect, context) { }

        public override bool TestConditions()
        {
            if(Condition.Cost > Context.ActionPoints) 
                return false;
            
            if(Condition.Range < Context.Distance)
                return false;
            
            return true;
        }

        public override void ExecuteEffects()
        {
            Context.TargetUnit.CurrentState.Health -= Effect.Damage;
        }
    }
}