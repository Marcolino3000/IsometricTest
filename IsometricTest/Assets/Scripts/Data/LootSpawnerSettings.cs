using System.Collections.Generic;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// What the loot spawner needs that belongs to no single kind of box: the prefab they are all
    /// built on, which kinds this match uses, and how they sort against the rest of the board.
    ///
    /// Everything a box actually *is* - its look, its contents, its cost, how many of it there are -
    /// lives on the <see cref="LootboxType"/> assets listed here, so a further kind is a further
    /// asset rather than a further field.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Settings/LootSpawnerSettings")]
    public class LootSpawnerSettings : ScriptableObject
    {
        [Header("Lootbox Settings")]
        [Tooltip("The object every box is built on. Its sprite is replaced by the one its kind " +
                 "carries, so all kinds share this one prefab.")]
        public Lootbox LootboxPrefab;

        [Tooltip("The kinds of box this match uses. A kind listed twice is spawned twice, and one " +
                 "left out of the list simply does not appear.")]
        public List<LootboxType> Types = new();

        [Header("Visual Settings")]
        [Tooltip("Sorting order of the box sprite. Below the units so nobody is hidden behind a box.")]
        public int OrderInLayer = 1;

        /// <summary>How many boxes all kinds together ask for, before either cap is applied.</summary>
        public int LootboxCount
        {
            get
            {
                var count = 0;

                foreach (var type in Types)
                    if (type != null)
                        count += type.LootboxCount;

                return count;
            }
        }
    }
}
