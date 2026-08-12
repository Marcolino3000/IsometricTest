using System;
using System.Collections.Generic;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Items;
using Runtime.Gameplay.Traits;
using TMPro;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class Tile : MonoBehaviour, IClickable
    {
        public Vector2Int Position;
        public bool IsOccupied {get; private set;}

        /// <summary>
        /// The lootbox lying here, or null. Unlike a unit it does not occupy the tile - it has to be
        /// walked onto to be taken. Kept on the tile so the box is found in one step and follows the
        /// fog the ground is already tinted with.
        /// </summary>
        public Lootbox Lootbox { get; private set; }

        public TerrainType Terrain { get; private set; }
        public bool IsPassable { get; private set; } = true;
        public int ExtraMoveCost { get; private set; }
        public float HeightOffset { get; private set; }

        /// <summary>
        /// How high this tile stands for the sight rules - see <see cref="Global.SightRules.BlocksSight"/>.
        /// Not the visual <see cref="HeightOffset"/>: what a tile looks like and what can be seen
        /// over it are authored apart.
        /// </summary>
        public int Elevation { get; private set; }

        // Traits this tile's terrain grants to the unit standing on it (e.g. hill defence/range).
        public IReadOnlyList<TerrainTrait> Traits { get; private set; } = Array.Empty<TerrainTrait>();

        // Defaults to Visible so the board renders normally when no FogOfWarManager is driving it.
        public TileVisibility Visibility { get; private set; } = TileVisibility.Visible;

        [SerializeField] private Unit unit;
        [SerializeField] private TileMarker marker;

        private SpriteRenderer spriteRenderer;
        private Color baseTerrainColor = Color.white;

        // Kept so a box placed after the last fog pass is tinted like the ground it lands on.
        private Color fogTint = Color.white;

        // The debug label naming this tile's grid position. Found rather than serialized - it is the
        // only text on the prefab - and held, because once switched off a lookup would no longer
        // find it.
        private TextMeshPro coordinateLabel;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            coordinateLabel = GetComponentInChildren<TextMeshPro>(true);
        }

        /// <summary>
        /// Writes this tile's grid position onto it, or takes the label away. Switched by
        /// <see cref="Global.GameRules.ShowTileCoordinates"/> and pushed by the spawner, so it can be
        /// flipped mid-play; the text is rewritten each time rather than at spawn, since a tile that
        /// spawned with the label off has never had one written.
        /// </summary>
        public void ShowCoordinates(bool show)
        {
            if (coordinateLabel == null)
                return;

            coordinateLabel.text = $"{Position.x}-{Position.y}";
            coordinateLabel.enabled = show;
        }


        public int DistanceTo(Tile other)
        {
            return Mathf.Abs(Position.x - other.Position.x) + Mathf.Abs(Position.y - other.Position.y);
        }

        public void SetUnit(Unit unit)
        {
            this.unit = unit;
            SetOccupied(unit != null);
        }

        /// <summary>Puts a lootbox on this tile, or clears it with null once the box is taken.</summary>
        public void SetLootbox(Lootbox lootbox)
        {
            Lootbox = lootbox;
            RefreshLootbox();
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
            Elevation = profile.Elevation;
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

            fogTint = visibility switch
            {
                TileVisibility.Visible => Color.white,
                TileVisibility.Explored => exploredTint,
                _ => hiddenTint
            };

            if (spriteRenderer != null)
                spriteRenderer.color = baseTerrainColor * fogTint;

            RefreshMarker();
            RefreshLootbox();
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

        /// <summary>
        /// Shows the box on this tile only once the ground has been seen and tints it along with it -
        /// a box that has not been scouted must not shine out of the dark. Unlike an enemy it stays
        /// visible on remembered ground: it does not move, so what was seen there is still true.
        /// </summary>
        private void RefreshLootbox()
        {
            if (Lootbox == null)
                return;

            Lootbox.SetVisibility(Visibility != TileVisibility.Hidden, fogTint);
        }
    }
}