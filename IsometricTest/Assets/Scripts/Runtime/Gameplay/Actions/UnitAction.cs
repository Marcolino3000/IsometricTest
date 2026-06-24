using Runtime.Gameplay.Actions;

namespace Actions
{
    public abstract class UnitAction<UCondition, TEffect> : IUnitAction
        where UCondition : ActionCondition 
        where TEffect : ActionEffect
    {
        public UCondition Condition;
        public TEffect Effect;

        public UnitAction(UCondition condition, TEffect effect, ActionContext context)
        {
            Condition = condition;
            Effect = effect;
            Context = context;
        }
        
        protected ActionContext Context;
        public int Cost => Condition.Cost;
        public abstract bool TestConditions();

        public abstract void ExecuteEffects();
        
    }
}