using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;

namespace Actions
{
    public interface IUnitAction
    {
        int Cost { get; }

        /// <summary>
        /// What the action is, in the same vocabulary the history reports in. Asked by anything that
        /// has to show a plan before it runs, so a planned step can be drawn without the drawer
        /// knowing what kinds of action exist.
        /// </summary>
        ActionKind Kind { get; }

        bool TestConditions();
        void ExecuteEffects();
    }

    public class MoveAction : UnitAction<MoveCondition>
    {
        public MoveAction(MoveCondition condition, ActionContext context) : base(condition, context) { }

        public override ActionKind Kind => ActionKind.Move;

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

            // The same question the route was found by, so the path and the bill can never disagree
            // about where a confined unit may set foot - see MovementRules.CanEnter.
            if (!MovementRules.CanEnter(
                    Context.TargetUnit != null ? Context.TargetUnit.CurrentState : null,
                    Context.TargetTile))
                return false;

            return true;
        }

        public override void ExecuteEffects()
        {
            Context.TargetUnit.TryMoveToTile(Context.TargetTile);
        }
    }
}