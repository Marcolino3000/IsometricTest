using Actions;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// What using an active item takes: nothing beyond the action points every action costs. A
    /// self-targeted item has no target and no range, so there is nothing else to test yet.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Active Item Condition")]
    public class ActiveItemCondition : ActionCondition
    {
    }
}
