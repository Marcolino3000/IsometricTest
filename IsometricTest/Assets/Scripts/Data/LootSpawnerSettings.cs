using System;
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

        [Tooltip("Every box this match holds: which kind, how many of it, and which ring of the map " +
                 "they lie in. A kind may be listed more than once - the same tier in two rings is " +
                 "two entries with a count each - and one left out of the list does not appear at " +
                 "all. How many of a kind there are is authored here rather than on the kind, since " +
                 "it is a question about this match rather than about what a chest is.")]
        public List<LootboxAmount> Boxes = new();

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

                foreach (var entry in Boxes)
                    if (entry?.Type != null && entry.Type.Source == LootboxSource.ScatteredOnMap)
                        count += entry.Count;

                return count;
            }
        }
    }

    /// <summary>
    /// How many boxes of one kind a match holds, and where they lie. <see cref="Zone"/> is what ties
    /// the loot to the map's rings - a ring is a distance and says nothing about what is found at it,
    /// so what is found there is authored here beside how many of them there are.
    ///
    /// A dropped kind (<see cref="LootboxSource.DroppedByUnits"/>) ignores both: every unit leaves
    /// one box, so how many there are is how many units there are to fall, and one lands wherever
    /// its unit fell rather than in a ring.
    /// </summary>
    [Serializable]
    public class LootboxAmount
    {
        public LootboxType Type;

        [Tooltip("How many boxes of this kind lie in this ring. Still capped by how many free tiles " +
                 "the ring has to lie on - the rest spill over its border rather than being lost.")]
        [Min(0)] public int Count = 1;

        [Tooltip("Which ring of the map they lie in, counted from the middle out: 0 is the one the " +
                 "player spawns in. Below zero they belong to no ring and are scattered over the " +
                 "whole map, which is what a map with no rings authored does.")]
        public int Zone = -1;

        /// <summary>Whether these belong to a ring of the map at all.</summary>
        public bool HasZone => Zone >= 0;
    }
}
