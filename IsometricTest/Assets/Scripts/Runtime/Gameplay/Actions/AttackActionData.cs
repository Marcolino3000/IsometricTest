using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/Attack")]
    public class AttackActionData : ActionData<AttackCondition, AttackEffect>
    {
        public override UnitAction<AttackCondition, AttackEffect> CreateAction(ActionContext context)
        {
            return new AttackAction(Condition, Effect, context);
        }
    }
}