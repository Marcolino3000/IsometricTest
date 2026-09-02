using System;
using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// The rings of the map, from the middle outwards, and what belongs to no single one of them:
    /// how a border between two of them is drawn and what is said when one is entered.
    ///
    /// A <see cref="RuntimeSettings"/>, so a radius moved in the inspector announces itself and the
    /// borders are redrawn mid-play rather than polled for drift.
    ///
    /// Loaded from Resources by <see cref="ZoneRules"/> rather than injected, like the tooltip and
    /// effect-animation settings: the border and the announcement are both built from code, so
    /// nothing has to be wired, and a project with no asset simply has no zones - the map is then
    /// one undivided board, exactly as it was before zones existed.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Settings/ZoneSettings")]
    public class ZoneSettings : RuntimeSettings
    {
        public const string ResourcePath = "Settings/Default ZoneSettings";

        [Tooltip("The rings, innermost first. The player starts in the first one; each further one " +
                 "begins where the one before it ended.")]
        public List<MapZone> Zones = new();

        [Tooltip("When on, a ring's opponents and boxes are held back until the character first " +
                 "walks into it, instead of the whole map being filled at the start. Everything is " +
                 "still made and rolled up front - only its arrival waits - so a match yields the " +
                 "same things either way and undo takes an arrival back like any other change. " +
                 "Switch it off to fill the board at the start, which is what the game did before " +
                 "rings existed.")]
        public bool SpawnOnEntry = true;

        [Header("Border")]
        [Tooltip("What stands on the seam between two rings - a strip drawn upright, cut into " +
                 "half-tile pieces and set along the boundary, so a wall of flame reads as one " +
                 "however the ring turns. Left empty, a pixel-art line is drawn flat along the " +
                 "tile edges instead, from the two numbers below, so a border shows before any art " +
                 "is made for it.")]
        public Sprite BorderSprite;

        [Tooltip("How tall that strip is drawn, in world units - half a tile is 0.25, so a third " +
                 "of a unit stands a little above the ground it divides without hiding what is " +
                 "behind it. The strip is cut to its own pixels at this height, never stretched.")]
        [Min(0.01f)] public float BorderHeight = 0.3f;

        [Tooltip("How far the foot of the border is set into the ground, in world units. A little " +
                 "sunk reads as standing on the seam; nothing at all reads as balanced on top of " +
                 "it.")]
        [Min(0f)] public float BorderSink = 0.06f;

        [Tooltip("How wide the generated line is, in pixels. It spans exactly half a tile, so this " +
                 "is what its pixels are measured against: 16 draws the border at half the " +
                 "resolution of a 32-pixel-wide tile.")]
        [Min(4)] public int BorderPixelWidth = 16;

        [Tooltip("How thick the generated line is, in pixels of that same scale. Two is a hairline " +
                 "that reads as a boundary rather than as an outline around every tile.")]
        [Min(1)] public int BorderThickness = 2;

        [Tooltip("How solid a border is drawn, on top of its zone's own colour. Below one it reads " +
                 "as a marking on the ground rather than as a wall standing on it.")]
        [Range(0f, 1f)] public float BorderOpacity = 0.85f;

        [Tooltip("Sorting order of the border sprites. Above the tiles and their markers so the " +
                 "movement highlights do not paint over it - ground 0, tile marker 1, border and " +
                 "lootbox 2, unit 3.")]
        public int OrderInLayer = 2;

        [Header("Announcement")]
        [Tooltip("What the screen says the first time any ring is entered. A zone authoring a line " +
                 "of its own says that instead.")]
        public string EnteredHeadline = "The horde is puzzled";

        /// <summary>How many rings the map is divided into; 0 for an undivided map.</summary>
        public int Count => Zones?.Count ?? 0;

        /// <summary>The ring at <paramref name="index"/>, or null for one outside the list.</summary>
        public MapZone At(int index)
        {
            return index >= 0 && index < Count ? Zones[index] : null;
        }

        /// <summary>
        /// The outer edge of ring <paramref name="index"/>. Read as a running maximum, so a list
        /// authored out of order cannot produce a ring that begins outside where it ends; the last
        /// one always reaches the rim, or the tiles beyond it would be in no zone at all.
        /// </summary>
        public float OuterRadius(int index)
        {
            if (index >= Count - 1)
                return 1f;

            var radius = 0f;

            for (var i = 0; i <= index; i++)
                radius = Mathf.Max(radius, At(i).Radius);

            return radius;
        }

        /// <summary>The inner edge of ring <paramref name="index"/> - where the ring before it ended.</summary>
        public float InnerRadius(int index)
        {
            return index <= 0 ? 0f : OuterRadius(index - 1);
        }

        /// <summary>
        /// Which ring a tile lying at <paramref name="distanceFromCenter"/> (0 in the middle of the
        /// map, 1 at its furthest tile) falls in; -1 while no zone is authored. The outermost ring
        /// catches whatever is left, so every tile of the board is in exactly one.
        /// </summary>
        public int IndexAt(float distanceFromCenter)
        {
            for (var index = 0; index < Count; index++)
                if (distanceFromCenter <= OuterRadius(index))
                    return index;

            return Count - 1;
        }

        /// <summary>
        /// How far a tile at <paramref name="distanceFromCenter"/> falls outside ring
        /// <paramref name="index"/>; 0 inside it, which is what makes the ring itself sort as one
        /// block for a random tiebreak to scatter.
        ///
        /// The same shape as a spawn zone's miss distance, and for the same reason: a ring is
        /// preferred rather than required, so one walled off by mountains or already filled takes
        /// the nearest ground outside it instead of losing what belongs in it.
        /// </summary>
        public float DistanceOutside(int index, float distanceFromCenter)
        {
            if (index < 0 || index >= Count)
                return 0f;

            return Mathf.Max(0f,
                Mathf.Max(InnerRadius(index) - distanceFromCenter, distanceFromCenter - OuterRadius(index)));
        }

        /// <summary>
        /// The authored asset, or a default instance where none is authored yet. Never null, so
        /// nothing needs a branch for a project that has not made one - a settings asset with no
        /// zones in it is simply an undivided map.
        /// </summary>
        public static ZoneSettings Load()
        {
            var settings = Resources.Load<ZoneSettings>(ResourcePath);

            return settings != null ? settings : CreateInstance<ZoneSettings>();
        }
    }

    /// <summary>
    /// One ring of the map, measured out from the tile the player spawns on. A zone is a
    /// <b>distance</b> and nothing else: how far out it reaches, what colour marks its edge and
    /// what the screen says on the way in. Who guards it and what lies in it are authored where
    /// those things are spawned - <see cref="UnitSpawnerSettings.OpponentUnits"/> and
    /// <see cref="LootSpawnerSettings.Boxes"/>, each entry naming a ring and a count - so how many
    /// of a thing a match holds is read in one place rather than split between here and there.
    ///
    /// A zone owns only its <b>outer</b> edge. Where it begins is where the zone before it ended,
    /// so the rings tile the map with no gap and no overlap however they are authored - the same
    /// reason flat terrain is whatever the hills and mountains leave over rather than a third
    /// percentage of its own.
    /// </summary>
    [Serializable]
    public class MapZone
    {
        [Tooltip("The outer edge of this ring, as a fraction of the way from the middle of the map " +
                 "to its furthest tile: 0 is the centre, 1 the rim. Where the ring begins is the " +
                 "outer edge of the one before it, so only this has to be authored. The last zone " +
                 "reaches the rim whatever it says here, so no tile is left in no zone.")]
        [Range(0f, 1f)] public float Radius = 1f;

        [Tooltip("The colour the line along this ring's inner edge is drawn in. An edge wears the " +
                 "colour of the zone being entered - the line marks the danger ahead, not the " +
                 "ground behind.")]
        public Color Color = new(1f, 1f, 1f, 1f);

        [Tooltip("What the screen says when this ring is first entered. Falls back to the line " +
                 "authored for every zone in the settings while empty.")]
        public string Headline;

        [Tooltip("The smaller line under it, if there is anything to add. Nothing is printed while " +
                 "it is empty - a ring is a distance, not a place, so it has no name of its own.")]
        public string Detail;
    }
}
