using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.History
{
    public enum ActionKind
    {
        Move,
        Attack,
        TurnChange,
        Pickup,
        UseItem,

        /// <summary>
        /// Two items merged into one - see <see cref="Global.MergeRules"/>. Appended rather than
        /// slipped in beside the other item kinds, since the order is serialized in the icon table.
        /// </summary>
        Merge
    }

    /// <summary>
    /// Announces that something worth remembering just happened. Carries only the participants:
    /// everything the history shows about the outcome (damage dealt, kills, action points spent) is
    /// derived by diffing the snapshots taken around the action, so a new mechanic only has to say
    /// "I acted" instead of describing how to undo itself.
    /// </summary>
    public readonly struct ActionReport
    {
        public readonly ActionKind Kind;
        public readonly Unit Actor;
        public readonly Unit Target;
        public readonly Team Team;

        private ActionReport(ActionKind kind, Unit actor, Unit target, Team team)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Team = team;
        }

        public static ActionReport Move(Unit actor) =>
            new(ActionKind.Move, actor, null, actor.CurrentState.Team);

        public static ActionReport Attack(Unit actor, Unit target) =>
            new(ActionKind.Attack, actor, target, actor.CurrentState.Team);

        public static ActionReport Pickup(Unit actor) =>
            new(ActionKind.Pickup, actor, null, actor.CurrentState.Team);

        public static ActionReport UseItem(Unit actor) =>
            new(ActionKind.UseItem, actor, null, actor.CurrentState.Team);

        /// <summary>
        /// A merge, successful or not. Both outcomes are reported, because both spend the item on
        /// the right: what actually happened is the difference between the snapshots taken around
        /// this, exactly as with every other action.
        /// </summary>
        public static ActionReport Merge(Unit actor) =>
            new(ActionKind.Merge, actor, null, actor.CurrentState.Team);

        public static ActionReport TurnChange(Team team) =>
            new(ActionKind.TurnChange, null, null, team);
    }

    /// <summary>
    /// Where executed actions are announced. An announcement channel rather than an injected
    /// dependency on purpose: gameplay says what happened without knowing that anything records it,
    /// so <see cref="ActionHistory"/> stays optional and does not have to be threaded through
    /// <c>UnitSpawner</c> and <c>Unit.Init</c> into every per-unit executor. The recorder itself is
    /// wired the usual way, by <c>Setup</c> injection from the Initiator.
    /// </summary>
    public static class ActionReporter
    {
        public static event Action<ActionReport> ActionExecuted;

        public static void Report(ActionReport report)
        {
            ActionExecuted?.Invoke(report);
        }
    }
}
