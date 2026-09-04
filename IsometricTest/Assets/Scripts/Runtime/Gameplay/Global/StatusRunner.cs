using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// The one thing that gives traits a turn. Every other trait hook shapes a number somebody else
    /// is folding - what a strike deals, how far an eye reaches, what a step costs - and is asked at
    /// the moment that number is wanted. <see cref="Trait.OnTurnBegan"/> is asked by nobody, because
    /// nothing else in the game is a moment: this is that moment, and it is the only caller.
    ///
    /// <b>It ticks on <see cref="GameStateManager.TurnStarted"/>, and both halves of that matter.</b>
    /// After <see cref="GameStateManager.TurnReset"/>, so the action points a status drains are the
    /// ones the turn just handed out rather than what was left of the last one. And never on a
    /// snapshot restore, since a restored turn is deliberately given no <c>TurnStarted</c> - its
    /// actor has already played it, and its statuses have already ticked. That is the whole of undo
    /// here: no reason flag, no guard, nothing to take back.
    ///
    /// Created by the Initiator <i>before</i> <c>ActionHistory</c> subscribes, so what a tick takes
    /// is inside the turn change's own snapshot rather than charged to whatever is done next.
    ///
    /// It holds nothing: which units there are is the spawner's, what they carry is theirs. Like
    /// <c>UnitStateManager</c>, it is a pass over the live units and not a mirror of them.
    /// </summary>
    public class StatusRunner : MonoBehaviour
    {
        private GameStateManager gameStateManager;
        private UnitSpawner unitSpawner;

        // Reused across turns rather than allocated per tick: a turn's fallen are spent immediately.
        private readonly List<Unit> fallen = new();
        private readonly List<Trait> ticking = new();

        // The board as the turn found it. Copied for the same reason the traits below are: a fall
        // moves a unit between the two lists the spawner concatenates, so a status that finished
        // somebody would otherwise change the list being walked.
        private readonly List<Unit> acting = new();

        public void Setup(GameStateManager gameStateManagerArg, UnitSpawner unitSpawnerArg)
        {
            gameStateManager = gameStateManagerArg;
            unitSpawner = unitSpawnerArg;

            gameStateManager.TurnStarted += HandleTurnStarted;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted(ChangeEvent<State> changeEvent)
        {
            var team = changeEvent.NewValue.Team;

            fallen.Clear();
            acting.Clear();
            acting.AddRange(unitSpawner.AllSpawnedUnits);

            foreach (var unit in acting)
            {
                if (unit == null || !unit.IsAlive || unit.CurrentState.Team != team)
                    continue;

                // A unit standing on no tile has not arrived yet - its ring is still unwalked. It is
                // hidden like a fallen one, and standing nowhere is what tells the two apart.
                if (unit.CurrentState.Position == null)
                    continue;

                Tick(unit);
            }

            // Collected rather than removed on the spot, the way CombatRunner collects them: a unit
            // can be finished by one status while another is still to be asked, and a fall announced
            // mid-pass would drop its spoils under a board still being ticked.
            foreach (var unit in fallen)
                unit.Remove();

            fallen.Clear();
            acting.Clear();
        }

        private void Tick(Unit unit)
        {
            var tile = unit.CurrentState.Position;

            // Copied before anything is asked: a trait may put a status on the very unit it is
            // ticking on - a wound that festers - and the fold reads the list it would be added to.
            ticking.Clear();
            ticking.AddRange(CombatRules.TraitsAffecting(unit.CurrentState, tile));

            var context = new TurnContext(unit, tile);

            foreach (var trait in ticking)
            {
                // Whatever ran before it may have finished the unit; nothing goes on happening to it.
                if (unit.CurrentState.Health <= 0)
                    break;

                trait.OnTurnBegan(context);
            }

            ticking.Clear();

            // After the hooks, so a status authored to last one turn is felt on the turn it was put
            // on rather than wearing off unspent.
            unit.CurrentState.AgeStatuses();

            if (unit.CurrentState.Health <= 0 && !fallen.Contains(unit))
                fallen.Add(unit);
        }
    }
}
