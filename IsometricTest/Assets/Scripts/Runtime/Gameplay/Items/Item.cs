using System;
using System.Collections.Generic;
using Data;
using Runtime.Gameplay.Global;
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

        /// <summary>
        /// A unique find, worn for good in a slot of its own - see <see cref="Artefact"/>. Its own
        /// category rather than a kind of <see cref="Passive"/>, because the layout gives it several
        /// slots where the passive gets one, the loot table places it on its own, and the win for
        /// collecting the set has to be able to tell one apart. Appended rather than slipped in
        /// beside the passive, so nothing already serialized changes meaning.
        /// </summary>
        Artefact,

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

        [Tooltip("Which kind of lootbox this item turns up in - in practice which tier it belongs " +
                 "to. Left empty it is never found, which is what the weapon the character starts " +
                 "with wants. The loot table is authored from this side: a box lists no contents, " +
                 "so an item is put in play by the same asset that defines it.")]
        public LootboxType FoundIn;

        /// <summary>The slot category this item is offered in.</summary>
        public virtual SlotKind Slot => SlotKind.None;

        /// <summary>
        /// What a view labelling this item says - the same three things the find popup puts on its
        /// card, in the one shape everything labelled hands over. Built from <see cref="Stats"/> like
        /// the card is, so a kind of item that carries different numbers says so everywhere at once,
        /// and the view does the formatting: it is a card of its own now rather than one string with
        /// a bold tag baked into it.
        /// </summary>
        public TooltipContent Describe()
        {
            return new TooltipContent(Title, NameOf(Slot), Description, Stats, icon: Symbol);
        }

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
                SlotKind.Artefact => "Artefact",
                _ => string.Empty
            };
        }

        /// <summary>
        /// What a category is for, in one sentence - what an empty slot says when it is pointed at,
        /// since there is no item there to describe itself. Kept beside the name it reads for the
        /// same reason: a further category has to be given both the moment it is given a slot.
        /// </summary>
        public static string DescriptionOf(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.Melee => "A weapon swung at what stands beside you. Choosing one draws it.",
                SlotKind.Ranged => "A weapon fired across the board. Choosing one draws it.",
                SlotKind.Active => "Something used up on the spot - it costs action points and is gone.",
                SlotKind.Passive => "Gear worn for its traits. It works by being carried here.",
                SlotKind.Artefact => "A unique find, worn for good. Collect all three to win.",
                _ => string.Empty
            };
        }
    }
}
