using System;
using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.AI;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Fog
{
    /// <summary>
    /// Owns per-team visibility. On every turn change (and whenever a unit moves) it recomputes
    /// which tiles the active team can see — the union of its units' sight — and pushes that
    /// down to the tiles (lit / remembered / hidden) and to enemy units (shown / hidden).
    /// How far each unit sees and what the ground hides from it is <see cref="Global.SightRules"/>'
    /// business, asked through <c>TileSpawner.GetVisibleTiles</c>; the fog only collects the answers.
    ///
    /// Two teams are in play here and the split is load-bearing: the <b>active</b> team is the one whose
    /// sight grows the explored map and answers <see cref="IsVisible"/> for the AI, while the
    /// <see cref="ViewingTeam"/> is merely the one the screen is drawn for. They are the same team unless
    /// <see cref="GameRules.ShowEnemyTurns"/> hides an AI turn — the AI keeps seeing with its own eyes
    /// either way, so hiding its turn never blinds it.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        [Header("Fog tints (multiplied with each tile's lit terrain colour)")]
        [SerializeField] private Color exploredTint = new(0.45f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color hiddenTint = new(0.12f, 0.12f, 0.16f, 1f);

        private TileSpawner _tileSpawner;
        private UnitSpawner _unitSpawner;
        private AiController _aiController;
        private GameRules _rules;
        private Team _activeTeam;
        private Team _shownTeam;

        // Whether the tiles were last drawn with unscouted terrain disguised, so a switch flipped
        // during play is noticed - the same reason _shownTeam is kept.
        private bool _terrainHidden;
        private readonly Dictionary<Team, HashSet<Vector2Int>> _exploredTiles = new();
        private HashSet<Vector2Int> _visiblePositions = new();
        private HashSet<Vector2Int> _shownPositions = new();

        // Copy of the explored map handed to history snapshots, reused until the map actually changes:
        // explored ground grows on few actions, and copying the whole map per recorded action adds up.
        private readonly Dictionary<Team, HashSet<Vector2Int>> _exploredForSnapshots = new();
        private bool _exploredChanged = true;

        public void Setup(TileSpawner tileSpawner, UnitSpawner unitSpawner, GameStateManager gameStateManager,
            AiController aiController, GameRules gameRules)
        {
            _tileSpawner = tileSpawner;
            _unitSpawner = unitSpawner;
            _aiController = aiController;
            _rules = gameRules;

            ResetExploration();

            gameStateManager.TurnReset += HandleTurnReset;

            // Both switches the fog is drawn from are held live and may be flipped mid-play. They say
            // so now rather than being watched for drift: the rules asset announces an edit, and the
            // AI announces being switched off - which moves ViewingTeam without any rule changing.
            if (_rules != null)
                _rules.Changed += HandleSettingsChanged;

            if (_aiController != null)
                _aiController.EnabledChanged += HandleSettingsChanged;
        }

        private void OnDestroy()
        {
            if (_rules != null)
                _rules.Changed -= HandleSettingsChanged;

            if (_aiController != null)
                _aiController.EnabledChanged -= HandleSettingsChanged;
        }

        /// <summary>
        /// A switch the fog draws from was flipped. Recomputed only when it actually changes what is
        /// drawn - an edit to an unrelated rule on the same asset announces itself just the same.
        /// </summary>
        private void HandleSettingsChanged()
        {
            if (_tileSpawner != null && (ViewingTeam != _shownTeam || HideUnexploredTerrain != _terrainHidden))
                Recompute();
        }

        /// <summary>
        /// The team the map is drawn for. The team at the table, except while an AI plays a turn the
        /// player is not meant to watch — then the view stays with the player and the AI's units only
        /// surface where the player's own units can see them.
        /// </summary>
        public Team ViewingTeam =>
            ShowEnemyTurns || _aiController == null || !_aiController.Drives(_activeTeam)
                ? _activeTeam
                : Team.Player;

        // Missing asset falls back to the built-in default (shown), matching CombatRules' behaviour.
        private bool ShowEnemyTurns => _rules == null || _rules.ShowEnemyTurns;

        /// <summary>
        /// Whether ground nobody has scouted is drawn as plain terrain instead of its own. Without a
        /// rules asset the whole board's terrain stays readable, which is what the fog did before
        /// there was a switch.
        /// </summary>
        private bool HideUnexploredTerrain => _rules != null && _rules.HideUnexploredTerrain;

        public void ResetExploration()
        {
            _exploredTiles[Team.Player] = new HashSet<Vector2Int>();
            _exploredTiles[Team.Opponent] = new HashSet<Vector2Int>();
            _exploredChanged = true;
        }

        /// <summary>
        /// The explored map per team, for a history snapshot. The returned sets are never mutated
        /// afterwards, so callers may hold on to them; they must not write to them either.
        /// </summary>
        public IReadOnlyDictionary<Team, HashSet<Vector2Int>> CaptureExplored()
        {
            if (_exploredChanged)
            {
                _exploredForSnapshots.Clear();
                foreach (var pair in _exploredTiles)
                    _exploredForSnapshots[pair.Key] = new HashSet<Vector2Int>(pair.Value);

                _exploredChanged = false;
            }

            return _exploredForSnapshots;
        }

        /// <summary>
        /// Restores a previously captured explored map (undo/redo). Copies the sets, since play
        /// continues to grow them and the snapshot they came from must stay untouched.
        /// </summary>
        public void RestoreExplored(IReadOnlyDictionary<Team, HashSet<Vector2Int>> explored)
        {
            ResetExploration();

            foreach (var pair in explored)
                _exploredTiles[pair.Key] = new HashSet<Vector2Int>(pair.Value);
        }

        private void HandleTurnReset(ChangeEvent<State> changeEvent)
        {
            _activeTeam = changeEvent.NewValue.Team;
            Recompute();
        }

        public void Recompute()
        {
            // What the acting team sees. This is what grows the explored map and what the AI's own
            // decisions read, so it always follows the turn — never the view.
            var visible = CollectVisiblePositions(_activeTeam);
            _visiblePositions = visible;

            var explored = ExploredFor(_activeTeam);
            var exploredCount = explored.Count;
            explored.UnionWith(visible);

            if (explored.Count != exploredCount)
                _exploredChanged = true;

            // What is drawn. Same sight in the usual case; a second pass only while a hidden AI turn
            // keeps the screen on the player's units. Assigned before it is applied, so IsShown always
            // answers with what the tiles and units were last given.
            _shownTeam = ViewingTeam;
            _terrainHidden = HideUnexploredTerrain;
            _shownPositions = _shownTeam == _activeTeam ? visible : CollectVisiblePositions(_shownTeam);

            ApplyTileVisibility(_shownPositions, ExploredFor(_shownTeam));
            ApplyUnitVisibility();

            Recomputed?.Invoke();
        }

        /// <summary>
        /// The fog has been redrawn. For whatever is drawn on the board without belonging to a tile
        /// or a unit - the zone borders today - since those two are pushed at directly and anything
        /// else would otherwise have to watch for drift in <c>Update</c>.
        /// </summary>
        public event Action Recomputed;

        /// <summary>
        /// Whether the unit is on screen right now: it belongs to the viewing team, or it stands where
        /// that team can see. The AI asks before pacing an action — there is nobody to pace for while
        /// the acting unit is behind the fog.
        /// </summary>
        public bool IsShown(Unit unit)
        {
            if (unit == null || !unit.IsAlive)
                return false;

            if (unit.CurrentState.Team == _shownTeam)
                return true;

            var tile = unit.CurrentState.Position;
            return tile != null && _shownPositions.Contains(tile.Position);
        }

        public bool IsExplored(Team team, Vector2Int position)
        {
            return _exploredTiles.TryGetValue(team, out var explored) && explored.Contains(position);
        }

        /// <summary>
        /// Whether the team currently taking its turn can see the position — what the AI reasons with,
        /// not what the screen shows. The two differ while an AI turn is hidden.
        /// </summary>
        public bool IsVisible(Vector2Int position)
        {
            return _visiblePositions.Contains(position);
        }

        private HashSet<Vector2Int> ExploredFor(Team team)
        {
            if (!_exploredTiles.TryGetValue(team, out var explored))
                explored = _exploredTiles[team] = new HashSet<Vector2Int>();

            return explored;
        }

        private HashSet<Vector2Int> CollectVisiblePositions(Team team)
        {
            var visible = new HashSet<Vector2Int>();

            foreach (var unit in _unitSpawner.AllUnits)
            {
                if (unit == null || unit.CurrentState.Team != team)
                    continue;

                var tile = unit.CurrentState.Position;
                if (tile == null)
                    continue;

                foreach (var seen in _tileSpawner.GetVisibleTiles(unit, tile))
                    visible.Add(seen.Position);
            }

            return visible;
        }

        private void ApplyTileVisibility(HashSet<Vector2Int> visible, HashSet<Vector2Int> explored)
        {
            foreach (var tile in _tileSpawner.AllTiles)
            {
                var visibility =
                    visible.Contains(tile.Position) ? TileVisibility.Visible :
                    explored.Contains(tile.Position) ? TileVisibility.Explored :
                    TileVisibility.Hidden;

                tile.SetVisibility(visibility, exploredTint, hiddenTint, _terrainHidden);
            }
        }

        /// <summary>
        /// Pushes <see cref="IsShown"/> onto every unit: the viewing team's own are always revealed,
        /// enemies only while standing on a tile it can see.
        /// </summary>
        private void ApplyUnitVisibility()
        {
            foreach (var unit in _unitSpawner.AllUnits)
            {
                if (unit != null)
                    unit.SetRevealed(IsShown(unit));
            }
        }
    }
}
