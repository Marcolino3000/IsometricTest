using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// An item that is used rather than worn: an action the character can take, carried in a slot.
    /// It is an <see cref="ActionData{UCondition,TEffect}"/> like a weapon is, so it costs action
    /// points, is tested before it runs and announces itself to the history through the same path an
    /// attack does - which is what makes it undoable without any history code of its own.
    ///
    /// Self-targeted for now: choosing it in the picker uses it on the character, and the use consumes
    /// it. Aiming one at a tile or another unit would need the selection pipeline to hold an armed
    /// action, which it cannot do yet.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Active Item")]
    public class ActiveItemData : ActionData<ActiveItemCondition, ActiveItemEffect>
    {
        public override SlotKind Slot => SlotKind.Active;

        /// <summary>
        /// What a use takes and what it gives: the cost every action has, and the effect speaking for
        /// itself - which is why a new active item still needs no code beyond its effect.
        /// </summary>
        public override IReadOnlyList<string> Stats
        {
            get
            {
                var stats = new List<string>();

                if (Condition != null)
                    stats.Add($"Cost {Condition.Cost} AP");

                if (Effect != null)
                    stats.Add(Effect.Summary);

                return stats;
            }
        }

        public override UnitAction<ActiveItemCondition, ActiveItemEffect> CreateAction(ActionContext context)
        {
            return new ActiveItemAction(Condition, Effect, context);
        }
    }
}
