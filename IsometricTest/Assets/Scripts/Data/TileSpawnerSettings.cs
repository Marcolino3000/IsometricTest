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

        [Tooltip("How high above its grid position a tile's surface is drawn, in world units - the " +
                 "diamond a unit stands on, which the tile art puts in the upper half of its sprite " +
                 "rather than at the tile's own position. What anything laid flat on the board is " +
                 "placed at; it is the offset of the tile prefab's marker, the sprite covering " +
                 "exactly that face.")]
        public float SurfaceOffset = 0.25f;

        [Header("Grid Settings")]
        public Vector3 StartPosition;
        public int GridSizeX;
        public int GridSizeY;
        public bool AllowMovementInAllDirections;

        [Header("Spawn Settings")]
        [Tooltip("The player spawns on a tile no further than this from the centre of the map.")]
        public int PlayerSpawnRadius = 2;
        [Tooltip("How many tiles deep the opponents' band around the rim of the map is: 1 spawns them on the outermost tiles only, 2 adds the row behind those, and so on.")]
        public int OpponentSpawnZoneSize = 2;

        /// <summary>The middle tile of the grid, which the player spawns around.</summary>
        public Vector2Int GridCenter => new((GridSizeX - 1) / 2, (GridSizeY - 1) / 2);

        /// <summary>
        /// How far it is from <see cref="GridCenter"/> to the furthest tile of the grid - what a
        /// distance from the middle is measured against, so anything authored as a fraction of it
        /// means the same thing whatever size the map is. Euclidean, like the player's spawn circle
        /// and fog sight, so a fraction describes a ring rather than the diamond Manhattan would give.
        /// </summary>
        public float GridRadius
        {
            get
            {
                // The centre is rounded down on both axes, so the far side is the longer one.
                var x = Mathf.Max(GridCenter.x, GridSizeX - 1 - GridCenter.x);
                var y = Mathf.Max(GridCenter.y, GridSizeY - 1 - GridCenter.y);

                return Mathf.Sqrt(x * x + y * y);
            }
        }

        /// <summary>
        /// How far out <paramref name="position"/> lies: 0 at the middle of the map and 1 at the
        /// tile furthest from it. What anything authored as a fraction of the map is measured
        /// against - which ring a zone ends at, which ring a kind of lootbox lies in - so all of
        /// them mean the same thing whatever size the grid is.
        /// </summary>
        public float DistanceFromCenter(Vector2Int position)
        {
            var radius = GridRadius;

            return radius <= 0f ? 0f : Mathf.Clamp01(Vector2.Distance(position, GridCenter) / radius);
        }

        [Header("Terrain Settings")]
        public TerrainProfile FlatTerrain = new() { Type = TerrainType.Flat };
        public TerrainProfile HillTerrain = new() { Type = TerrainType.Hills, ExtraMoveCost = 1, HeightOffset = 0.1f, Elevation = 1 };
        public TerrainProfile MountainTerrain = new() { Type = TerrainType.Mountain, Passable = false, HeightOffset = 0.2f, Elevation = 2, OverrideColor = true, Color = Color.white };

        [Tooltip("When enabled, hills and mountains are scattered on random tiles (outside spawn zones) instead of using the fixed position lists below.")]
        public bool RandomTerrainPlacement;

        [Tooltip("Percent of the map's tiles that are hills when random terrain placement is enabled. " +
                 "A share of the whole grid rather than a number of tiles, so it means the same on any map size.")]
        [Range(0, 100)] public int RandomHillPercent;

        [Tooltip("Percent of the map's tiles that are mountains, on the same scale. Whatever the two " +
                 "leave over is flat, so they only have to be authored up to 100 - past it they are " +
                 "scaled down between them rather than the second losing out to the first.")]
        [Range(0, 100)] public int RandomMountainPercent;

        [Tooltip("Grid positions that should spawn as hills (used when random placement is disabled).")]
        public List<Vector2Int> HillPositions = new();
        [Tooltip("Grid positions that should spawn as mountains (used when random placement is disabled).")]
        public List<Vector2Int> MountainPositions = new();

        /// <summary>
        /// Builds the terrain layout for a fresh grid, keyed by grid position (only non-flat tiles are included).
        /// When <see cref="RandomTerrainPlacement"/> is enabled, mountains and hills are scattered on random
        /// tiles, as many of each as their percentage of the whole grid asks for; otherwise the fixed
        /// <see cref="HillPositions"/>/<see cref="MountainPositions"/> lists are used.
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
            var (hills, mountains) = RandomTerrainCounts(candidates.Count);
            var index = 0;

            for (int i = 0; i < mountains; i++, index++)
                map[candidates[index]] = MountainTerrain;

            for (int i = 0; i < hills; i++, index++)
                map[candidates[index]] = HillTerrain;

            return map;
        }

        /// <summary>
        /// How many of <paramref name="total"/> tiles are hills and how many are mountains, derived from
        /// the authored percentages rather than authored as counts, so a resized map keeps the same mix
        /// instead of having every number rewritten.
        /// </summary>
        public (int Hills, int Mountains) RandomTerrainCounts(int total)
        {
            // Flat is the share nothing else asked for, which is what keeps the three adding back up to
            // the map: read as shares of whatever they come to, so hills and mountains authored past 100
            // between them are scaled down rather than the second one being starved by the first.
            var flat = Mathf.Max(0, 100 - RandomHillPercent - RandomMountainPercent);
            var counts = LootboxType.Distribute(total, new[] { flat, RandomHillPercent, RandomMountainPercent });

            return (counts[1], counts[2]);
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
        /// Where a unit of <paramref name="team"/> may spawn, best candidates first. The player takes a tile
        /// within <see cref="PlayerSpawnRadius"/> of <see cref="GridCenter"/>; an opponent takes one in the
        /// <see cref="OpponentSpawnZoneSize"/>-deep band along the rim of the map. The two are measured from
        /// opposite ends on purpose - the player from the middle outwards, the opponents from the edge inwards
        /// - so the opponents encircle the player without either zone having to know where the other landed.
        /// <para>
        /// Every grid position is returned, not just the zone's: the ones inside it come first in random order,
        /// the rest follow ordered by how far outside they are, so a zone blocked by terrain or already-placed
        /// units spills over its border instead of failing to spawn.
        /// </para>
        /// </summary>
        public List<Vector2Int> GetSpawnZonePositions(Team team)
        {
            return AllPositions()
                .OrderBy(position => DistanceOutsideZone(position, team))
                .ThenBy(_ => Random.value)
                .ToList();
        }

        /// <summary>
        /// How far <paramref name="position"/> misses its team's spawn zone; 0 inside it, which is what makes
        /// the zone itself sort as one block for the random tiebreak to shuffle.
        /// </summary>
        private float DistanceOutsideZone(Vector2Int position, Team team)
        {
            // The player's zone is a circle, measured Euclidean like fog sight rather than the diamond
            // Manhattan would give. The opponents' is a band, so it counts whole tiles in from the rim -
            // a size of 1 is the outermost tiles and nothing else, which is why the depth is compared
            // against one less than the size.
            return team == Team.Player
                ? Mathf.Max(0f, Vector2.Distance(position, GridCenter) - PlayerSpawnRadius)
                : Mathf.Max(0f, DepthFromRim(position) - (OpponentSpawnZoneSize - 1));
        }

        /// <summary>How many tiles in from the nearest edge of the map a position lies; 0 on the rim itself.</summary>
        private int DepthFromRim(Vector2Int position)
        {
            return Mathf.Min(
                Mathf.Min(position.x, GridSizeX - 1 - position.x),
                Mathf.Min(position.y, GridSizeY - 1 - position.y));
        }

        private IEnumerable<Vector2Int> AllPositions()
        {
            for (int x = 0; x < GridSizeX; x++)
            for (int y = 0; y < GridSizeY; y++)
                yield return new Vector2Int(x, y);
        }
    }
}