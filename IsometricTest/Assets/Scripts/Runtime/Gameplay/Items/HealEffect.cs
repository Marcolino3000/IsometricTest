using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Restores health, never past the health the unit's blueprint starts it with - there is no
    /// separate maximum, so <see cref="Unit.MaxHealth"/> is that starting value.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Effects/Heal")]
    public class HealEffect : ActiveItemEffect
    {
        [Tooltip("Health restored. Capped at the unit's starting health.")]
        [Min(1)] public int Amount = 5;

        public override void Apply(Unit user)
        {
            user.CurrentState.Health = Mathf.Min(user.CurrentState.Health + Amount, user.MaxHealth);
        }
    }
}
