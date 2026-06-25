using System;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public enum TerrainType
    {
        Flat,
        Hills,
        Mountain
    }

    /// <summary>
    /// Describes how a <see cref="TerrainType"/> looks and how it affects movement.
    /// Flat tiles use the plain prefab; hills raise the tile and cost extra AP to enter;
    /// mountains are raised further, tinted and cannot be entered at all.
    /// </summary>
    [Serializable]
    public class TerrainProfile
    {
        public TerrainType Type;

        [Tooltip("Whether units are allowed to move onto this terrain. Mountains are impassable.")]
        public bool Passable = true;

        [Tooltip("Extra action points required to step onto this terrain, on top of the base move cost.")]
        public int ExtraMoveCost;

        [Tooltip("How far the tile is raised visually, in world units.")]
        public float HeightOffset;

        [Tooltip("When enabled, the tile sprite is tinted with Color instead of keeping its default look.")]
        public bool OverrideColor;

        public Color Color = Color.white;

        [Tooltip("Optional sprite that replaces the default tile sprite for this terrain (e.g. a rocky tile for mountains). Leave empty to keep the prefab's sprite.")]
        public Sprite OverrideSprite;
    }
}
