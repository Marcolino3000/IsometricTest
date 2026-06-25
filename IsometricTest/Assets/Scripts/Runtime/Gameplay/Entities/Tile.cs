using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Feedback;
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

        [SerializeField] private Unit unit;
        [SerializeField] private TileMarker marker;

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

            transform.position += Vector3.up * profile.HeightOffset;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (profile.OverrideSprite != null)
                    spriteRenderer.sprite = profile.OverrideSprite;

                if (profile.OverrideColor)
                    spriteRenderer.color = profile.Color;
            }
        }

        private void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            
            if(occupied)
                marker.SetMarkerColor(MarkerColor.Orange);
            else
                marker.SetMarkerColor(MarkerColor.None);
        }
    }
}