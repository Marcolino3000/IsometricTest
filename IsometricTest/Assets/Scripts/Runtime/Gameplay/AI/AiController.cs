using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using UnityEngine;

namespace Runtime.Gameplay.AI
{
    /// <summary>
    /// Drives a team automatically. At the start of its turn it activates each of its units in order;
    /// a unit attacks the closest enemy it can reach this turn, otherwise advances toward the closest
    /// visible enemy, otherwise moves to uncover as much unexplored map as possible. When every unit is
    /// spent it hands the turn back via <see cref="GameStateManager.ToggleCurrentTeam"/>.
    /// </summary>
    public class AiController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Team aiTeam = Team.Opponent;

        [Tooltip("When off, the AI never takes its team's turn and you can command its units manually. " +
                 "Can be toggled live: switching off (incl. mid-turn) hands the rest of the turn to you; " +
                 "use SetEnabled/ToggleEnabled to switch on and have the AI take over.")]
        [SerializeField] private bool aiEnabled = true;

        [Tooltip("Seconds to wait between individual unit actions so the player can follow the AI's moves.")]
        [SerializeField] private float actionDelay = 0.35f;

        [Tooltip("Safety cap on actions per unit per turn, guarding against any pathological loop.")]
        [SerializeField] private int maxActionsPerUnit = 20;

        [Header("References")]
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private FogOfWar fogOfWar;

        private bool _running;

        /// <summary>
        /// Whether the AI drives its team. When false the team's turn is left to the player.
        /// Setting this is equivalent to <see cref="SetEnabled"/>.
        /// </summary>
        public bool Enabled
        {
            get => aiEnabled;
            set => SetEnabled(value);
        }

        [ContextMenu("Toggle AI")]
        public void ToggleEnabled() => SetEnabled(!aiEnabled);

        /// <summary>
        /// Enables or disables the AI at runtime. Switching off lets a turn in progress finish in the
        /// player's hands (the coroutine stops and does not auto-advance). Switching on while it is
        /// already this team's turn makes the AI take over the remainder immediately.
        /// </summary>
        public void SetEnabled(bool value)
        {
            if (aiEnabled == value)
                return;

            aiEnabled = value;

            // Re-enabled during my own turn (player was controlling): take over what's left now.
            // When disabled, the running coroutine notices and stops itself, so nothing to do here.
            if (aiEnabled && !_running
                && gameStateManager != null && gameStateManager.State != null
                && gameStateManager.State.Team == aiTeam)
            {
                StartCoroutine(RunTurn());
            }
        }

        public void Setup(GameStateManager gameStateManagerArg, UnitSpawner unitSpawnerArg,
            TileSpawner tileSpawnerArg, FogOfWar fogOfWarArg)
        {
            gameStateManager = gameStateManagerArg;
            unitSpawner = unitSpawnerArg;
            tileSpawner = tileSpawnerArg;
            fogOfWar = fogOfWarArg;

            gameStateManager.TurnStarted += HandleTurnStarted;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted(ChangeEvent<State> changeEvent)
        {
            if (aiEnabled && !_running && changeEvent.NewValue.Team == aiTeam)
                StartCoroutine(RunTurn());
        }

        private IEnumerator RunTurn()
        {
            _running = true;
            
            // Snapshot: units can be destroyed mid-turn (combat retaliation), so iterate a copy and null-check.
            var myUnits = unitSpawner.AllUnits
                .Where(u => u != null && u.CurrentState.Team == aiTeam)
                .ToList();

            foreach (var unit in myUnits)
            {
                if (!aiEnabled)
                    break; // switched off mid-turn: hand the remaining units to the player

                if (unit == null)
                    continue;

                yield return ActUnit(unit);
            }

            _running = false;

            // Only auto-advance if the AI actually played the turn out. If it was switched off
            // mid-turn, leave the turn for the player to finish and end via the Next Turn button.
            if (aiEnabled)
                gameStateManager.ToggleCurrentTeam();
        }

        private IEnumerator ActUnit(Unit unit)
        {
            for (var i = 0; i < maxActionsPerUnit; i++)
            {
                if (!aiEnabled || unit == null || !unit.CurrentState.HasActionsLeft)
                    yield break;

                // Pause before every action — including each unit's first, and the turn's very first —
                // so the player can follow along instead of the opening move snapping in instantly.
                yield return new WaitForSeconds(actionDelay);

                if (!aiEnabled)
                    yield break; // switched off during the pause: stop before acting

                if (!TryActOnce(unit))
                    yield break; // nothing productive left for this unit this turn
            }
        }

        /// <summary>
        /// Performs at most one action for the unit. Returns false when the unit has nothing
        /// productive to do, so the caller stops re-activating it (and doesn't spin).
        /// </summary>
        private bool TryActOnce(Unit unit)
        {
            var enemy = ClosestVisibleEnemy(unit);

            return enemy != null
                ? EngageEnemy(unit, enemy)
                : Explore(unit);
        }

        // --- Combat -----------------------------------------------------------------------------

        private Unit ClosestVisibleEnemy(Unit unit)
        {
            Unit closest = null;
            var closestDistance = int.MaxValue;

            foreach (var other in unitSpawner.AllUnits)
            {
                if (other == null || other.CurrentState.Team == aiTeam)
                    continue;

                var tile = other.CurrentState.Position;
                if (tile == null || !fogOfWar.IsVisible(tile.Position))
                    continue;

                var distance = tileSpawner.GetDistanceBetweenTiles(unit.CurrentState.Position, tile);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = other;
                }
            }

            return closest;
        }

