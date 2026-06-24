using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/AttackCondition")]
    public class AttackCondition : ActionCondition
    {
        public int Range;
    }
}