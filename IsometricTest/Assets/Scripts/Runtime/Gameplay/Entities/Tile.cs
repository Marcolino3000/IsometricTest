using System;
using System.Collections.Generic;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class Tile : MonoBehaviour, IClickable
    {
        public Vector2Int Position;
        public bool IsOccupied {get; private set;}

        public TerrainType Terrain { get; private set; }
        public bool IsPassable { get; private set; } = true;
        public int ExtraMoveCost { get; private set; }
        public float HeightOffset { get; private set; }

        // Traits this tile's terrain grants to the unit standing on it (e.g. hill defence/range).
        public IReadOnlyList<TerrainTrait> Traits { get; private set; } = Array.Empty<TerrainTrait>();

        // Defaults to Visible so the board renders normally when no FogOfWarManager is driving it.
        public TileVisibility Visibility { get; private set; } = TileVisibility.Visible;

        [SerializeField] private Unit unit;
        [SerializeField] private TileMarker marker;

        private SpriteRenderer spriteRenderer;
        private Color baseTerrainColor = Color.white;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetUnit(Unit unit)
        {
            this.unit = unit;
            SetOccupied(unit != null);
        }

        /// <summary>
        /// Applies a terrain profile to this tile: stores its movement rules, raises the tile
        /// visually by the profile's height offset and optionally tints the tile sprite.
        /// </summary>
        public void ApplyTerrain(TerrainProfile profile)
        {
            if (profile == null)
                return;

            Terrain = profile.Type;
            IsPassable = profile.Passable;
            ExtraMoveCost = profile.ExtraMoveCost;
            HeightOffset = profile.HeightOffset;
            Traits = profile.Traits ?? (IReadOnlyList<TerrainTrait>)Array.Empty<TerrainTrait>();

            transform.position += Vector3.up * profile.HeightOffset;

            if (spriteRenderer != null)
            {
                if (profile.OverrideSprite != null)
                    spriteRenderer.sprite = profile.OverrideSprite;

                if (profile.OverrideColor)
                    spriteRenderer.color = profile.Color;

                // Remember the lit colour so fog tinting can multiply against it (and restore it later).
                baseTerrainColor = spriteRenderer.color;
            }
        }

        /// <summary>
        /// Applies a fog state to the tile: tints the terrain sprite and hides the tile marker
        /// (e.g. the "occupied" highlight) unless the tile is currently visible, so enemy
        /// positions don't leak through fog.
        /// </summary>
        public void SetVisibility(TileVisibility visibility, Color exploredTint, Color hiddenTint)
        {
            Visibility = visibility;

            if (spriteRenderer != null)
            {
                var tint = visibility switch
                {
                    TileVisibility.Visible => Color.white,
                    TileVisibility.Explored => exploredTint,
                    _ => hiddenTint
                };
                spriteRenderer.color = baseTerrainColor * tint;
            }

            RefreshMarker();
        }

        private void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            RefreshMarker();
        }

        /// <summary>
        /// Shows the "occupied" marker only on currently visible tiles, so an enemy standing on a
        /// hidden or explored tile is not given away by its marker.
        /// </summary>
        private void RefreshMarker()
        {
            if (marker == null)
                return;

            marker.SetMarkerColor(Visibility == TileVisibility.Visible && IsOccupied
                ? MarkerColor.Orange
                : MarkerColor.None);
        }
    }
}