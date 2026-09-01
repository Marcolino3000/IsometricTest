using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// An item that is used rather than worn: an action the character can take, carried in a slot.
    /// It is an <see cref="ActionData{UCondition}"/> like a weapon is, so it costs action points, is
    /// tested before it runs and announces itself to the history through the same path an attack
    /// does - which is what makes it undoable without any history code of its own.
    ///
    /// Self-targeted for now: choosing it in the picker uses it on the character, and the use consumes
    /// it. Aiming one at a tile or another unit would need the selection pipeline to hold an armed
    /// action, which it cannot do yet.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Active Item")]
    public class ActiveItemData : ActionData<ActiveItemCondition>
    {
        public override SlotKind Slot => SlotKind.Active;

        /// <summary>
        /// What a use does, applied top to bottom. A list rather than a single effect because an
        /// active item's effect is a <b>verb</b>: "restore 4 health" and "restore 2 points" compose
        /// by being run one after the other, so a ration that does both is two entries here. An
        /// effect the rules read as a number (a weapon's damage) is single for the opposite reason -
        /// see the note on <see cref="ActionData{UCondition}"/>.
        /// </summary>
        public IReadOnlyList<ActiveItemEffect> Effects => effects;

        [Tooltip("Applied top to bottom when the item is used.")]
        [SerializeReference] private List<ActiveItemEffect> effects = new();

        /// <summary>
        /// What a use takes and what it gives: the cost every action has, and each effect speaking
        /// for itself - which is why a new active item still needs no code beyond its effect.
        /// </summary>
        public override IReadOnlyList<string> Stats
        {
            get
            {
                var stats = new List<string>();

                if (Condition != null)
                    stats.Add($"Cost {Condition.Cost} AP");

                if (effects != null)
                    foreach (var effect in effects)
                        if (effect != null)
                            stats.Add(effect.Summary + effect.TargetSummary);

                return stats;
            }
        }

        public override IUnitAction CreateAction(ActionContext context)
        {
            return new ActiveItemAction(Condition, effects, context);
        }
    }
}
