using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/TileSpawnerSettings")]
    public class TileSpawnerSettings : ScriptableObject
    {
        [Header("Tile Settings")]
        public GameObject TilePrefab;
        public float TileSize;
        public float HalfTileOffsetX;
        public float HalfTileOffsetY;

        [Header("Grid Settings")] 
        public Vector3 StartPosition;
        public int GridSizeX;
        public int GridSizeY;
        public int SpawnZoneSize;
        
        public List<Vector2Int> GetSpawnZonePositions(Team team)
        {
            var positions = new List<Vector2Int>();

            switch (team)
            {
                case Team.Player:
                    // Player spawns in the first SpawnZoneSize rows (y), across all columns (x)
                    for (int y = 0; y < SpawnZoneSize && y < GridSizeY; y++)
                    {
                        for (int x = 0; x < GridSizeX; x++)
                        {
                            positions.Add(new Vector2Int(x, y));
                        }
                    }
                    break;

                case Team.Opponent:
                    // Opponent spawns in the last SpawnZoneSize rows (y), across all columns (x)
                    for (int y = Mathf.Max(0, GridSizeY - SpawnZoneSize); y < GridSizeY; y++)
                    {
                        for (int x = 0; x < GridSizeX; x++)
                        {
                            positions.Add(new Vector2Int(x, y));
                        }
                    }
                    break;
            }

            return positions;
        }
    }
}