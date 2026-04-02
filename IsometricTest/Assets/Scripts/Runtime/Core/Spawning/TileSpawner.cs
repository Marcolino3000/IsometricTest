using System.Collections.Generic;
using Data;
using Runtime.Controls;
using Runtime.Core.State;
using Runtime.Entities;
using Runtime.Feedback;
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
        
        private static readonly List<Tile> Tiles = new();

        public bool GetTilesWithinReach(Vector2Int startPosition, int range, out List<Tile> reachableTiles)
        {
            var reachablePositions = new List<Vector2Int>();
            
            CheckMoveDirections(startPosition, range, reachablePositions);

            reachableTiles = GetTilesFromPositions(reachablePositions); 
            
            // FilterForOccupiedTiles(reachableTiles);
            
            return reachableTiles.Count > 0;
        }

        public void FilterForOccupiedTiles(List<Tile> reachableTiles)
        {
            if (reachableTiles == null || reachableTiles.Count == 0)
                return;

            reachableTiles.RemoveAll(t => t.IsOccupied);
        }
        
        public Tile GetTileAtPosition(Vector2Int position)
        {
            return Tiles.Find(t => t.Position == position);
        }

        private List<Tile> GetTilesFromPositions(List<Vector2Int> reachablePositions)
        {
            var result = new List<Tile>();

            if (reachablePositions == null || reachablePositions.Count == 0)
                return result;

            foreach (var pos in reachablePositions)
            {
                var tile = Tiles.Find(t => t.Position == pos);
                if (tile != null)
                {
                    result.Add(tile);
                }
            }

            return result;
        }

        private void CheckMoveDirections(Vector2Int startPosition, int range, List<Vector2Int> tiles)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    // skip own tile
                    if (dx == 0 && dy == 0)
                        continue;

                    // only keep tiles inside the diamond (Manhattan distance)
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > range)
                        continue;

                    var offset = new Vector2Int(dx, dy);

                    // filter to "forward and sides" half-space:
                    // dot(offset, forward) >= 0 => not behind the unit
                    if(settings.AllowMovementInAllDirections)
                    {
                        float dot = Vector2.Dot(offset, Direction.Forward);
                        if (dot < 0f)
                            continue;
                    }

                    var position = startPosition + offset;

                    if (!CheckForGridBoundaries(position.x, position.y))
                        continue;

                    tiles.Add(position);
                }
            }
        }

        public void HighlightMoveableTiles(Vector2Int startPosition, int range)
        {
            GetTilesWithinReach(startPosition, range, out var tiles);
            FilterForOccupiedTiles(tiles);
            
            foreach (var tile in tiles)
            {
                HighlightTile(tile.Position);
            }
        }

        private bool CheckForGridBoundaries(int x, int y)
        {
            return x >= 0 && x < settings.GridSizeX && y >= 0 && y < settings.GridSizeY;
        }

        public void HighlightTile(Vector2Int tilePosition, MarkerColor markerColor = MarkerColor.White)
        {
            if (!CheckForGridBoundaries(tilePosition.x, tilePosition.y))
            {
                UnityEngine.Debug.LogWarning("Tile Position was out of bounds");
                return;
            }

            var tile = Tiles.Find(t => t.GetComponent<Tile>().Position == tilePosition);
            if (tile == null)
            {
                UnityEngine.Debug.LogWarning("Tile not found at position: " + tilePosition);
                return;
            }
            
            tile.GetComponentInChildren<TileMarker>().SetMarkerColor(markerColor);
        }
  
        public static void ResetHighlightedTiles()
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
        
        private void Start()
        {
            SpawnGrid();
        }

        [ContextMenu("Spawn Grid")]
        private void SpawnGrid()
        {
            ClearGrid();
            
            for (int x = 0; x < settings.GridSizeX; x++)
                for (int y = 0; y < settings.GridSizeY; y++)
                    SpawnTile(x, y);
        }

        private void ClearGrid()
        {
            foreach (var tile in Tiles)
            {
                Destroy(tile);
            }
        }

        private void SpawnTile(int xIndex, int yIndex)
        {
            var position = GridIndexToWorldPosition(xIndex, yIndex);
            
            var instance = Instantiate(settings.TilePrefab, position, Quaternion.identity,transform);
            instance.name = $"Tile {xIndex}-{yIndex}";
            instance.GetComponentInChildren<TextMeshPro>().text = xIndex + "-" + yIndex;
            
            var tile = instance.GetComponent<Tile>();
            tile.Position = new Vector2Int(xIndex, yIndex);
            Tiles.Add(tile);

            ClickableRegistry.RegisterClickable(tile.GetComponent<Clickable>());
            // selector.RegisterClickable(tile.GetComponent<Clickable>());
        }

        public Vector2Int GetRandomGridPosition()
        {
            return GetRandomGridPosition(settings.GridSizeX, settings.GridSizeY);
        }
        
        public Vector2Int GetRandomSpawnZonePosition(Team team)
        {
            var positions = settings.GetSpawnZonePositions(team);

            if (positions == null || positions.Count == 0)
            {
                UnityEngine.Debug.LogWarning("No spawn positions defined for team: " + team);
                return Vector2Int.zero;
            }

            var index = Random.Range(0, positions.Count);
            return positions[index];
        }

        public Vector3 GridIndexToWorldPosition(int xIndex, int yIndex)
        {
            return new Vector3(
                settings.StartPosition.x + xIndex * settings.HalfTileOffsetX + yIndex * settings.HalfTileOffsetX,
                settings.StartPosition.y + xIndex * -settings.HalfTileOffsetY + yIndex * settings.HalfTileOffsetY,
                settings.StartPosition.z
            );
        }
        
        public Vector3 GridIndexToWorldPosition(Vector2Int gridPosition)
        {
            return new Vector3(
                settings.StartPosition.x + gridPosition.x * settings.HalfTileOffsetX + gridPosition.y * settings.HalfTileOffsetX,
                settings.StartPosition.y + gridPosition.x * -settings.HalfTileOffsetY + gridPosition.y * settings.HalfTileOffsetY,
                settings.StartPosition.z
            );
        }

        private Vector2Int GetRandomGridPosition(int gridSizeX, int gridSizeY)
        {
            var x = Random.Range(0, gridSizeX);
            var y = Random.Range(0, gridSizeY);
            return new Vector2Int(x, y);
        }
    }
}