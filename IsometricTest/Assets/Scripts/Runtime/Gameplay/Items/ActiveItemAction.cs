using Actions;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.History;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// One use of an <see cref="ActiveItemData"/>. The effect is the only thing that varies between
    /// items, so this class stays the same for every one of them - a new active item is a new effect
    /// asset, not a new action class.
    /// </summary>
    public class ActiveItemAction : UnitAction<ActiveItemCondition, ActiveItemEffect>
    {
        public ActiveItemAction(ActiveItemCondition condition, ActiveItemEffect effect, ActionContext context)
            : base(condition, effect, context) { }

        public override ActionKind Kind => ActionKind.UseItem;

        public override bool TestConditions()
        {
            if (Effect == null || Context.Unit == null || !Context.Unit.IsAlive)
                return false;

            return Condition.Cost <= Context.ActionPoints;
        }

        public override void ExecuteEffects()
        {
            Effect.Apply(Context.Unit);
        }
    }
}
