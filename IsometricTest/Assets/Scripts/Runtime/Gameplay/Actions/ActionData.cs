using UnityEngine;
using ActionContext = Runtime.Gameplay.Actions.ActionContext;

namespace Actions
{
    public abstract class ActionData<UCondition, TEffect> : ScriptableObject 
        where UCondition : ActionCondition 
        where TEffect : ActionEffect
    {
        public UCondition Condition;
        public TEffect Effect;
        
        public abstract UnitAction<UCondition, TEffect> CreateAction(ActionContext context);
    }

    public abstract class ActionCondition : ScriptableObject
    {
        public int Cost;
    }
    
    public abstract class ActionEffect : ScriptableObject
    {
        
    }
}