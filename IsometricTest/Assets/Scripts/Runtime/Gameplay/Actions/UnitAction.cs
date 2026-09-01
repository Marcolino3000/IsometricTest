using Runtime.Gameplay.Actions;
using Runtime.Gameplay.History;

namespace Actions
{
    /// <summary>
    /// One planned use of an action: what it costs and the context it runs in. What it <i>does</i> is
    /// the subclass's - the effects sit on the authored action, not here; see the note on
    /// <see cref="ActionData{UCondition}"/>.
    /// </summary>
    public abstract class UnitAction<UCondition> : IUnitAction
        where UCondition : ActionCondition
    {
        public UCondition Condition;

        protected ActionContext Context;

        protected UnitAction(UCondition condition, ActionContext context)
        {
            Condition = condition;
            Context = context;
        }

        public virtual int Cost => Condition.Cost;

        // Abstract on purpose: an action that can be planned has to say what it is, or it would be
        // shown as whatever the default happened to be.
        public abstract ActionKind Kind { get; }

        public abstract bool TestConditions();

        public abstract void ExecuteEffects();
    }
}
