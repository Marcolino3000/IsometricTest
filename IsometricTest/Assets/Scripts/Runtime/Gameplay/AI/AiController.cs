using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.Fog;
using UnityEngine;

namespace Runtime.Gameplay.AI
{
    /// <summary>
    /// Drives a team automatically. At the start of its turn it activates each of its units in order;
    /// a unit attacks the closest enemy it can reach this turn - or, when its weapon does something
    /// beyond hitting what it is aimed at, whichever reachable enemy the swing is worth the most
    /// against - otherwise advances toward the closest visible enemy, otherwise hunts the tile an
    /// enemy was last seen on, otherwise moves to uncover as much unexplored map as possible. When
    /// every unit is spent it hands the turn back via
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
        [SerializeField] private MatchOutcomeWatcher outcomeWatcher;

        private bool _running;

        // Whether the match has been decided. A verdict stops the AI where it stands: its remaining
        // units would otherwise play on over a game that is over, and every action they took would be
        // one more the player has to step back through to take the deciding one back.
        private bool _matchOver;

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
        /// The AI was switched on or off. Moves what the fog draws without any rule changing - a turn
        /// the player has taken over is never hidden - so the fog listens rather than watching for it.
        /// </summary>
        public event Action EnabledChanged;

        /// <summary>
        /// Whether the AI - rather than the player - is the one playing this team's turn.
        /// <see cref="Fog.FogOfWar"/> asks before hiding a turn, so a turn the player took over manually
        /// (AI switched off) is never hidden from them.
        /// </summary>
        public bool Drives(Team team) => aiEnabled && team == aiTeam;

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

            EnabledChanged?.Invoke();

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
            if (aiEnabled && !_running && !_matchOver
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
            TileSpawner tileSpawnerArg, FogOfWar fogOfWarArg, MatchOutcomeWatcher matchOutcomeWatcher)
        {
            gameStateManager = gameStateManagerArg;
            unitSpawner = unitSpawnerArg;
            tileSpawner = tileSpawnerArg;
            fogOfWar = fogOfWarArg;
            outcomeWatcher = matchOutcomeWatcher;

            gameStateManager.TurnStarted += HandleTurnStarted;
            outcomeWatcher.OutcomeChanged += HandleOutcomeChanged;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnStarted -= HandleTurnStarted;

            if (outcomeWatcher != null)
                outcomeWatcher.OutcomeChanged -= HandleOutcomeChanged;
        }

        private void HandleTurnStarted(ChangeEvent<State> changeEvent)
        {
            if (aiEnabled && !_running && !_matchOver && changeEvent.NewValue.Team == aiTeam)
                StartCoroutine(RunTurn());
        }

        /// <summary>
        /// A decided match drops the turn being played out, for the same reason undo/redo does: the AI
        /// would go on acting over a game that has already been settled. Taking the verdict back only
        /// lifts the block - the turn is handed back the way a restored one is, by asking
        /// (<see cref="ResumeTurn"/>), since it was the history that cancelled that one.
        /// </summary>
        private void HandleOutcomeChanged(MatchResult result)
        {
            _matchOver = result.IsOver;

            if (_matchOver)
                CancelTurn();
        }

        private IEnumerator RunTurn()
        {
            _running = true;

            // A turn nobody watches never waits, so without this the whole turn - handover included -
            // would run inside the TurnStarted dispatch that started it: ToggleCurrentTeam would then
            // compare against a previousState the raise has not written back yet, see no team change,
            // and skip the player's own TurnReset. StartCoroutine runs the body up to the first yield
            // immediately, so this one keeps everything below out of the event.
            yield return null;

            // Snapshot: units can die mid-turn (combat retaliation), so iterate a copy and re-check.
            var myUnits = unitSpawner.AllUnits
                .Where(u => u != null && u.IsAlive && u.CurrentState.Team == aiTeam)
                .ToList();

            // Every other action is paced by the one before it; the turn's first has nothing ahead of
            // it. Give it the same beat, unless there is no unit of ours in sight to watch it move.
            if (myUnits.Any(fogOfWar.IsShown))
                yield return new WaitForSeconds(actionDelay);

            foreach (var unit in myUnits)
            {
                if (!aiEnabled || _matchOver)
                    break; // switched off mid-turn: hand the remaining units to the player

                if (unit == null || !unit.IsAlive)
                    continue;

                yield return ActUnit(unit);
            }

            _running = false;

            // Only auto-advance if the AI actually played the turn out. If it was switched off
            // mid-turn, leave the turn for the player to finish and end via the Next Turn button;
            // if the turn decided the match, there is no next turn to hand over to.
            if (aiEnabled && !_matchOver)
                gameStateManager.ToggleCurrentTeam();
        }

