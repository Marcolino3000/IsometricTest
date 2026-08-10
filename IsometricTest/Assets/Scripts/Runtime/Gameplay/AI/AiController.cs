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
    /// visible enemy, otherwise hunts the tile an enemy was last seen on, otherwise moves to uncover as
    /// much unexplored map as possible. When every unit is spent it hands the turn back via
    /// <see cref="GameStateManager.ToggleCurrentTeam"/>.
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

        [Tooltip("Last resort when every enemy is out of sight, none was seen recently and the whole map " +
                 "is explored: units advance on the closest enemy's real position instead of standing " +
                 "still. Off is strictly fog-honest - units then idle until an enemy shows up again.")]
        [SerializeField] private bool advanceOnLostEnemies = true;

        [Header("References")]
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private FogOfWar fogOfWar;

        private bool _running;

        // Where each enemy was last seen. Sight is the only thing that ever put an enemy on the AI's
        // radar, so without this a unit loses its target the moment the player steps out of its sight
        // radius. Pure AI scratch state: not part of a history snapshot, and losing it on undo only
        // costs the AI a turn of searching.
        private readonly Dictionary<Unit, Tile> _lastKnownEnemyTiles = new();

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
            // When disabled, the running coroutine notices and stops itself, so this does nothing.
            ResumeTurn();
        }

        /// <summary>
        /// Takes over the rest of the current turn if it is the AI's and nothing is playing it out.
        /// Used when the AI is switched on mid-turn, and to hand a turn back to it after undo/redo
        /// cancelled it.
        /// </summary>
        public void ResumeTurn()
        {
            if (aiEnabled && !_running
                && gameStateManager != null && gameStateManager.State != null
                && gameStateManager.State.Team == aiTeam)
            {
                StartCoroutine(RunTurn());
            }
        }

        /// <summary>
        /// Drops a turn that is being played out. Undo/redo replaces the world under the AI's feet,
        /// so the units it still had queued - and its end-of-turn handover - refer to a state that no
        /// longer exists.
        /// </summary>
        public void CancelTurn()
        {
            if (!_running)
                return;

            StopAllCoroutines();
            _running = false;
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
            
            // Snapshot: units can die mid-turn (combat retaliation), so iterate a copy and re-check.
            var myUnits = unitSpawner.AllUnits
                .Where(u => u != null && u.IsAlive && u.CurrentState.Team == aiTeam)
                .ToList();

            foreach (var unit in myUnits)
            {
                if (!aiEnabled)
                    break; // switched off mid-turn: hand the remaining units to the player

                if (unit == null || !unit.IsAlive)
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
                if (!aiEnabled || unit == null || !unit.IsAlive || !unit.CurrentState.HasActionsLeft)
                    yield break;

                // Pause before every action — including each unit's first, and the turn's very first —
                // so the player can follow along instead of the opening move snapping in instantly.
                yield return new WaitForSeconds(actionDelay);

                // Re-check the unit too: it may have died to retaliation in its own previous action,
                // or been taken off the board by an undo during this pause.
                if (!aiEnabled || unit == null || !unit.IsAlive)
                    yield break; // switched off or unit gone: stop before acting

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
            RefreshEnemyMemory();

            var enemy = ClosestEnemy(unit, visibleOnly: true);
            if (enemy != null)
                return EngageEnemy(unit, enemy);

            // Lost contact: go to where the enemy was, rather than forgetting it exists.
            var lastKnown = ClosestRememberedEnemyTile(unit);
            if (lastKnown != null && MoveToward(unit, lastKnown))
                return true;

            if (Explore(unit))
                return true;

            // Nothing seen, nothing remembered, nothing left to uncover. The explored map never
            // shrinks, so from here the unit would stand still for the rest of the match.
            return advanceOnLostEnemies && MoveTowardEnemy(unit, ClosestEnemy(unit, visibleOnly: false));
        }

        // --- Combat -----------------------------------------------------------------------------

        /// <summary>
        /// The nearest enemy still in play, either the nearest one the team can currently see
        /// (<paramref name="visibleOnly"/>) or the nearest one outright.
        /// </summary>
        private Unit ClosestEnemy(Unit unit, bool visibleOnly)
        {
            Unit closest = null;
            var closestDistance = int.MaxValue;

            foreach (var other in unitSpawner.AllUnits)
            {
                if (other == null || !other.IsAlive || other.CurrentState.Team == aiTeam)
                    continue;

                var tile = other.CurrentState.Position;
                if (tile == null || visibleOnly && !fogOfWar.IsVisible(tile.Position))
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
            return MoveTowardEnemy(unit, enemy);
        }

        private bool MoveTowardEnemy(Unit unit, Unit enemy)
        {
            var tile = enemy != null ? enemy.CurrentState.Position : null;
            return tile != null && MoveToward(unit, tile);
        }

        // --- Memory of lost enemies ---------------------------------------------------------------

        /// <summary>
        /// Brings the memory of where enemies are in line with what the team can see right now: a
        /// visible enemy is (re)recorded, a remembered tile that is in sight without its enemy on it is
        /// forgotten (we looked, it moved on), and enemies that left play stop being hunted.
        /// </summary>
        private void RefreshEnemyMemory()
        {
            var inPlay = new HashSet<Unit>();

            foreach (var enemy in unitSpawner.AllUnits)
            {
                if (enemy == null || !enemy.IsAlive || enemy.CurrentState.Team == aiTeam)
                    continue;

                inPlay.Add(enemy);

                var tile = enemy.CurrentState.Position;
                if (tile != null && fogOfWar.IsVisible(tile.Position))
                    _lastKnownEnemyTiles[enemy] = tile;
                else if (_lastKnownEnemyTiles.TryGetValue(enemy, out var remembered)
                         && (remembered == null || fogOfWar.IsVisible(remembered.Position)))
                    _lastKnownEnemyTiles.Remove(enemy);
            }

            // AllUnits drops removed units, so anything left over here has been killed or undone away.
            foreach (var gone in _lastKnownEnemyTiles.Keys.Where(u => !inPlay.Contains(u)).ToList())
                _lastKnownEnemyTiles.Remove(gone);
        }

        private Tile ClosestRememberedEnemyTile(Unit unit)
        {
            Tile closest = null;
            var closestDistance = int.MaxValue;

            foreach (var tile in _lastKnownEnemyTiles.Values)
            {
                if (tile == null)
                    continue;

                var distance = tileSpawner.GetDistanceBetweenTiles(unit.CurrentState.Position, tile);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = tile;
                }
            }

            return closest;
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
        /// Walks as far along the path to <paramref name="target"/> as the AP budget allows.
        /// Returns false when nothing gets the unit closer (so we don't spin).
        /// </summary>
        private bool MoveToward(Unit unit, Tile target)
        {
            var step = FurthestStepAlongPath(unit, target) ?? ClosestReachableTile(unit, target);

            if (step == null)
                return false;

            unit.ActionExecutor.ExecuteMoveActions(new ExecuteArgs(step));
            return true;
        }

        /// <summary>
        /// The furthest tile on the path to <paramref name="target"/> the unit can still pay for.
        /// Follows the pathfinder rather than picking the reachable tile with the smallest distance:
        /// a detour around impassable terrain first walks *away* from the target, which a
        /// closest-tile choice reads as "no improvement" and stalls on.
        /// </summary>
        private Tile FurthestStepAlongPath(Unit unit, Tile target)
        {
            // Enemies and last-known tiles are occupied targets; path onto them and stop just short.
            var path = tileSpawner.GetPath(unit.CurrentState.Position, target, ignoreGoalOccupied: true);

            var budget = unit.CurrentState.ActionPoints;
            var moveCost = unit.CurrentState.MoveAction.Condition.Cost;

            Tile furthest = null;
            var spent = 0;

            for (var i = 1; i < path.Count; i++)
            {
                spent += moveCost + path[i].ExtraMoveCost;
                if (spent > budget)
                    break;

                if (!path[i].IsOccupied)
                    furthest = path[i];
            }

            return furthest;
        }

        /// <summary>
        /// Fallback for when there is no path at all (target walled off, or the only corridor to it
        /// blocked): the reachable tile that gets strictly closest to <paramref name="target"/>.
        /// </summary>
        private Tile ClosestReachableTile(Unit unit, Tile target)
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

            return best;
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
