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