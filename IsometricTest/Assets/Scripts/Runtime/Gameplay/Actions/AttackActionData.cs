using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/Attack")]
    public class AttackActionData : ActionData<AttackCondition, AttackEffect>
    {
        public string DisplayName;
        
        [TextArea] public string Description;
        
        public Sprite Symbol;
        
        public string Tooltip => string.IsNullOrWhiteSpace(Description) ? name : $"{name}\n{Description}";

        public override UnitAction<AttackCondition, AttackEffect> CreateAction(ActionContext context)
        {
            return new AttackAction(Condition, Effect, context);
        }
    }
}
