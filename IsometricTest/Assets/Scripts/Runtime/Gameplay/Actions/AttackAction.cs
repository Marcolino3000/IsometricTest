using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;

namespace Actions
{
    public class AttackAction : UnitAction<AttackCondition>
    {
        // No effect: what a strike does is asked of the weapon the unit currently has drawn, in
        // CombatRules, rather than carried on the planned action - so a weapon swapped between
        // planning and striking changes the blow.
        public AttackAction(AttackCondition condition, ActionContext context) : base(condition, context) { }

        public override ActionKind Kind => ActionKind.Attack;

        public override bool TestConditions()
        {
            if(Condition.Cost > Context.ActionPoints)
                return false;

            // Asked of the tile the strike is made from - the end of the approach path for a planned
            // attack, where the unit stands for an immediate one - since both the effective range
            // (terrain bonuses) and the line of fire are properties of where it shoots from.
            return CombatRules.CanAttackFrom(Context.Unit, Context.FromTile ?? Context.Unit.CurrentState.Position,
                Context.TargetTile);
        }

        public override void ExecuteEffects()
        {
            CombatRunner.ResolveCombat(Context.Unit, Context.TargetUnit);
        }
    }
}