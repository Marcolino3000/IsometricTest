using Actions;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "Actions/Move")]
    public class MoveActionData : ActionData<MoveCondition, MoveEffect>
    {
        public override UnitAction<MoveCondition, MoveEffect> CreateAction(ActionContext context)
        {
            return new MoveAction(Condition, Effect, context);
        }

    }
}