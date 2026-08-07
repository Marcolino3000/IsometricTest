using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/Attack")]
    public class AttackActionData : ActionData<AttackCondition, AttackEffect>
    {
        [Tooltip("Which item slot the weapon is offered in. Authored rather than read off the range: " +
                 "traits change the effective range, the category must not change with them.")]
        public WeaponKind Kind;

        public string DisplayName;
        
        [TextArea] public string Description;
        
        public Sprite Symbol;
        
        public string Tooltip => string.IsNullOrWhiteSpace(Description) ? name : $"{name}\n{Description}";

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
