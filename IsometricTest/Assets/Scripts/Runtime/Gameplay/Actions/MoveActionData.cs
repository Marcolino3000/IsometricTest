using Actions;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "Actions/Move")]
    public class MoveActionData : ActionData<MoveCondition>
    {
        public override IUnitAction CreateAction(ActionContext context)
        {
            return new MoveAction(Condition, context);
        }

    }
}