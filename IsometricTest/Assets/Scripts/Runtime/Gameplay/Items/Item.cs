using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// The category an item belongs to, which decides the slots of the item bar it is offered in.
    /// Authored on the item rather than derived from what it does: traits change a weapon's effective
    /// range, but the slot it is carried in must not change with them.
    ///
    /// Which slots a category gets - and how many, since an active item gets one each - is the layout
    /// table in <see cref="ItemManager"/>, not this order. This order is serialized on every item asset
    /// and in the loot settings, so it is not free to change; <see cref="None"/> has to stay last, as
    /// it marks where the categories end and everything counting them reads it as the count.
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

        /// <summary>What to call this item, falling back to the asset name while none is authored.</summary>
        public string Title => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        /// <summary>
        /// What this item does in numbers, one short line each ("Damage 5", "+2 Defense") - what the
        /// find popup shows under the description. An item answers this from its own fields: a kind of
        /// item that carries different numbers overrides it rather than being taken apart from outside,
        /// so nothing has to switch over the three kinds to describe one.
        /// </summary>
        public virtual IReadOnlyList<string> Stats => Array.Empty<string>();

        /// <summary>
        /// What a category is called to the player. Kept next to the enum it reads, since a further
        /// category has to be given a name here the same moment it is given a slot.
        /// </summary>
        public static string NameOf(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.Melee => "Melee Weapon",
                SlotKind.Ranged => "Ranged Weapon",
                SlotKind.Active => "Active Item",
                SlotKind.Passive => "Passive Item",
                _ => string.Empty
            };
        }
    }
}
