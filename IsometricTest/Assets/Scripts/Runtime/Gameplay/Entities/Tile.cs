using System;
using System.Collections.Generic;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.Items;
using Runtime.Gameplay.Traits;
using TMPro;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class Tile : MonoBehaviour, IClickable, ITooltipSource
    {
        public Vector2Int Position;
        public bool IsOccupied {get; private set;}

        /// <summary>
        /// The unit standing here, or null. Exposed like <see cref="Lootbox"/> is, so anything
        /// spatial that has to reach the occupant - an area effect asking who is caught - finds it
        /// in one step instead of scanning every unit for a matching position.
        /// </summary>
        public Unit Unit => unit;

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

        // What this tile is, and what unscouted ground is drawn as. Both are held because the look is
        // re-applied whenever the fog conceals or gives away the terrain; the unknown one is the flat
        // profile the spawner hands over, so a disguised tile looks exactly like plain ground rather
        // than merely like the bare prefab.
        private TerrainProfile terrainProfile;
        private TerrainProfile unknownTerrainProfile;
        private bool terrainConcealed;

        // The prefab's own look, which a profile overriding neither falls back to.
        private Sprite defaultSprite;
        private Color defaultColor = Color.white;

        // How far the tile is currently raised. Tracked rather than remembering the unraised position,
        // so the height can be taken back off without depending on when the grid position was written.
        private float appliedHeightOffset;

        // The debug label naming this tile's grid position. Found rather than serialized - it is the
        // only text on the prefab - and held, because once switched off a lookup would no longer
        // find it.
        private TextMeshPro coordinateLabel;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            coordinateLabel = GetComponentInChildren<TextMeshPro>(true);

            if (spriteRenderer != null)
            {
                defaultSprite = spriteRenderer.sprite;
                defaultColor = spriteRenderer.color;
            }
        }

        /// <summary>
        /// Writes this tile's grid position onto it, or takes the label away. Switched by
        /// <see cref="Global.GameRules.ShowTileCoordinates"/> and pushed by the spawner, so it can be
        /// flipped mid-play; the text is rewritten each time rather than at spawn, since a tile that
        /// spawned with the label off has never had one written.
        /// </summary>
        /// <summary>
        /// What the card labelling this tile says: what the ground is, what it costs to cross, what
        /// it grants whoever stands on it, and what lies on it.
        ///
        /// It says what the player is allowed to know, not what the tile is. Unscouted ground is
        /// drawn as flat and describes itself as unexplored, so the card cannot give away the
        /// mountain the fog is hiding - the real values are kept here either way, since pathfinding
        /// and sight read them regardless of what is drawn.
        /// </summary>
        public TooltipContent Describe()
        {
            if (Visibility == TileVisibility.Hidden)
                return new TooltipContent("Unexplored");

            var stats = new List<string>();

            if (!IsPassable)
                stats.Add("Impassable");

            if (ExtraMoveCost > 0)
                stats.Add($"Move cost +{ExtraMoveCost}");
            //
            // if (Elevation > 0)
            //     stats.Add($"Elevation {Elevation} - blocks the sight of anyone standing lower");

            var entries = new List<Capability>();

            foreach (var trait in Traits)
            {
                if (trait != null)
                    entries.Add(new Capability(trait.Icon, trait.name,""));
            }

            // The box is folded in rather than picked out of the world of its own: it does not occupy
            // its tile and has no collider, so the ground it lies on is what the cursor reaches.
            if (Lootbox != null && Lootbox.IsInPlay)
            {
                var box = Lootbox.Describe();

                if (!box.IsEmpty)
                    entries.Add(box.AsEntry());
            }

            return new TooltipContent(
                TerrainNames.Of(Terrain),
                // Remembered ground is still true - terrain does not move - but what stands on it is
                // whatever was last seen, so the card says which of the two it is.
                Visibility == TileVisibility.Explored ? "Remembered" : null,
                stats: stats,
                entries: entries);
        }

        /// <summary>The top of the tile sprite, so a card labelling it hangs over the ground.</summary>
        public Vector3 TooltipPoint
        {
            get
            {
                if (spriteRenderer == null)
                    return transform.position;

                Bounds bounds = spriteRenderer.bounds;

                return new Vector3(bounds.center.x, bounds.max.y, transform.position.z);
            }
        }

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
        /// visually by the profile's height offset and optionally tints the tile sprite. The rules
        /// are always this tile's own; <paramref name="unknownProfile"/> is merely how it is drawn
        /// while the fog is hiding what it is - see <see cref="SetVisibility"/>.
        /// </summary>
        public void ApplyTerrain(TerrainProfile profile, TerrainProfile unknownProfile = null)
        {
            if (profile == null)
                return;

            terrainProfile = profile;
            unknownTerrainProfile = unknownProfile;

            Terrain = profile.Type;
            IsPassable = profile.Passable;
            ExtraMoveCost = profile.ExtraMoveCost;
            HeightOffset = profile.HeightOffset;
            Elevation = profile.Elevation;
            Traits = profile.Traits ?? (IReadOnlyList<TerrainTrait>)Array.Empty<TerrainTrait>();

            ApplyLook();
        }

        /// <summary>
        /// Applies a fog state to the tile: tints the terrain sprite, disguises unscouted ground as
        /// plain terrain when <paramref name="hideUnknownTerrain"/> asks for it, and hides the tile
        /// marker (e.g. the "occupied" highlight) unless the tile is currently visible, so enemy
        /// positions don't leak through fog.
        /// </summary>
        public void SetVisibility(TileVisibility visibility, Color exploredTint, Color hiddenTint,
            bool hideUnknownTerrain = false)
        {
            Visibility = visibility;

            fogTint = visibility switch
            {
                TileVisibility.Visible => Color.white,
                TileVisibility.Explored => exploredTint,
                _ => hiddenTint
            };

            // Only ground nobody has seen is disguised. Remembered ground keeps its own look, the way
            // a lootbox stays on it: terrain does not move, so what was seen there is still true.
            terrainConcealed = hideUnknownTerrain && visibility == TileVisibility.Hidden;

            ApplyLook();
            RefreshMarker();
            RefreshLootbox();
        }

        /// <summary>
        /// Draws the tile as whichever terrain it is currently showing - its own, or the plain ground
        /// unscouted tiles are disguised as - and tints the result with the fog. Sprite, colour and
        /// height go together: a mountain gives itself away by its silhouette as surely as by its rock.
        /// Only the look moves; <see cref="HeightOffset"/> and every rule stay this tile's own, so what
        /// stands on it is still placed on top of the real terrain.
        /// </summary>
        private void ApplyLook()
        {
            var profile = terrainConcealed ? unknownTerrainProfile : terrainProfile;

            // Adjusted by the difference rather than written outright, so the grid position the tile
            // was spawned at is never assumed to be known here.
            var height = profile != null ? profile.HeightOffset : 0f;
            transform.position += Vector3.up * (height - appliedHeightOffset);
            appliedHeightOffset = height;

            if (spriteRenderer == null)
                return;

            spriteRenderer.sprite = profile != null && profile.OverrideSprite != null
                ? profile.OverrideSprite
                : defaultSprite;

            // Remembered so fog tinting can multiply against the lit colour (and restore it later).
            baseTerrainColor = profile != null && profile.OverrideColor ? profile.Color : defaultColor;
            spriteRenderer.color = baseTerrainColor * fogTint;
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