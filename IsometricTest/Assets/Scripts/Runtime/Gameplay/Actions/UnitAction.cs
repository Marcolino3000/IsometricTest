// using Runtime.Gameplay.Actions;
//
// namespace Actions
// {
//     public abstract class UnitAction
//     {
//         public ActionCondition Condition;
//         public ActionEffect Effect;
//
//         // public UnitAction(ActionCondition condition, ActionEffect effect)
//         // {
//         //     Condition = condition;
//         //     Effect = effect;
//         // }
//         
//         protected ActionContext Context;
//         public abstract bool TestConditions(ActionContext context);
//         public abstract void ExecuteEffects();
//         
//     }
// }




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
        public abstract bool TestConditions();

        public abstract void ExecuteEffects();
        
    }
}