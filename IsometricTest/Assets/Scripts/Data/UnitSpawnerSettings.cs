using System;
using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/UnitSpawnerSettings")]
    public class UnitSpawnerSettings : ScriptableObject
    {
        [Header("Unit Settings")]
        public Unit PlayerUnit;
        public List<UnitAmount> OpponentUnits;

        [Header("Visual Settings")]
        [Tooltip("Sorting order of the unit sprite. Top of the board's ladder - ground 0, tile " +
                 "marker 1, lootbox 2, unit 3 - so nothing lying on a tile hides whoever stands " +
                 "on it. Keep it above LootSpawnerSettings.OrderInLayer.")]
        public int OrderInLayer;
        public Color OpponentColor;
    }

    [Serializable]
    public class UnitAmount
    {
        public int Amount;
        public Unit Prefab;
    }
}