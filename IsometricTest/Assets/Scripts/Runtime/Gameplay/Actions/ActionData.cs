using Runtime.Gameplay.Items;
using UnityEngine;
using ActionContext = Runtime.Gameplay.Actions.ActionContext;

namespace Actions
{
    /// <summary>
    /// An authored action. Derives from <see cref="Item"/> because the actions the player chooses
    /// between - the weapon it swings, the potion it drinks - are exactly the things it carries;
    /// one that is not carried (the move action) simply keeps <see cref="SlotKind.None"/> and is
    /// never offered in a slot.
    /// </summary>
    public abstract class ActionData<UCondition, TEffect> : Item
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