        private bool EngageEnemy(Unit unit, Unit enemy)
        {
            // Can we reach attack range and strike this turn? PlanAttackAction tests AP + range and
            // stops just inside range (GetPathWithinRange), so this is the cheapest approach-and-hit.
            if (unit.ActionExecutor.PlanAttackAction(new ExecuteArgs(null, enemy)).IsValid)
            {
                unit.ActionExecutor.ExecuteAttackAction(new ExecuteArgs(null, enemy));
                return true;
            }

            // Out of reach this turn: close the distance as much as the AP budget allows.
            return MoveToward(unit, enemy.CurrentState.Position);
        }

        // --- Exploration ------------------------------------------------------------------------

        private bool Explore(Unit unit)
        {
            var sightRange = unit.CurrentState.SightRange;

            Tile best = null;
            var bestReveal = 0;

            foreach (var tile in ReachableTiles(unit))
            {
                var reveal = CountNewlyRevealed(tile, sightRange);
                if (reveal > bestReveal)
                {
                    bestReveal = reveal;
                    best = tile;
                }
            }

            if (best != null)
            {
                unit.ActionExecutor.ExecuteMoveActions(new ExecuteArgs(best));
                return true;
            }

            // Nothing in immediate reach reveals new ground: head toward the nearest unexplored tile.
            var frontier = NearestUnexploredTile(unit);
            return frontier != null && MoveToward(unit, frontier);
        }

        private int CountNewlyRevealed(Tile from, int sightRange)
        {
            var count = 0;
            foreach (var seen in tileSpawner.GetTilesInSightRange(from.Position, sightRange))
            {
                if (!fogOfWar.IsExplored(aiTeam, seen.Position))
                    count++;
            }

            return count;
        }

        private Tile NearestUnexploredTile(Unit unit)
        {
            Tile nearest = null;
            var nearestDistance = int.MaxValue;

            foreach (var tile in tileSpawner.AllTiles)
            {
                if (!tile.IsPassable || fogOfWar.IsExplored(aiTeam, tile.Position))
                    continue;

                var distance = tileSpawner.GetDistanceBetweenTiles(unit.CurrentState.Position, tile);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = tile;
                }
            }

            return nearest;
        }

        // --- Movement helpers -------------------------------------------------------------------

        /// <summary>
        /// Moves to the reachable tile that gets strictly closest to <paramref name="target"/>.
        /// Returns false when no reachable tile improves on the current position (so we don't spin).
        /// </summary>
        private bool MoveToward(Unit unit, Tile target)
        {
            Tile best = null;
            var bestDistance = tileSpawner.GetDistanceBetweenTiles(unit.CurrentState.Position, target);

            foreach (var tile in ReachableTiles(unit))
            {
                var distance = tileSpawner.GetDistanceBetweenTiles(tile, target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tile;
                }
            }

            if (best == null)
                return false;

            unit.ActionExecutor.ExecuteMoveActions(new ExecuteArgs(best));
            return true;
        }

        private List<Tile> ReachableTiles(Unit unit)
        {
            return tileSpawner.GetMoveableTiles(
                unit.CurrentState.Position.Position,
                unit.CurrentState.ActionPoints,
                unit.CurrentState.MoveAction.Condition.Cost);
        }
    }
}
