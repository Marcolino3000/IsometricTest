using System.Collections.Generic;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// An item that does nothing by itself: while it sits in the passive slot its traits are on the
    /// character's <see cref="Entities.UnitState.Traits"/>, where
    /// <see cref="Global.CombatRules"/> already folds them in with the ones the blueprint and the
    /// ground underfoot contribute - so a passive item needs no rule code of its own.
    ///
    /// The slot holds one item, so the character wears one set of traits: choosing another passive
    /// takes the previous one's traits back off.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Passive Item")]
    public class PassiveItem : Item
    {
        [Tooltip("Traits granted to whoever carries this item. Drag unit trait assets here.")]
        public List<UnitTrait> Traits = new();

        public override SlotKind Slot => SlotKind.Passive;
    }
}
