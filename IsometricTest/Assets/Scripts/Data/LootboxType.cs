using Runtime.Gameplay.Items;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// One kind - in practice one tier - of lootbox: what it looks like, what it costs to take, how
    /// many boxes of it a match contains and how many of those a fallen enemy leaves behind rather
    /// than lying about on the ground.
    ///
    /// It deliberately does **not** list what may be inside it. That is authored the other way round:
    /// every <see cref="Item"/> names the kind of box it turns up in on <see cref="Item.FoundIn"/>,
    /// so a new item is placed in the loot table by the same asset that defines it and no second
    /// list has to be kept in step. A kind is therefore a whole ScriptableObject holding nothing but
    /// its own look and its own numbers, and a further one is a further asset and no code.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Loot/LootboxType")]
    public class LootboxType : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("What this kind of box is called. Falls back to the asset name while empty.")]
        public string DisplayName;

        [Tooltip("The sprite a box of this kind is drawn with. Replaces whatever the shared prefab " +
                 "carries, so a further kind needs a further asset and no further prefab - and so " +
                 "the tier of a box is legible on the board before it is opened.")]
        public Sprite Sprite;

        [Tooltip("Action points taking a box of this kind costs. Belongs to the box rather than to " +
                 "the unit taking it, so a richer tier can simply ask for more.")]
        [Min(0)] public int PickupCost = 1;

        [Header("How Many Boxes Of Each Category")]
        [Tooltip("How many boxes of this kind hold a melee weapon.")]
        [Min(0)] public int MeleeWeaponCount;

        [Tooltip("How many boxes of this kind hold a ranged weapon.")]
        [Min(0)] public int RangedWeaponCount;

        [Tooltip("How many boxes of this kind hold an active item - a potion or the like, used once " +
                 "and gone.")]
        [Min(0)] public int ActiveItemCount;

        [Tooltip("How many boxes of this kind hold a passive item - gear worn for its traits.")]
        [Min(0)] public int PassiveItemCount;

        [Header("Where They Come From")]
        [Tooltip("How many of this kind's boxes a fallen enemy leaves behind instead of lying about " +
                 "the map from the start. Drawn at random from this kind's own boxes, so a drop is " +
                 "its authored mix rather than its first category. Never more than there are enemies " +
                 "to fall - a box nobody could leave behind is not made at all.")]
        [Min(0)] public int DroppedCount;

        /// <summary>What to call this kind, falling back to the asset name while none is authored.</summary>
        public string Title => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        /// <summary>
        /// How many boxes of this kind a match contains: one per item asked for. Derived rather than
        /// authored on its own so the total and the per-category counts can never disagree - a box
        /// exists because something was asked to be in it. Scattered boxes are still capped by how
        /// many free tiles there are, and dropped ones by how many units there are to fall.
        /// </summary>
        public int LootboxCount => MeleeWeaponCount + RangedWeaponCount + ActiveItemCount + PassiveItemCount;

        /// <summary>How many boxes of this kind hold <paramref name="kind"/>. Zero for a non-category.</summary>
        public int CountFor(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.Melee => MeleeWeaponCount,
                SlotKind.Ranged => RangedWeaponCount,
                SlotKind.Active => ActiveItemCount,
                SlotKind.Passive => PassiveItemCount,
                _ => 0
            };
        }
    }
}
