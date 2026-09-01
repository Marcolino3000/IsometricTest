using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.History;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// One use of an <see cref="ActiveItemData"/>. The effects are the only thing that varies between
    /// items, so this class stays the same for every one of them - a new active item is a new effect
    /// class, not a new action class.
    /// </summary>
    public class ActiveItemAction : UnitAction<ActiveItemCondition>
    {
        private readonly IReadOnlyList<ActiveItemEffect> effects;

        public ActiveItemAction(ActiveItemCondition condition, IReadOnlyList<ActiveItemEffect> effects,
            ActionContext context) : base(condition, context)
        {
            this.effects = effects;
        }

        public override ActionKind Kind => ActionKind.UseItem;

        public override bool TestConditions()
        {
            // An item with nothing to do is refused rather than spent, the way one with no effect
            // authored used to be.
            if (effects == null || effects.Count == 0 || Context.Unit == null || !Context.Unit.IsAlive)
                return false;

            return Condition.Cost <= Context.ActionPoints;
        }

        public override void ExecuteEffects()
        {
            // Self-targeted unless an effect says otherwise: with no selector of its own, an effect
            // resolves to the context's target, which for an item used from its slot is the user.
            var context = EffectContext.SelfTargeted(Context.Unit);

            foreach (var effect in effects)
            {
                if (effect == null)
                    continue;

                foreach (var target in effect.ResolveTargets(context))
                    effect.Apply(target);
            }
        }
    }
}
