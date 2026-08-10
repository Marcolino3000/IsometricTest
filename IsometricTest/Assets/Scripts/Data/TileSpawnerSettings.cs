using System.Collections.Generic;
using System.Linq;
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
        public bool AllowMovementInAllDirections;

        [Header("Spawn Settings")]
        [Tooltip("The player spawns on a tile no further than this from the centre of the map.")]
        public int PlayerSpawnRadius = 2;
        [Tooltip("Opponents spawn in a ring around the player - no closer to it than this.")]
        public int OpponentSpawnDistanceMin = 4;
        [Tooltip("Opponents spawn in a ring around the player - no further from it than this.")]
        public int OpponentSpawnDistanceMax = 6;

        /// <summary>The middle tile of the grid, which the player spawns around.</summary>
        public Vector2Int GridCenter => new((GridSizeX - 1) / 2, (GridSizeY - 1) / 2);

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
        /// tiles; otherwise the fixed <see cref="HillPositions"/>/<see cref="MountainPositions"/> lists are used.
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
        /// All grid positions, returned in randomized order, used to scatter random terrain. Spawn zones are
        /// included: they move with the player's roll, and a zone that ends up walled off spills outwards
        /// (see <see cref="GetSpawnZonePositions"/>) rather than losing its spawn.
        /// </summary>
        private List<Vector2Int> GetShuffledRandomTerrainCandidates()
        {
            var candidates = AllPositions().ToList();

            // Fisher–Yates shuffle
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates;
        }

        /// <summary>
        /// Where a unit of <paramref name="team"/> may spawn, best candidates first. The player takes a
        /// tile within <see cref="PlayerSpawnRadius"/> of <see cref="GridCenter"/>; an opponent takes one
        /// in the ring between <see cref="OpponentSpawnDistanceMin"/> and <see cref="OpponentSpawnDistanceMax"/>
        /// around the player, which is why <paramref name="playerPosition"/> is passed in - the player is
        /// placed first and the opponents' zone is measured from where it landed.
        /// <para>
        /// Distance is Euclidean, so a zone reads as a circle rather than the diamond Manhattan would give
        /// - the same metric fog sight uses. Every grid position is returned, not just the zone's: the ones
        /// inside it come first in random order, the rest follow ordered by how far outside they are, so a
        /// zone blocked by terrain or already-placed units spills outwards instead of failing to spawn.
        /// </para>
        /// </summary>
        public List<Vector2Int> GetSpawnZonePositions(Team team, Vector2Int playerPosition)
        {
            var isPlayer = team == Team.Player;
            var center = isPlayer ? GridCenter : playerPosition;
            float minDistance = isPlayer ? 0 : OpponentSpawnDistanceMin;
            float maxDistance = isPlayer ? PlayerSpawnRadius : OpponentSpawnDistanceMax;

            return AllPositions()
                .OrderBy(position => DistanceOutsideZone(Vector2.Distance(position, center), minDistance, maxDistance))
                .ThenBy(_ => Random.value)
                .ToList();
        }

        /// <summary>How far <paramref name="distance"/> misses the ring between the two radii; 0 inside it.</summary>
        private static float DistanceOutsideZone(float distance, float minDistance, float maxDistance)
        {
            return Mathf.Max(0f, Mathf.Max(minDistance - distance, distance - maxDistance));
        }

        private IEnumerable<Vector2Int> AllPositions()
        {
            for (int x = 0; x < GridSizeX; x++)
            for (int y = 0; y < GridSizeY; y++)
                yield return new Vector2Int(x, y);
        }
    }
}