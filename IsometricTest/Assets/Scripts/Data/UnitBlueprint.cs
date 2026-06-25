using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "ScriptableObjects/Data/UnitBlueprint")]
    public class UnitBlueprint : ScriptableObject
    {
        public Sprite Sprite; 
        public UnitState DefaultState => new(defaultState);

        [SerializeField] private UnitState defaultState;
    }
}