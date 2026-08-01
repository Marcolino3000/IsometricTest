using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.History
{
    public enum ActionKind
    {
        Move,
        Attack,
        TurnChange
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
