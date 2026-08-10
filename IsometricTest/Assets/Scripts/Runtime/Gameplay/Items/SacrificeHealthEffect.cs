using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Costs the user health, so an item can charge something other than action points. Never
    /// reduces below one: dying to an item would have to go through <see cref="Unit.Remove"/> like a
    /// killing blow does, and nothing in the item path is set up to remove a unit.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Effects/Sacrifice Health")]
    public class SacrificeHealthEffect : ActiveItemEffect
    {
        [Tooltip("Health paid. The user is always left with at least one.")]
        [Min(1)] public int Amount = 4;

        public override void Apply(Unit user)
        {
            user.CurrentState.Health = Mathf.Max(1, user.CurrentState.Health - Amount);
        }
    }
}
