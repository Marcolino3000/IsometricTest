using Actions;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "Actions/MoveCondition")]
    public class MoveCondition : ActionCondition
    {
        public int Cost;
    }
}