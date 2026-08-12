using System.Collections.Generic;
using Data;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Global;
using TMPro;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    public class TileSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TileSpawnerSettings settings;
        [SerializeField] private Selector selector;
        
        private readonly List<Tile> Tiles = new();

        // The same tiles keyed by grid position: sight walks a line tile by tile, and a search
        // through the whole board per step of every line adds up over a fog pass.
        private readonly Dictionary<Vector2Int, Tile> _tilesByPosition = new();

        private Pathfinder _pathfinder;

        public IReadOnlyList<Tile> AllTiles => Tiles;
        private Dictionary<Vector2Int, TerrainProfile> _terrainMap = new();

        #region Services
        public int GetDistanceBetweenTiles(Tile tileA, Tile tileB)
        {
            return tileA.DistanceTo(tileB);
        }

        public List<Tile> GetPath(Tile start, Tile goal, bool ignoreOccupied = false, bool ignoreGoalOccupied = false, bool excludeGoal = false)
        {
            return _pathfinder.FindPath(start, goal, ignoreOccupied, ignoreGoalOccupied, excludeGoal);
        }

        /// <summary>
        /// The path a unit would walk until it can strike <paramref name="targetTile"/> - within its
        /// effective attack range (terrain bonuses included, so a unit on a hill stops further out)
        /// and with a clear line to it. Starts at the unit's own tile and contains just that tile when
        /// it can already attack from where it stands. Shared by the attack planner and the attack
        /// preview so they always agree.
        /// </summary>
        public List<Tile> GetAttackApproachPath(Unit attacker, Tile targetTile)
        {
            return _pathfinder.FindAttackApproachPath(attacker, targetTile);
        }
      
        public Tile GetTileAtPosition(Vector2Int position)
        {
            return _tilesByPosition.TryGetValue(position, out var tile) ? tile : null;
        }

        /// <summary>
        /// All existing tiles within a circular (Euclidean) radius of <paramref name="center"/>,
        /// including the centre tile. The bare circle - terrain, occupancy and line of sight are
        /// <see cref="GetVisibleTiles"/>'s business.
        /// </summary>
        public IEnumerable<Tile> GetTilesInSightRange(Vector2Int center, int range)
        {
            for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx * dx + dy * dy > range * range)
                    continue;

                var tile = GetTileAtPosition(center + new Vector2Int(dx, dy));
                if (tile != null)
                    yield return tile;
            }
        }

        /// <summary>
        /// What <paramref name="viewer"/> actually sees from <paramref name="fromTile"/>: the circle
        /// its effective sight reaches, minus whatever higher ground hides. Both halves come from
        /// <see cref="SightRules"/> - the fog and the AI ask this one question, so ground that widens
        /// the fog widens what the AI expects to uncover by exactly as much.
        /// </summary>
        public IEnumerable<Tile> GetVisibleTiles(Unit viewer, Tile fromTile)
        {
            if (viewer == null || fromTile == null)
                yield break;

            foreach (var tile in GetTilesInSightRange(fromTile.Position, SightRules.GetSightRange(viewer, fromTile)))
            {
                if (SightRules.HasClearLine(fromTile, tile))
                    yield return tile;
            }
        }

        /// <summary>
        /// Returns every tile the unit at <paramref name="startPosition"/> can reach within the given
        /// <paramref name="actionPoints"/> budget, using the pathfinder so impassable terrain, occupied
        /// tiles and difficult-terrain costs are all respected (not just straight-line distance).
        /// Costs come from <see cref="MovementRules"/>, so a trait that discounts terrain widens the
        /// highlight by exactly as much as it widens what the unit can actually pay for.
        /// </summary>
        public List<Tile> GetMoveableTiles(UnitState mover)
        {
            return GetMoveableTiles(mover, mover != null ? mover.ActionPoints : 0);
        }

        /// <summary>
        /// The same, on a budget of somebody else's choosing. What an enemy could reach is asked of
        /// the points it *starts* a turn with rather than the ones it has left over from its last
        /// one, which are nearly always none while the player is the one moving.
        /// </summary>
        public List<Tile> GetMoveableTiles(UnitState mover, int actionPoints)
        {
            var moveableTiles = new List<Tile>();

            var start = mover?.Position;
            if (start == null)
                return moveableTiles;

            // No step can cost less than this, so a tile whose minimum step count already blows the
            // budget can never be reached - skip it before paying for a pathfinding search.
            var cheapestStep = MovementRules.GetStepCost(mover, start);

            foreach (var tile in Tiles)
            {
                if (tile == start)
                    continue;

                if (GetDistanceBetweenTiles(start, tile) * cheapestStep > actionPoints)
                    continue;

                var path = _pathfinder.FindPath(start, tile, mover: mover);
                if (path.Count == 0)
                    continue;

                if (MovementRules.GetPathCost(mover, path) <= actionPoints)
                    moveableTiles.Add(tile);
            }

            return moveableTiles;
        }

        /// <summary>
        /// Every tile <paramref name="attacker"/> could strike within one turn: it walks somewhere it
        /// can afford to walk, and hits whatever its effective range reaches from there. The reach
        /// comes from the same <see cref="MovementRules"/> the move itself is charged against and the
        /// range from the same <see cref="CombatRules"/> the strike is resolved with, so the tint can
        /// never promise safety the rules would not honour - a unit on a hill threatens as far as it
        /// would actually shoot from it.
        ///
        /// <paramref name="actionPoints"/> is the budget to spend walking; the tile it stands on is
        /// counted too, since it may simply attack from where it is.
        /// </summary>
        public IEnumerable<Tile> GetThreatenedTiles(Unit attacker, int actionPoints)
        {
            if (attacker == null)
                return new List<Tile>();

            return GetThreatenedTiles(attacker, GetMoveableTiles(attacker.CurrentState, actionPoints));
        }

        /// <summary>
        /// The same for a caller that has already worked out where the unit could walk - the overlay
        /// draws that set as well, and a second sweep of the board would cost a pathfinding search
        /// per tile to arrive at the list it is already holding. The tile it stands on need not be in
        /// <paramref name="standingOn"/>; it is counted either way.
        /// </summary>
        public IEnumerable<Tile> GetThreatenedTiles(Unit attacker, IEnumerable<Tile> standingOn)
        {
            var start = attacker != null ? attacker.CurrentState.Position : null;

            if (start == null)
                return new List<Tile>();

            // Collected as tiles rather than positions: the line of fire has to be walked over real
            // ground anyway, and the same tile threatened from two directions costs one hash.
            var threatened = new HashSet<Tile>();

            foreach (var tile in Prepend(start, standingOn))
            {
                var range = CombatRules.GetEffectiveAttackRange(attacker, tile);

                // Manhattan, like every other grid distance here - Tile.DistanceTo is what the attack
                // condition tests against, and a diamond is what the player will actually be hit in.
                for (int dx = -range; dx <= range; dx++)
                for (int dy = -range + Mathf.Abs(dx); dy <= range - Mathf.Abs(dx); dy++)
                {
                    var reached = GetTileAtPosition(tile.Position + new Vector2Int(dx, dy));

                    // Range is already settled by the diamond; what is left is whether the shot has
                    // anywhere to travel, so the tint never promises a hit through a mountain.
                    if (reached != null && !threatened.Contains(reached) && SightRules.HasClearLine(tile, reached))
                        threatened.Add(reached);
                }
            }

            return threatened;
        }

        private static IEnumerable<Tile> Prepend(Tile first, IEnumerable<Tile> rest)
        {
            yield return first;

            if (rest == null)
                yield break;

            foreach (var tile in rest)
                yield return tile;
        }

        public void HighlightTile(Vector2Int tilePosition, MarkerColor markerColor = MarkerColor.White)
        {
            var tile = Tiles.Find(t => t.GetComponent<Tile>().Position == tilePosition);
            if (tile == null)
            {
                Debug.LogWarning("Tile not found at position: " + tilePosition);
                return;
            }

            tile.GetComponentInChildren<TileMarker>().SetMarkerColor(markerColor);
        }

        /// <summary>
        /// The same for a tile already in hand - the overlays paint whole sets of them, and looking
        /// each one back up by position is a search through the board per tile.
        /// </summary>
        public void HighlightTile(Tile tile, MarkerColor markerColor)
        {
            if (tile == null)
                return;

            tile.GetComponentInChildren<TileMarker>().SetMarkerColor(markerColor);
        }

        public void ResetHighlightedTiles()
        {
            foreach (var tile in Tiles)
            {
                tile.GetComponentInChildren<TileMarker>().SetMarkerColor(MarkerColor.None);
            }
        }

        public void ResetOccupiedTiles()
        {
            foreach (var tile in Tiles)
            {
                tile.SetUnit(null);
            }
        }

        /// <summary>
        /// Candidate spawn positions for <paramref name="team"/>, best first - the player around the map's
        /// centre, opponents along its rim. See <see cref="TileSpawnerSettings.GetSpawnZonePositions"/>; the
        /// caller walks the list until a tile takes the unit, so terrain and occupancy are settled there
        /// rather than here.
        /// </summary>
        public List<Vector2Int> GetSpawnZonePositions(Team team)
        {
            return settings.GetSpawnZonePositions(team);
        }

        public Vector3 GridIndexToWorldPosition(Vector2Int gridPosition)
        {
            return new Vector3(
                settings.StartPosition.x + gridPosition.x * settings.HalfTileOffsetX + gridPosition.y * settings.HalfTileOffsetX,
                settings.StartPosition.y + gridPosition.x * -settings.HalfTileOffsetY + gridPosition.y * settings.HalfTileOffsetY,
                settings.StartPosition.z
            );
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Total movement cost of walking <paramref name="path"/> (excluding the start tile): the unit's
        /// base <paramref name="moveCost"/> per step plus each destination tile's extra terrain cost.
        /// </summary>
        private void ClearGrid()
        {
            foreach (var tile in Tiles)
            {
                if (tile != null)
                    Destroy(tile.gameObject);
            }

            Tiles.Clear();
            _tilesByPosition.Clear();
        }

        private void SpawnTile(int xIndex, int yIndex)
        {
            var position = GridIndexToWorldPosition(xIndex, yIndex);
            
            var instance = Instantiate(settings.TilePrefab, position, Quaternion.identity,transform);
            instance.name = $"Tile {xIndex}-{yIndex}";
            instance.GetComponentInChildren<TextMeshPro>().text = xIndex + "-" + yIndex;
            
            var tile = instance.GetComponent<Tile>();
            tile.Position = new Vector2Int(xIndex, yIndex);
            tile.ApplyTerrain(GetTerrainProfile(tile.Position));
            Tiles.Add(tile);
            _tilesByPosition[tile.Position] = tile;

            selector.RegisterClickable(tile.GetComponent<Clickable>());
        }

        private TerrainProfile GetTerrainProfile(Vector2Int position)
        {
            return _terrainMap != null && _terrainMap.TryGetValue(position, out var profile)
                ? profile
                : settings.FlatTerrain;
        }

        private Vector3 GridIndexToWorldPosition(int xIndex, int yIndex)
        {
            return new Vector3(
                settings.StartPosition.x + xIndex * settings.HalfTileOffsetX + yIndex * settings.HalfTileOffsetX,
                settings.StartPosition.y + xIndex * -settings.HalfTileOffsetY + yIndex * settings.HalfTileOffsetY,
                settings.StartPosition.z
            );
        }

        #endregion

        #region Setup

        public void Setup(Selector selectorArg)
        {
            selector = selectorArg;
            _pathfinder = new Pathfinder(this);
        }

        [ContextMenu("Spawn Grid")]
        public void SpawnTiles()
        {
            ClearGrid();

            _terrainMap = settings.BuildTerrainMap();

            for (int x = 0; x < settings.GridSizeX; x++)
            for (int y = 0; y < settings.GridSizeY; y++)
                SpawnTile(x, y);
        }

        #endregion
    }
}