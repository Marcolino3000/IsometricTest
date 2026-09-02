using System;
using System.Collections.Generic;
using Data;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Where a tile lies on the map, in rings measured out from the middle - the very tile the
    /// player spawns around. The <see cref="CombatRules"/>-style query for it: pure functions over a
    /// position, no state of its own beyond what it was set up with, so the spawners, the border
    /// drawn between two rings and the line announcing one all read the same answer and cannot
    /// disagree about where a zone ends.
    ///
    /// It is the one place a distance becomes a zone. Nothing else measures out from the centre:
    /// <see cref="TileSpawner.DistanceFromCenter(Tile)"/> is asked for the fraction and the ring
    /// bounds come off <see cref="ZoneSettings"/>, which owns them.
    ///
    /// Like <see cref="SightRules"/> it cannot answer from its arguments alone, so the Initiator
    /// hands it the tile spawner. Without an authored settings asset it reports no zones at all,
    /// which leaves the map one undivided board rather than breaking anything that asks.
    /// </summary>
    public static class ZoneRules
    {
        private static readonly MapZone[] NoZones = Array.Empty<MapZone>();

        private static ZoneSettings settings;
        private static TileSpawner tileSpawner;

        /// <summary>
        /// Hands over what the zones are measured against. <paramref name="zoneSettings"/> is
        /// loaded from Resources when none is passed, so nothing has to be wired for a match to
        /// have zones.
        /// </summary>
        public static void Setup(TileSpawner tiles, ZoneSettings zoneSettings = null)
        {
            tileSpawner = tiles;
            settings = zoneSettings != null ? zoneSettings : ZoneSettings.Load();

            // Said out loud rather than left to be noticed on the board: with no rings the map is
            // one zone, so no border is drawn and every ring's roster and loot is simply missing.
            if (settings == null || settings.Count == 0)
                Debug.LogWarning($"No zones: nothing was loaded from Resources/{ZoneSettings.ResourcePath}, " +
                                 "so the map is one undivided zone.");
        }

        /// <summary>The asset the rings are authored in. Never null once set up.</summary>
        public static ZoneSettings Settings => settings;

        /// <summary>The rings, innermost first. Empty while none are authored.</summary>
        public static IReadOnlyList<MapZone> Zones =>
            settings != null && settings.Zones != null ? settings.Zones : NoZones;

        /// <summary>How many rings the map is divided into; 0 for an undivided one.</summary>
        public static int Count => Zones.Count;

        /// <summary>Which ring <paramref name="position"/> falls in, or -1 while none are authored.</summary>
        public static int IndexAt(Vector2Int position)
        {
            if (settings == null || tileSpawner == null || Count == 0)
                return -1;

            return settings.IndexAt(tileSpawner.DistanceFromCenter(position));
        }

        /// <summary>The same for a tile already in hand. -1 for a null tile, so it is safe to compare.</summary>
        public static int IndexAt(Tile tile)
        {
            return tile != null ? IndexAt(tile.Position) : -1;
        }

        public static MapZone ZoneAt(Vector2Int position)
        {
            return settings != null ? settings.At(IndexAt(position)) : null;
        }

        public static MapZone ZoneAt(Tile tile)
        {
            return tile != null ? ZoneAt(tile.Position) : null;
        }

        /// <summary>Where <paramref name="zone"/> stands in the list, or -1 for one not in it.</summary>
        public static int IndexOf(MapZone zone)
        {
            for (var index = 0; index < Count; index++)
                if (ReferenceEquals(Zones[index], zone))
                    return index;

            return -1;
        }

        /// <summary>
        /// How far <paramref name="position"/> misses ring <paramref name="index"/>; 0 inside it.
        /// What a caller placing something in a zone orders its candidates by, so a ring that
        /// cannot take everything meant for it spills over its border rather than losing it.
        /// </summary>
        public static float DistanceOutside(int index, Vector2Int position)
        {
            if (settings == null || tileSpawner == null)
                return 0f;

            return settings.DistanceOutside(index, tileSpawner.DistanceFromCenter(position));
        }

        public static float DistanceOutside(MapZone zone, Vector2Int position)
        {
            return DistanceOutside(IndexOf(zone), position);
        }

        /// <summary>
        /// How far <paramref name="tile"/> misses the ground a kind of lootbox belongs on: the
        /// nearest of the rings listing it, or 0 everywhere for a kind no zone lists - which is what
        /// puts an unclaimed kind anywhere on the map, since every tile then misses by the same
        /// nothing.
        ///
        /// The nearest rather than one ring of its own is what lets a kind be listed by several
        /// zones without its count having to be split between them: its boxes are simply scattered
        /// over whichever of those rings has room.
        /// </summary>
        public static float DistanceOutside(LootboxType type, Tile tile)
        {
            if (type == null || tile == null || Count == 0)
                return 0f;

            var nearest = float.MaxValue;

            for (var index = 0; index < Count; index++)
            {
                var zone = Zones[index];

                if (zone?.Loot == null || !zone.Loot.Contains(type))
                    continue;

                nearest = Mathf.Min(nearest, DistanceOutside(index, tile.Position));
            }

            return nearest == float.MaxValue ? 0f : nearest;
        }

        /// <summary>
        /// The big line said when ring <paramref name="index"/> is first entered: the zone's own, or
        /// the one authored for every zone.
        /// </summary>
        public static string HeadlineOf(int index)
        {
            var zone = settings != null ? settings.At(index) : null;

            if (zone == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(zone.Headline) ? settings.EnteredHeadline : zone.Headline;
        }

        /// <summary>
        /// The smaller line under it, or nothing at all - a ring is a distance rather than a place,
        /// so it has no name to print and says only what it authored.
        /// </summary>
        public static string DetailOf(int index)
        {
            var zone = settings != null ? settings.At(index) : null;

            return zone != null ? zone.Detail : string.Empty;
        }
    }
}