        private IEnumerator ActUnit(Unit unit)
        {
            for (var i = 0; i < maxActionsPerUnit; i++)
            {
                // Also the re-check after the pause below: the unit may have died to retaliation in its
                // own previous action, been taken off the board by an undo while we waited, or just
                // decided the match - the action that ends it is the last one played.
                if (!aiEnabled || _matchOver || unit == null || !unit.IsAlive || !unit.CurrentState.HasActionsLeft)
                    yield break;

                if (!TryActOnce(unit))
                    yield break; // nothing productive left for this unit this turn

                // Pace the action that just happened, if it ended where the player can see it. Taken
                // after rather than before, because only then is it known: a unit stepping out of the
                // fog is invisible right up to the moment it arrives, and the turn's closing action is
                // the one nothing follows. So the player gets a beat whenever a move lands in view -
                // including the last one before the turn is handed back - while a turn that plays out
                // entirely behind the fog (GameRules.ShowEnemyTurns off) never waits at all and
                // resolves as fast as it computes.
                if (fogOfWar.IsShown(unit))
                    yield return new WaitForSeconds(actionDelay);
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
                return EngageEnemy(unit, BestAttackTarget(unit) ?? enemy);

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

        /// <summary>
        /// Which enemy this unit's swing is worth the most against. Null when its weapon does
        /// nothing beyond the blow - every target then scores the same and the closest is as good as
        /// any, which is the choice this made before area effects existed - and null when none of
        /// them can be reached and struck this turn, so the caller falls back to closing the distance.
        /// </summary>
        private Unit BestAttackTarget(Unit unit)
        {
            if (!CombatRules.HasAreaEffects(unit))
                return null;

            Unit best = null;
            var bestScore = 0f;
            var bestDistance = int.MaxValue;

            // A step costs at least one point, so range plus the budget is as far as this unit could
            // possibly strike. Asked before planning, because the planner warns about every plan it
            // turns down and most enemies on the map are nowhere near.
            var reach = CombatRules.GetEffectiveAttackRange(unit) + unit.CurrentState.ActionPoints;

            foreach (var enemy in unitSpawner.AllUnits)
            {
                if (enemy == null || !enemy.IsAlive || enemy.CurrentState.Team == aiTeam)
                    continue;

                var tile = enemy.CurrentState.Position;
                if (tile == null || !fogOfWar.IsVisible(tile.Position))
                    continue;

                var distance = tileSpawner.GetDistanceBetweenTiles(unit.CurrentState.Position, tile);
                if (distance > reach)
                    continue;

                if (!unit.ActionExecutor.PlanAttackAction(new ExecuteArgs(null, enemy)).IsValid)
                    continue;

                var score = ScoreAttack(unit, enemy);

                // Ties go to the nearer one, so a unit with nothing to gain from either still closes
                // rather than walking past.
                if (best != null && !(score > bestScore + 0.001f
                                      || score > bestScore - 0.001f && distance < bestDistance))
                    continue;

                best = enemy;
                bestScore = score;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>
        /// What striking <paramref name="enemy"/> is worth: the blow itself, plus what the weapon's
        /// area effects would catch - the AI's own units counting against it, so it does not cleave
        /// through its own line to reach one more enemy.
        ///
        /// Read off the authored damage rather than resolved. Asking <c>CombatRules.AreaDamage</c>
        /// for the number would resolve a real strike: it rolls what a trait rolls and writes the
        /// roll to the combat log, for an attack that is only being considered.
        /// </summary>
        private float ScoreAttack(Unit unit, Unit enemy)
        {
            // From the tile it would actually swing from, not the one it stands on: an area centred
            // on the attacker moves with it, and the approach path is where the strike is planned.
            var fromTile = tileSpawner.GetAttackApproachPath(unit, enemy.CurrentState.Position).LastOrDefault();

            var score = (float)CombatRules.BaseDamageOf(unit.CurrentState.AttackAction);

            foreach (var hit in CombatRules.PlanAreaEffects(unit, fromTile, enemy, isRetaliation: false))
            {
                var own = hit.Victim.CurrentState.Team == aiTeam;

                score += own ? -hit.Effect.Damage : hit.Effect.Damage;
            }

            return score;
        }

        private bool EngageEnemy(Unit unit, Unit enemy)
        {
            // Can we reach a shot and take it this turn? PlanAttackAction tests AP and asks
            // CombatRules.CanAttackFrom where to stop, so this is the cheapest approach-and-hit -
            // and a blocked shot simply falls through to closing the distance below.
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
            Tile best = null;
            var bestReveal = 0;

            foreach (var tile in ReachableTiles(unit))
            {
                var reveal = CountNewlyRevealed(unit, tile);
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

        /// <summary>
        /// How much unseen ground <paramref name="unit"/> would uncover by standing on
        /// <paramref name="from"/> - asked of the same query the fog is drawn from, so a hill counts
        /// for both the further it sees and the further it sees over.
        /// </summary>
        private int CountNewlyRevealed(Unit unit, Tile from)
        {
            var count = 0;
            foreach (var seen in tileSpawner.GetVisibleTiles(unit, from))
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

            Tile furthest = null;
            var spent = 0;

            for (var i = 1; i < path.Count; i++)
            {
                spent += MovementRules.GetStepCost(unit.CurrentState, path[i]);
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
            return tileSpawner.GetMoveableTiles(unit.CurrentState);
        }
    }
}
