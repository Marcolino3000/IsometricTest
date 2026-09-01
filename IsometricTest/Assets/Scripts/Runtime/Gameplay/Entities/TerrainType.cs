using System;
using System.Collections.Generic;
using Runtime.Gameplay.Traits;
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
    /// What a kind of terrain is called to the player - what the card labelling a tile is headed
    /// with. Kept beside the enum it reads, the way <c>Item.NameOf</c> sits beside the slot kinds.
    /// </summary>
    public static class TerrainNames
    {
        public static string Of(TerrainType type)
        {
            return type switch
            {
                TerrainType.Flat => "Flat Ground",
                TerrainType.Hills => "Hills",
                TerrainType.Mountain => "Mountain",
                _ => type.ToString()
            };
        }
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

        [Tooltip("Whether units are allowed to move onto this terrain.")]
        public bool Passable = true;

        [Tooltip("Extra action points required to step onto this terrain, on top of the base move cost.")]
        public int ExtraMoveCost;

        [Tooltip("How far the tile is raised visually, in world units.")]
        public float HeightOffset;

        [Tooltip("How high this terrain stands, as a rule rather than a look: sight is stopped by " +
                 "terrain standing higher than the viewer, so a unit on flat ground sees nothing " +
                 "past a hill while one standing on a hill does. Kept separate from Height Offset " +
                 "so raising a tile visually never changes what can be seen over it.")]
        public int Elevation;

        [Tooltip("When enabled, the tile sprite is tinted with Color instead of keeping its default look.")]
        public bool OverrideColor;

        public Color Color = Color.white;

        [Tooltip("Optional sprite that replaces the default tile sprite for this terrain (e.g. a rocky tile for mountains). Leave empty to keep the prefab's sprite.")]
        public Sprite OverrideSprite;

        [Tooltip("Traits granted to whichever unit is occupying this terrain (e.g. extra defence or range on hills). Drag terrain trait assets here.")]
        public List<TerrainTrait> Traits = new();
    }
}
