using Runtime;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/UnitSpawnerSettings")]
    public class UnitSpawnerSettings : ScriptableObject
    {
        [Header("Unit Settings")]
        public GameObject Prefab;
        public UnitBlueprint Blueprint;
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;
        public Vector3 Scale;
        public int Amount;
        
        [Header("Visual Settings")]
        public int OrderInLayer;
    }
}