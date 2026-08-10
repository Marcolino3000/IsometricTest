using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Global;

namespace Actions
{
    public interface IUnitAction
    {
        int Cost { get; }
        bool TestConditions();
        void ExecuteEffects();
    }

    public class MoveAction : UnitAction<MoveCondition, MoveEffect>
    {
        public MoveAction(MoveCondition condition, MoveEffect effect, ActionContext context) : base(condition, effect, context) { }

        // Difficult terrain and any trait that discounts it are folded in by MovementRules, so what
        // is charged here is the same number the pathfinder routed by and the highlight promised.
        // TargetUnit is the mover on a move action - it is what ExecuteEffects walks.
        public override int Cost => MovementRules.GetStepCost(
            Context.TargetUnit != null ? Context.TargetUnit.CurrentState : null,
            Context.TargetTile,
            Condition.Cost);

        public override bool TestConditions()
        {
            if(Cost > Context.ActionPoints)
                return false;

            return true;
        }

        public override void ExecuteEffects()
        {
            Context.TargetUnit.TryMoveToTile(Context.TargetTile);
        }
    }
}