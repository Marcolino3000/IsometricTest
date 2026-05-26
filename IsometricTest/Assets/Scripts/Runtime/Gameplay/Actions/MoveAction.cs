using Runtime.Gameplay.Actions;

namespace Actions
{
    public interface IUnitAction
    {
        bool TestConditions();
        void ExecuteEffects();
    }

    public class MoveAction : UnitAction<MoveCondition, MoveEffect>
    {
        public MoveAction(MoveCondition condition, MoveEffect effect, ActionContext context) : base(condition, effect, context) { }

        public override bool TestConditions()
        {
            if(Condition.Cost > Context.ActionPoints) 
                return false;
            
            return true;
        }

        public override void ExecuteEffects()
        {
            // Context.TargetUnit.CurrentState.Position = Context.TargetTile;
            Context.TargetUnit.TryMoveToTile(Context.TargetTile);
        }
    }
}