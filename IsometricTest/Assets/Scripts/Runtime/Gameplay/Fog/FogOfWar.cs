using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Fog
{
    public class InClassName
    {
        public InClassName(HashSet<Vector2Int> visible)
        {
            Visible = visible;
        }

        public HashSet<Vector2Int> Visible { get; private set; }
    }

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

        public void Setup(TileSpawner tileSpawner, UnitSpawner unitSpawner, GameStateManager gameStateManager)
        {
            _tileSpawner = tileSpawner;
            _unitSpawner = unitSpawner;

            ResetExploration();

            gameStateManager.OnGameStateChanged += HandleStateChange;
        }

        // private void OnDestroy()
        // {
        //     if (_gameStateManager != null)
        //         _gameStateManager.OnGameStateChanged -= HandleStateChange;
        // }

        public void ResetExploration()
        {
            _exploredTiles[Team.Player] = new HashSet<Vector2Int>();
            _exploredTiles[Team.Opponent] = new HashSet<Vector2Int>();
        }

        // public void RecomputeForActiveTeam()
        // {
        //     if (_gameStateManager == null || _gameStateManager.State == null)
        //         return;
        //
        //     Recompute(_gameStateManager.State.Team);
        // }

        private void HandleStateChange(ChangeEvent<State> changeEvent)
        {
            _activeTeam = changeEvent.NewValue.Team;
            Recompute();
        }

        public void Recompute()
        {
            var visible = CollectVisiblePositions();

            if (!_exploredTiles.TryGetValue(_activeTeam, out var explored))
                explored = _exploredTiles[_activeTeam] = new HashSet<Vector2Int>();
            explored.UnionWith(visible);

            ApplyTileVisibility(visible, explored);
            ApplyUnitVisibility(new InClassName(visible));
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

        private void ApplyUnitVisibility(InClassName inClassName)
        {
            foreach (var unit in _unitSpawner.AllUnits)
            {
                if (unit == null)
                    continue;

                var tile = unit.CurrentState.Position;
                var onVisibleTile = tile != null && inClassName.Visible.Contains(tile.Position);

                // Your own units are always shown; enemies only when standing on a visible tile.
                unit.SetRevealed(unit.CurrentState.Team == _activeTeam || onVisibleTile);
            }
        }
    }
}
