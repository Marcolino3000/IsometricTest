using System;
using System.Collections.Generic;
using Runtime;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/UnitSpawnerSettings")]
    public class UnitSpawnerSettings : ScriptableObject
    {
        [Header("Unit Settings")]
        public List<UnitAmount> UnitAmounts;
        
        [Header("Visual Settings")]
        public int OrderInLayer;
    }

    [Serializable]
    public class UnitAmount
    {
        public int Amount;
        public Unit Prefab;
    }
}