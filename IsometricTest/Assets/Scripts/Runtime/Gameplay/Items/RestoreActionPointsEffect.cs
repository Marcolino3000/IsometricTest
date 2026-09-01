using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Hands the character extra action points for the current turn. Uncapped on purpose: the turn's
    /// refresh is what the blueprint sets, and an item that pushes past it is the point of the item.
    /// </summary>
    [System.Serializable]
    public class RestoreActionPointsEffect : ActiveItemEffect
    {
        [Tooltip("Action points granted. Spent from the item's own cost first, so a cost of 1 and an " +
                 "amount of 3 nets the character 2.")]
        [Min(1)] public int Amount = 3;

        public override string Summary => $"Grants {Amount} action points";

        public override void Apply(Unit target)
        {
            target.CurrentState.ActionPoints += Amount;
        }
    }
}
