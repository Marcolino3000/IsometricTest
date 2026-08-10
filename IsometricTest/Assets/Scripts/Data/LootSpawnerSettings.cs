using System.Collections.Generic;
using Runtime.Gameplay.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/LootSpawnerSettings")]
    public class LootSpawnerSettings : ScriptableObject
    {
        [Header("Lootbox Settings")]
        public Lootbox LootboxPrefab;

        [Tooltip("Action points taking a box costs. Belongs to the loot rather than to a unit, so it " +
                 "lives here instead of on a blueprint.")]
        public int PickupCost = 1;

        [Header("Contents")]
        [Tooltip("Items the boxes hand out - weapons, active items, passive items alike. Dealt " +
                 "without repeating until every one of a category has been placed once.")]
        [FormerlySerializedAs("Weapons")]
        public List<Item> Items = new();

        [Header("How Many Boxes Of Each Category")]
        [Tooltip("How many boxes hold a melee weapon.")]
        [Min(0)] public int MeleeWeaponCount = 3;

        [Tooltip("How many boxes hold a ranged weapon.")]
        [Min(0)] public int RangedWeaponCount = 3;

        [Tooltip("How many boxes hold an active item - a potion or the like, used once and gone.")]
        [Min(0)] public int ActiveItemCount = 2;

        [Tooltip("How many boxes hold a passive item - gear worn for its traits.")]
        [Min(0)] public int PassiveItemCount = 2;

        /// <summary>
        /// How many boxes are scattered: one per item asked for. Derived rather than authored on its
        /// own so the total and the per-category counts can never disagree - a box exists because
        /// something was asked to be in it. Still capped by how many walkable tiles are free.
        /// </summary>
        public int LootboxCount => MeleeWeaponCount + RangedWeaponCount + ActiveItemCount + PassiveItemCount;

        /// <summary>How many boxes of <paramref name="kind"/> to place. Zero for a non-category.</summary>
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

        [Header("Visual Settings")]
        [Tooltip("Sorting order of the box sprite. Below the units so nobody is hidden behind a box.")]
        public int OrderInLayer = 1;
    }
}
