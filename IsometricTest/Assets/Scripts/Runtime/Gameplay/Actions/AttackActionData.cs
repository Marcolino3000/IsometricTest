using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/Attack")]
    public class AttackActionData : ActionData<AttackCondition, AttackEffect>
    {
        [Tooltip("Which item slot the weapon is offered in. Authored rather than read off the range: " +
                 "traits change the effective range, the category must not change with them.")]
        public WeaponKind Kind;

        // Weapons are the only items whose category is authored as a kind of its own, so that a
        // weapon asset can only ever be tagged melee or ranged - never active or passive.
        public override SlotKind Slot => Kind == WeaponKind.Melee ? SlotKind.Melee : SlotKind.Ranged;

        public override UnitAction<AttackCondition, AttackEffect> CreateAction(ActionContext context)
        {
            return new AttackAction(Condition, Effect, context);
        }
    }

    /// <summary>The category a weapon belongs to, which is the item slot it is carried in.</summary>
    public enum WeaponKind
    {
        Melee,
        Ranged
    }
}
