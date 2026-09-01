using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// Bundles the state of all units into the questions other systems ask of a whole team - today
    /// only "has anyone anything left to do", which the next-turn button reads.
    ///
    /// It is a <b>query over the live units plus an invalidation signal, never a mirror holding
    /// copies</b>. That is the whole point of it: the answer used to be a bool cached on
    /// <see cref="State"/>, which put it in <c>GameSnapshot</c> as well and had it written twice
    /// during a single restore - a cached copy of something derivable from the units, and a cached
    /// copy can drift. Derived, it stays out of the snapshot and comes back for free when the board
    /// does, the way <c>VictoryRules</c> asks the board and the weapon in hand is re-derived.
    ///
    /// Deriving it also answers a question the cache got wrong: a unit that <i>falls</i> is off the
    /// board, so the team may have nothing left the moment it dies - which the old push, hung on
    /// action points reaching zero, never noticed.
    /// </summary>
    public class UnitStateManager : MonoBehaviour
    {
        /// <summary>
        /// The aggregate may have moved: a unit spent points, fell, or was put back. Carries no
        /// value - a subscriber goes and asks whichever of the queries below it cares about, which
        /// is what keeps this from growing an event per question.
        /// </summary>
        public event Action Changed;

        private readonly List<Unit> tracked = new();

        private GameStateManager gameStateManager;

        public void Setup(GameStateManager gameStateManagerArg)
        {
            gameStateManager = gameStateManagerArg;
            gameStateManager.TurnReset += HandleTurnReset;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnReset -= HandleTurnReset;

            foreach (var unit in tracked)
                Unsubscribe(unit);

            tracked.Clear();
        }

        /// <summary>
        /// Starts watching a unit. Called by <see cref="UnitSpawner"/> for every unit it puts on the
        /// board, including one undo puts back, since that is where a unit's lifetime is known.
        /// </summary>
        public void Track(Unit unit)
        {
            if (unit == null || tracked.Contains(unit))
                return;

            tracked.Add(unit);
            unit.CurrentState.ActionPointsChanged += HandleUnitChanged;

            Changed?.Invoke();
        }

        /// <summary>Stops watching a unit - it has left the board, or the board has been cleared.</summary>
        public void Untrack(Unit unit)
        {
            if (unit == null || !tracked.Remove(unit))
                return;

            Unsubscribe(unit);

            Changed?.Invoke();
        }

        /// <summary>
        /// Whether any unit of a team can still act. Asked of the units as they stand: a unit that
        /// is not alive is not on the board and is not counted.
        /// </summary>
        public bool AnyHaveActionsLeft(Team team)
        {
            return tracked.Any(unit => unit != null
                                       && unit.IsAlive
                                       && unit.CurrentState.Team == team
                                       && unit.CurrentState.HasActionsLeft);
        }

        /// <summary>Whether the team whose turn it is can still act. What the next-turn button reads.</summary>
        public bool ActiveTeamHasActionsLeft =>
            gameStateManager != null && AnyHaveActionsLeft(gameStateManager.State.Team);

        private void Unsubscribe(Unit unit)
        {
            if (unit != null && unit.CurrentState != null)
                unit.CurrentState.ActionPointsChanged -= HandleUnitChanged;
        }

        // The turn changing does not move any unit's points, but it changes which team is being
        // asked about - so the answer to ActiveTeamHasActionsLeft moves without a unit doing anything.
        private void HandleTurnReset(ChangeEvent<State> changeEvent) => Changed?.Invoke();

        private void HandleUnitChanged(ChangeEvent<int> changeEvent) => Changed?.Invoke();
    }
}
