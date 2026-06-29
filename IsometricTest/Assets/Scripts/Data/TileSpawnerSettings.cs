using System.Collections.Generic;
using Runtime;
using Runtime.Gameplay.Entities;
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
        public bool AllowMovementInAllDirections;

        [Header("Terrain Settings")]
        public TerrainProfile FlatTerrain = new() { Type = TerrainType.Flat };
        public TerrainProfile HillTerrain = new() { Type = TerrainType.Hills, ExtraMoveCost = 1, HeightOffset = 0.1f };
        public TerrainProfile MountainTerrain = new() { Type = TerrainType.Mountain, Passable = false, HeightOffset = 0.2f, OverrideColor = true, Color = Color.white };

        [Tooltip("When enabled, hills and mountains are scattered on random tiles (outside spawn zones) instead of using the fixed position lists below.")]
        public bool RandomTerrainPlacement;
        [Tooltip("Number of hills to place when random terrain placement is enabled.")]
        public int RandomHillCount;
        [Tooltip("Number of mountains to place when random terrain placement is enabled.")]
        public int RandomMountainCount;

        [Tooltip("Grid positions that should spawn as hills (used when random placement is disabled).")]
        public List<Vector2Int> HillPositions = new();
        [Tooltip("Grid positions that should spawn as mountains (used when random placement is disabled).")]
        public List<Vector2Int> MountainPositions = new();

        /// <summary>
        /// Builds the terrain layout for a fresh grid, keyed by grid position (only non-flat tiles are included).
        /// When <see cref="RandomTerrainPlacement"/> is enabled, mountains and hills are scattered on random
        /// tiles outside the spawn zones; otherwise the fixed <see cref="HillPositions"/>/<see cref="MountainPositions"/> lists are used.
        /// </summary>
        public Dictionary<Vector2Int, TerrainProfile> BuildTerrainMap()
        {
            return RandomTerrainPlacement ? BuildRandomTerrainMap() : BuildFixedTerrainMap();
        }

        private Dictionary<Vector2Int, TerrainProfile> BuildFixedTerrainMap()
        {
            var map = new Dictionary<Vector2Int, TerrainProfile>();

            if (MountainPositions != null)
                foreach (var position in MountainPositions)
                    map[position] = MountainTerrain;

            // hills do not overwrite mountains when a position appears in both lists
            if (HillPositions != null)
                foreach (var position in HillPositions)
                    map.TryAdd(position, HillTerrain);

            return map;
        }

        private Dictionary<Vector2Int, TerrainProfile> BuildRandomTerrainMap()
        {
            var map = new Dictionary<Vector2Int, TerrainProfile>();
            var candidates = GetShuffledRandomTerrainCandidates();
            var index = 0;

            for (int i = 0; i < RandomMountainCount && index < candidates.Count; i++, index++)
                map[candidates[index]] = MountainTerrain;

            for (int i = 0; i < RandomHillCount && index < candidates.Count; i++, index++)
                map[candidates[index]] = HillTerrain;

            return map;
        }

        /// <summary>
        /// All grid positions (spawn zones included), returned in randomized order, used to scatter random terrain.
        /// </summary>
        private List<Vector2Int> GetShuffledRandomTerrainCandidates()
        {
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < GridSizeX; x++)
            for (int y = 0; y < GridSizeY; y++)
                candidates.Add(new Vector2Int(x, y));

            // Fisher–Yates shuffle
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates;
        }

        public List<Vector2Int> GetSpawnZonePositions(Team team)
        {
            var positions = new List<Vector2Int>();

            switch (team)
            {
                case Team.Player:
                    for (int y = 0; y < SpawnZoneSize && y < GridSizeY; y++)
                    {
                        for (int x = 0; x < GridSizeX; x++)
                        {
                            positions.Add(new Vector2Int(x, y));
                        }
                    }
                    break;

                case Team.Opponent:
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