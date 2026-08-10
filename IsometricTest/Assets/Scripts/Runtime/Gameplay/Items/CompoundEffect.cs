using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Several effects applied in order as one use. An <see cref="ActiveItemData"/> holds a single
    /// effect, so this is how an item does two things at once - a ration that heals and refreshes -
    /// without either a list on the item or a class per combination.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Effects/Compound")]
    public class CompoundEffect : ActiveItemEffect
    {
        [Tooltip("Applied top to bottom. Nesting a compound inside itself would not terminate - the " +
                 "direct case is caught, a longer cycle is not.")]
        public List<ActiveItemEffect> Effects = new();

        /// <summary>The parts, in the order they are applied - the compound itself does nothing to say.</summary>
        public override string Summary
        {
            get
            {
                var parts = new List<string>();

                foreach (var effect in Effects)
                    if (effect != null && effect != this)
                        parts.Add(effect.Summary);

                return string.Join(", ", parts);
            }
        }

        public override void Apply(Unit user)
        {
            foreach (var effect in Effects)
            {
                if (effect != null && effect != this)
                    effect.Apply(user);
            }
        }
    }
}
