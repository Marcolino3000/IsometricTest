using System;
using System.Collections.Generic;
using Data;
using UnityEngine;

namespace DefaultNamespace
{
    public class TileSpawner : MonoBehaviour
    {
        [SerializeField] private TileSpawnerSettings settings;
        [SerializeField] private List<GameObject> tiles;
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
            
            var tile = Instantiate(settings.TilePrefab, position, Quaternion.identity);
            tile.name = $"Tile {xIndex}-{yIndex}";
            tiles.Add(tile);
        }

        private Vector3 GridIndexToWorldPosition(int xIndex, int yIndex)
        {
            return new Vector3(
                settings.StartPosition.x + xIndex * settings.HalfTileOffsetX + yIndex * settings.HalfTileOffsetX,
                settings.StartPosition.y + xIndex * -settings.HalfTileOffsetY + yIndex * settings.HalfTileOffsetY,
                settings.StartPosition.z
            );
        }
    }
}