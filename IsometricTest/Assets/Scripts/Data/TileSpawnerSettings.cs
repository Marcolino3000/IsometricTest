using System;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// How much of one ring is made of one kind of terrain. The same shape as the roster and the
    /// loot list: a kind, an amount and the ring it belongs to, so where the ground gets rough is
    /// authored beside where the boxes and the opponents are rather than in a place of its own.
    ///
    /// The kind is named rather than described - the three profiles are authored once on the
    /// settings, so an entry says how much of a ring is hills, not what a hill is.
    /// </summary>
    [Serializable]
    public class TerrainAmount
    {
        public TerrainType Terrain = TerrainType.Hills;

        [Tooltip("Percent of this ring's tiles made of it. Whatever the ring's entries leave over " +
                 "is flat ground.")]
        [Range(0, 100)] public int Percent;

        [Tooltip("Which ring of the map, counted from the middle out: 0 is the one the player " +
                 "spawns in. Below zero the entry is the mix for every ring that authors none of " +
                 "its own.")]
        public int Zone = -1;

        /// <summary>Whether this entry belongs to a ring of the map at all.</summary>
        public bool HasZone => Zone >= 0;
    }

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

        [Tooltip("What the ground is made of, ring by ring: a kind of terrain, how much of that " +
                 "ring's tiles it takes, and which ring. Whatever the entries of a ring leave over " +
                 "is flat. An entry naming no ring is the mix used for every ring that authors none " +
                 "of its own, which is what a map with no rings uses for the whole board.")]
        public List<TerrainAmount> RandomTerrain = new()
        {
            new TerrainAmount { Terrain = TerrainType.Hills, Percent = 33 },
            new TerrainAmount { Terrain = TerrainType.Mountain, Percent = 9 }
        };

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
        /// <param name="zoneOf">
        /// Which ring a position falls in, or -1 for a map with no rings. Handed in rather than
        /// asked of the rules, so an asset stays a thing that is read rather than one that reaches
        /// into the game to answer.
        /// </param>
        public Dictionary<Vector2Int, TerrainProfile> BuildTerrainMap(Func<Vector2Int, int> zoneOf = null)
        {
            return RandomTerrainPlacement ? BuildRandomTerrainMap(zoneOf) : BuildFixedTerrainMap();
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

        /// <summary>
        /// Scatters the terrain one ring at a time: each ring's own tiles are shuffled and handed
        /// out in the proportions authored for it, so how rough the ground is can grow with the
        /// distance from the middle the way the loot and the opponents do. A map with no rings is
        /// one ring as far as this is concerned - every tile in the same bag, which is what the
        /// board did before rings existed.
        /// </summary>
        private Dictionary<Vector2Int, TerrainProfile> BuildRandomTerrainMap(Func<Vector2Int, int> zoneOf)
        {
            var map = new Dictionary<Vector2Int, TerrainProfile>();

            foreach (var ring in GroupByZone(zoneOf))
            {
                var candidates = ring.Value;
                var entries = TerrainFor(ring.Key);

                if (entries.Count == 0)
                    continue;

                Shuffle(candidates);

                var counts = RandomTerrainCounts(candidates.Count, entries);
                var index = 0;

                for (var i = 0; i < entries.Count; i++)
                    for (var placed = 0; placed < counts[i + 1] && index < candidates.Count; placed++, index++)
                        map[candidates[index]] = ProfileOf(entries[i].Terrain);
            }

            return map;
        }

        /// <summary>
        /// The tiles of each ring, keyed by it. Every position lands in exactly one, so nothing is
        /// handed out twice however the rings are authored.
        /// </summary>
        private Dictionary<int, List<Vector2Int>> GroupByZone(Func<Vector2Int, int> zoneOf)
        {
            var rings = new Dictionary<int, List<Vector2Int>>();

            foreach (var position in AllPositions())
            {
                var zone = zoneOf?.Invoke(position) ?? -1;

                if (!rings.TryGetValue(zone, out var tiles))
                    rings[zone] = tiles = new List<Vector2Int>();

                tiles.Add(position);
            }

            return rings;
        }

        /// <summary>
        /// What ring <paramref name="zone"/> is made of: the entries naming it, or the ones naming
        /// no ring at all, which is the mix every ring falls back to.
        /// </summary>
        private List<TerrainAmount> TerrainFor(int zone)
        {
            var own = new List<TerrainAmount>();
            var shared = new List<TerrainAmount>();

            foreach (var entry in RandomTerrain)
            {
                if (entry == null || entry.Percent <= 0)
                    continue;

                if (entry.Zone == zone)
                    own.Add(entry);
                else if (!entry.HasZone)
                    shared.Add(entry);
            }

            return own.Count > 0 ? own : shared;
        }

        /// <summary>
        /// How many of <paramref name="total"/> tiles each entry takes, with flat ground first as
        /// the share nothing else asked for. Derived from the percentages rather than authored as
        /// counts, so a resized map keeps the same mix instead of having every number rewritten -
        /// and read as shares of whatever they come to, so entries adding past 100 are scaled down
        /// between them rather than the last ones being starved by the first.
        /// </summary>
        private static int[] RandomTerrainCounts(int total, List<TerrainAmount> entries)
        {
            var shares = new int[entries.Count + 1];
            var asked = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                shares[i + 1] = Mathf.Max(0, entries[i].Percent);
                asked += shares[i + 1];
            }

            shares[0] = Mathf.Max(0, 100 - asked);

            return LootboxType.Distribute(total, shares);
        }

        /// <summary>The profile a kind of terrain is drawn and walked with.</summary>
        private TerrainProfile ProfileOf(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Hills => HillTerrain,
                TerrainType.Mountain => MountainTerrain,
                _ => FlatTerrain
            };
        }

        private static void Shuffle(List<Vector2Int> positions)
        {
            // Fisher–Yates shuffle
            for (int i = positions.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }
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
                .ThenBy(_ => UnityEngine.Random.value)
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