using System;
using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/UnitSpawnerSettings")]
    public class UnitSpawnerSettings : ScriptableObject
    {
        [Header("Unit Settings")]
        // public GameObject Prefab;
        public UnitBlueprint Blueprint;
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;
        public Vector3 Scale;
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