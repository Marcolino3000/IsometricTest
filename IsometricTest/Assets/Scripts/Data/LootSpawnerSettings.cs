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
        [Tooltip("Sorting order of the box sprite. Above the tiles' markers, so a box is not buried " +
                 "under the movement highlights it lies among, and below the units, so nobody is " +
                 "hidden behind a box. Ground 0, tile marker 1, box 2, unit 3.")]
        public int OrderInLayer = 2;

        [Tooltip("How large a box is drawn, in world units of the shared prefab's scale. Replaces " +
                 "whatever the prefab carries, exactly as the kind's sprite does, so the size is " +
                 "authored here rather than in a prefab nobody opens - and every kind stays the " +
                 "same size, so the tier is still read off the silhouette the artist drew.")]
        [Min(0.01f)] public float Scale = 1.25f;

        /// <summary>
        /// How many boxes the scattered kinds together ask for, before the free tiles run out. Says
        /// nothing about the dropped ones: there is one of those per unit, whatever their kinds ask
        /// for.
        /// </summary>
        public int LootboxCount
        {
            get
            {
                var count = 0;

                foreach (var type in Types)
                    if (type != null && type.Source == LootboxSource.ScatteredOnMap)
                        count += type.LootboxCount;

                return count;
            }
        }
    }
}
