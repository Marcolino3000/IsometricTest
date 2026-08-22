using Runtime.Gameplay.Actions;
using Runtime.Gameplay.History;

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
        public virtual int Cost => Condition.Cost;

        // Abstract on purpose: an action that can be planned has to say what it is, or it would be
        // shown as whatever the default happened to be.
        public abstract ActionKind Kind { get; }

        public abstract bool TestConditions();

        public abstract void ExecuteEffects();
        
    }
}