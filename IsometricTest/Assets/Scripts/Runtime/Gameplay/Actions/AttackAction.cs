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
            
            if(Condition.Range < Context.Distance)
                return false;
            
            return true;
        }

        public override void ExecuteEffects()
        {
            CombatRunner.ResolveCombat(Context.Unit, Context.TargetUnit);
        }
    }
}