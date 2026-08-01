using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Fog
{
    /// <summary>
    /// Owns per-team visibility. On every turn change (and whenever a unit moves) it recomputes
    /// which tiles the active team can see — the union of its units' sight ranges — and pushes that
    /// down to the tiles (lit / remembered / hidden) and to enemy units (shown / hidden).
    /// Radius-only: terrain does not block line of sight.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        [Header("Fog tints (multiplied with each tile's lit terrain colour)")]
        [SerializeField] private Color exploredTint = new(0.45f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color hiddenTint = new(0.12f, 0.12f, 0.16f, 1f);

        private TileSpawner _tileSpawner;
        private UnitSpawner _unitSpawner;
        private Team _activeTeam;
        private readonly Dictionary<Team, HashSet<Vector2Int>> _exploredTiles = new();
        private HashSet<Vector2Int> _visiblePositions = new();

        // Copy of the explored map handed to history snapshots, reused until the map actually changes:
        // explored ground grows on few actions, and copying the whole map per recorded action adds up.
        private readonly Dictionary<Team, HashSet<Vector2Int>> _exploredForSnapshots = new();
        private bool _exploredChanged = true;

        public void Setup(TileSpawner tileSpawner, UnitSpawner unitSpawner, GameStateManager gameStateManager)
        {
            _tileSpawner = tileSpawner;
            _unitSpawner = unitSpawner;

            ResetExploration();

            gameStateManager.TurnReset += HandleTurnReset;
        }

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
            var visible = CollectVisiblePositions();
            _visiblePositions = visible;

            if (!_exploredTiles.TryGetValue(_activeTeam, out var explored))
                explored = _exploredTiles[_activeTeam] = new HashSet<Vector2Int>();

            var exploredCount = explored.Count;
            explored.UnionWith(visible);

            if (explored.Count != exploredCount)
                _exploredChanged = true;

            ApplyTileVisibility(visible, explored);
            ApplyUnitVisibility(visible);
        }

        public bool IsExplored(Team team, Vector2Int position)
        {
            return _exploredTiles.TryGetValue(team, out var explored) && explored.Contains(position);
        }
        
        public bool IsVisible(Vector2Int position)
        {
            return _visiblePositions.Contains(position);
        }

        private HashSet<Vector2Int> CollectVisiblePositions()
        {
            var visible = new HashSet<Vector2Int>();

            foreach (var unit in _unitSpawner.AllUnits)
            {
                if (unit == null || unit.CurrentState.Team != _activeTeam)
                    continue;

                var tile = unit.CurrentState.Position;
                if (tile == null)
                    continue;

                foreach (var seen in _tileSpawner.GetTilesInSightRange(tile.Position, unit.CurrentState.SightRange))
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

                tile.SetVisibility(visibility, exploredTint, hiddenTint);
            }
        }

        private void ApplyUnitVisibility(HashSet<Vector2Int> visible)
        {
            foreach (var unit in _unitSpawner.AllUnits)
            {
                if (unit == null)
                    continue;

                var tile = unit.CurrentState.Position;
                var onVisibleTile = tile != null && visible.Contains(tile.Position);

                // Your own units are always shown; enemies only when standing on a visible tile.
                unit.SetRevealed(unit.CurrentState.Team == _activeTeam || onVisibleTile);
            }
        }
    }
}
