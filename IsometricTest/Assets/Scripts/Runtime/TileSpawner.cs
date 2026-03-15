using System.Collections.Generic;
using Data;
using TMPro;
using UnityEngine;

namespace Runtime
{
    public class TileSpawner : MonoBehaviour
    {
        [SerializeField] private TileSpawnerSettings settings;
        [SerializeField] private static List<GameObject> tiles = new();

        public bool CheckGridPosition(int x, int y)
        {
            return x >= 0 && x < settings.GridSizeX && y >= 0 && y < settings.GridSizeY;
        }

        public static void HighlightTile(GameObject tile)
        {
            tile.GetComponentInChildren<TileMarker>().SetMarkerColor(MarkerColor.Orange);
        }

        public void HighlightTile(Vector2Int tilePosition)
        {
            if (!CheckGridPosition(tilePosition.x, tilePosition.y))
            {
                Debug.LogWarning("Tile Position was out of bounds");
                return;
            }

            var tile = tiles.Find(t => t.GetComponent<Tile>().Position == tilePosition);
            if (tile == null)
            {
                Debug.LogWarning("Tile not found at position: " + tilePosition);
                return;
            }
            
            tile.GetComponentInChildren<TileMarker>().SetMarkerColor(MarkerColor.Orange);
        }

        public static void ResetHighlightedTiles()
        {
            foreach (var tile in tiles)
            {
                tile.GetComponentInChildren<TileMarker>().SetMarkerColor(MarkerColor.None);
            }
        }

        private void Awake()
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
            foreach (var tile in tiles)
            {
                Destroy(tile);
            }
        }

        private void SpawnTile(int xIndex, int yIndex)
        {
            var position = GridIndexToWorldPosition(xIndex, yIndex);
            
            var tile = Instantiate(settings.TilePrefab, position, Quaternion.identity,transform);
            tile.name = $"Tile {xIndex}-{yIndex}";
            tile.GetComponentInChildren<TextMeshPro>().text = xIndex + "-" + yIndex;
            tile.GetComponent<Tile>().Position = new Vector2Int(xIndex, yIndex);
            tiles.Add(tile);
        }

        public Vector2Int GetRandomGridPosition()
        {
            return GetRandomGridPosition(settings.GridSizeX, settings.GridSizeY);
            
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