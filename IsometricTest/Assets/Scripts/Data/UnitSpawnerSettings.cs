using System;
using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/UnitSpawnerSettings")]
    public class UnitSpawnerSettings : ScriptableObject
    {
        [Header("Unit Settings")]
        [Tooltip("The one prefab every unit is drawn with. What tells one unit from another is its " +
                 "blueprint, not a prefab of its own - the same shape the lootboxes use, where one " +
                 "prefab is dressed by the type asset.")]
        public Unit UnitPrefab;

        [Tooltip("The character the player commands.")]
        public UnitBlueprint PlayerUnit;

        [Tooltip("Every opponent this match fields: which kind, how many, and which ring of the map " +
                 "they stand in. One roster rather than one per ring, so how many of a kind a match " +
                 "holds is read in one place - the rings themselves only say where their edges lie.")]
        public List<UnitAmount> OpponentUnits;

        [Header("Visual Settings")]
        [Tooltip("Sorting order of the unit sprite. Top of the board's ladder - ground 0, tile " +
                 "marker 1, lootbox 2, unit 3 - so nothing lying on a tile hides whoever stands " +
                 "on it. Keep it above LootSpawnerSettings.OrderInLayer.")]
        public int OrderInLayer;
        public Color OpponentColor;
    }

    /// <summary>
    /// How many of one kind of unit a match fields, and where. <see cref="Zone"/> is what ties the
    /// roster to the map's rings: a ring is a distance and says nothing about who guards it, so who
    /// guards it is authored here beside how many of them there are.
    /// </summary>
    [Serializable]
    public class UnitAmount
    {
        public int Amount;

        [FormerlySerializedAs("Prefab")]
        public UnitBlueprint Blueprint;

        [Tooltip("Which ring of the map these stand in, counted from the middle out: 0 is the one " +
                 "the player spawns in. Below zero they belong to no ring and take the spawn band " +
                 "along the rim, which is what a map with no rings authored uses.")]
        public int Zone = -1;

        /// <summary>Whether these belong to a ring of the map at all.</summary>
        public bool HasZone => Zone >= 0;
    }
}