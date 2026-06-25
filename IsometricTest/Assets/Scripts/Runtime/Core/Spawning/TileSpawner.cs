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
        private Pathfinder _pathfinder;

        public IReadOnlyList<Tile> AllTiles => Tiles;
        private Dictionary<Vector2Int, TerrainProfile> _terrainMap = new();

        #region Services
        public int GetDistanceBetweenTiles(Tile tileA, Tile tileB)
        {
            return Mathf.Abs(tileA.Position.x - tileB.Position.x) + Mathf.Abs(tileA.Position.y - tileB.Position.y);
        }

        public List<Tile> GetPath(Tile start, Tile goal, bool ignoreOccupied = false, bool ignoreGoalOccupied = false, bool excludeGoal = false)
        {
            return _pathfinder.FindPath(start, goal, ignoreOccupied, ignoreGoalOccupied, excludeGoal);
        }

        public List<Tile> GetPathWithinRange(Tile start, Tile target, int range, bool ignoreOccupied = false)
        {
            return _pathfinder.FindPathWithinRange(start, target, range, ignoreOccupied);
        }
      
        public Tile GetTileAtPosition(Vector2Int position)
        {
            return Tiles.Find(t => t.Position == position);
        }

        /// <summary>
        /// All existing tiles within a circular (Euclidean) radius of <paramref name="center"/>,
        /// including the centre tile. Used for fog-of-war sight; ignores terrain and occupancy.
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
        /// Returns every tile the unit at <paramref name="startPosition"/> can reach within the given
        /// <paramref name="actionPoints"/> budget, using the pathfinder so impassable terrain, occupied
        /// tiles and difficult-terrain costs are all respected (not just straight-line distance).
        /// Per-step cost is the unit's base <paramref name="moveCost"/> plus the destination tile's
        /// extra terrain cost.
        /// </summary>
        public List<Tile> GetMoveableTiles(Vector2Int startPosition, int actionPoints, int moveCost)
        {
            var moveableTiles = new List<Tile>();

            var start = GetTileAtPosition(startPosition);
            if (start == null)
                return moveableTiles;

            foreach (var tile in Tiles)
            {
                if (tile == start)
                    continue;

                // Each step costs at least moveCost, so a tile whose minimum step count already blows
                // the budget can never be reached - skip it before paying for a pathfinding search.
                if (GetDistanceBetweenTiles(start, tile) * moveCost > actionPoints)
                    continue;

                var path = _pathfinder.FindPath(start, tile);
                if (path.Count == 0)
                    continue;

                if (GetPathCost(path, moveCost) <= actionPoints)
                    moveableTiles.Add(tile);
            }

            return moveableTiles;
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

        public Vector2Int GetRandomSpawnZonePosition(Team team)
        {
            var positions = settings.GetSpawnZonePositions(team);

            if (positions == null || positions.Count == 0)
            {
                Debug.LogWarning("No spawn positions defined for team: " + team);
                return Vector2Int.zero;
            }

            var index = Random.Range(0, positions.Count);
            return positions[index];
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
        private static int GetPathCost(List<Tile> path, int moveCost)
        {
            var cost = 0;
            for (var i = 1; i < path.Count; i++)
                cost += moveCost + path[i].ExtraMoveCost;
            return cost;
        }

        private void ClearGrid()
        {
            foreach (var tile in Tiles)
            {
                if (tile != null)
                    Destroy(tile.gameObject);
            }

            Tiles.Clear();
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