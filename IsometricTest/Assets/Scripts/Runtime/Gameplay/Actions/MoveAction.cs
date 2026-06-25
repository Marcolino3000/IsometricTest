using Runtime.Gameplay.Actions;

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

        // Stepping onto difficult terrain (e.g. hills) costs extra action points on top of the base move cost.
        public override int Cost => Condition.Cost + (Context.TargetTile != null ? Context.TargetTile.ExtraMoveCost : 0);

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