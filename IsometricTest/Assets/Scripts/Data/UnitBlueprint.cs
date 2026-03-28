using Runtime.Actions;
using UnityEngine;

namespace Runtime
{
    [CreateAssetMenu(menuName = "ScriptableObjects/Data/UnitBlueprint")]
    public class UnitBlueprint : ScriptableObject
    {
        public Sprite Sprite; 
        public UnitState DefaultState => new(defaultState);

        [SerializeField] private UnitState defaultState;
        public Move MoveAction;
        public Attack AttackAction;
        public int Speed;
        public int Attack;
        public int Defense;
        public int MovementInitiative;
        public int CombatInitiative;
    }

    public enum CombatType
    {
        Melee,
        Ranged
    }
}