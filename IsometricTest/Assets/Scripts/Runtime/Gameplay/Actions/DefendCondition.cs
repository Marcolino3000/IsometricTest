using UnityEngine;

namespace Actions
{
    [CreateAssetMenu (menuName = "Actions/DefendCondition")]
    public class DefendCondition : ActionCondition
    {
        public int DefenseAmount = 5;
    }
}