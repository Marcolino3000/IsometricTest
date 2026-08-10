using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// The category an item belongs to, which is the slot of the item bar it is offered in. Authored
    /// on the item rather than derived from what it does: traits change a weapon's effective range,
    /// but the slot it is carried in must not change with them.
    ///
    /// **This order is the order of the slots on the bar** (see <see cref="ItemManager.KindForSlot"/>):
    /// melee is slot 0 / key 1, passive is slot 3 / key 4. Reordering it reorders the bar, and
    /// <see cref="None"/> has to stay last - it marks where the categories end.
    /// </summary>
    public enum SlotKind
    {
        Melee,
        Ranged,
        Active,
        Passive,

        /// <summary>Belongs in no slot - e.g. the move action, which is not an item at all.</summary>
        None
    }

    /// <summary>
    /// Anything the player can own and carry in a slot. Holds only what the item bar draws - a symbol
    /// and a tooltip - plus the category it belongs to; what the item actually *does* is the business
    /// of the subclass, and the three kinds do it in three different ways:
    ///
    /// <list type="bullet">
    /// <item>a weapon is an <see cref="Actions.AttackActionData"/>, and equipping it is drawing it,</item>
    /// <item>an <see cref="ActiveItemData"/> is an action, and choosing it is using it,</item>
    /// <item>a <see cref="PassiveItem"/> is a bundle of traits, and choosing it is wearing them.</item>
    /// </list>
    ///
    /// The bar knows none of that: it draws icons and reports which one was chosen, and
    /// <see cref="ItemManager"/> is the only translator.
    /// </summary>
    public abstract class Item : ScriptableObject
    {
        public string DisplayName;

        [TextArea] public string Description;

        public Sprite Symbol;

        /// <summary>The slot category this item is offered in.</summary>
        public virtual SlotKind Slot => SlotKind.None;

        public string Tooltip => string.IsNullOrWhiteSpace(Description) ? name : $"{name}\n{Description}";
    }
}
