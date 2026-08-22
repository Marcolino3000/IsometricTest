using System;
using System.Collections.Generic;
using Data;
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
        /// What the item bar shows on hover — the same three things the find popup puts on its card,
        /// as lines of one string, since the bar labels with a plain text element. Built from
        /// <see cref="Stats"/> like the card is, so a kind of item that carries different numbers says
        /// so in both places at once.
        ///
        /// The name is bold through a rich text tag rather than a style: the whole tooltip is one label,
        /// so the lines cannot be styled apart from each other any other way.
        /// </summary>
        public string Tooltip
        {
            get
            {
                var lines = new List<string> { $"<b>{Title}</b>" };

                if (!string.IsNullOrWhiteSpace(Description))
                    lines.Add(Description);

                bool separated = false;

                foreach (var stat in Stats)
                {
                    if (string.IsNullOrWhiteSpace(stat))
                        continue;

                    // The numbers are set off by a blank line so they read as a block rather than as
                    // further sentences. Written before the first line that survives the filter, so an
                    // item whose numbers all come out empty ends on its description rather than a gap.
                    if (!separated)
                    {
                        lines.Add(string.Empty);
                        separated = true;
                    }

                    lines.Add(stat);
                }

                return string.Join("\n", lines);
            }
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
    }
}
