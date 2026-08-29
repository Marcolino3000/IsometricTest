using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "ScriptableObjects/Data/UnitBlueprint")]
    public class UnitBlueprint : ScriptableObject
    {
        [Tooltip("What the unit is drawn as before anything animates it - and what it keeps if no " +
                 "animation set is given.")]
        public Sprite Sprite;

        [Tooltip("Which frames the unit stands, walks and strikes with. Left empty, the unit is the " +
                 "still sprite above and steps onto its tiles the moment the rules say it does.")]
        public UnitAnimationSet Animations;

        public UnitState DefaultState => new(defaultState);

        [SerializeField] private UnitState defaultState;
    }
}
