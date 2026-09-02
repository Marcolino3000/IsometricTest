using System.Collections.Generic;
using Data;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    /// <summary>
    /// Draws the line where one zone ends and the next begins. One short sprite per tile edge whose
    /// two sides fall in different rings, laid on the seam between them rather than on either tile:
    /// a zone is a distance, so what marks it belongs between the ground it divides and must take up
    /// no tile of its own.
    ///
    /// Built from code like the attack preview, so the Systems prefab needs no further object, and
    /// rebuilt whenever the board is - a restart lays out fresh terrain, and the rings are measured
    /// against the grid.
    ///
    /// <b>A wall runs along the seam, and every piece of it stands straight up.</b> Nothing here is
    /// rotated: fire rises on the screen, it does not lean with the ground, so the slope of a seam
    /// comes from <em>where</em> the pieces are put - several narrow ones stepping along the line
    /// (<see cref="PiecesPerSeam"/>) - rather than from turning them. One wide piece would lie
    /// across both tiles instead of on the line they share.
    ///
    /// <b>Which look is drawn is whether art was authored.</b> A
    /// <see cref="ZoneSettings.BorderSprite"/> is a strip - a wall of flame - cut into pieces the
    /// width of their share of a seam: cut rather than stretched, so its pixels stay the size they
    /// were drawn, and cut into many so the wall does not repeat itself along a ring. With nothing
    /// authored a line is generated instead and laid flat on the seam - two sprites there, one per
    /// direction, since that one <em>is</em> drawn along the 2:1 stair, inside its own texture, and
    /// a mirrored stair is not a rotated one.
    ///
    /// <b>The tiles are drawn over it.</b> A piece stands on the bare grid position of its seam and
    /// hangs its art above that (<see cref="Lift"/>, a pivot below the sprite), so the board sorts
    /// it along Y where it actually stands - between the two tiles it divides. The tile in front
    /// therefore covers the foot of the flames instead of the flames covering the tile.
    ///
    /// <b>It follows the fog.</b> A seam between two tiles nobody has scouted is not drawn at all,
    /// and one only remembered is dimmed, exactly as the ground it lies on is: the rings are worth
    /// knowing about, but a border on ground the player has never seen would map the world for them.
    /// </summary>
    public class ZoneBorder : MonoBehaviour
    {
        /// <summary>The core of the generated line, tinted by the zone it marks.</summary>
        private static readonly Color32 LineColor = new(255, 255, 255, 255);

        /// <summary>The pixel above and below it, so the line reads against pale ground as well.</summary>
        private static readonly Color32 OutlineColor = new(0, 0, 0, 140);

        /// <summary>
        /// How much of its colour a border keeps on ground that is only remembered - the same idea
        /// as the fog's tint on the tiles themselves, so a seam does not shine out of the dark.
        /// </summary>
        private const float RememberedDim = 0.55f;

        /// <summary>
        /// How much wider than its share of a seam a piece is cut, so neighbouring pieces close the
        /// step between them instead of leaving a notch.
        /// </summary>
        private const float StandingOverlap = 1.3f;

        /// <summary>
        /// How many pieces a seam is built from. A piece stands straight up - fire rises on the
        /// screen, it does not lean with the ground - so the slope of the seam has to come from
        /// where the pieces are put rather than from turning them: several narrow ones stepping
        /// along the line read as a wall on it, while one wide one would lie across both tiles.
        /// </summary>
        private const int PiecesPerSeam = 4;

        /// <summary>
        /// One drawn seam: the piece itself and the two tiles it divides, which is what the fog is
        /// asked about. Kept together because both answers are needed on every fog pass.
        /// </summary>
        private readonly struct Edge
        {
            public readonly GameObject Object;
            public readonly SpriteRenderer Renderer;
            public readonly Tile A;
            public readonly Tile B;
            public readonly Color Color;

            public Edge(GameObject obj, SpriteRenderer renderer, Tile a, Tile b, Color color)
            {
                Object = obj;
                Renderer = renderer;
                A = a;
                B = b;
                Color = color;
            }
        }

        private TileSpawner tileSpawner;
        private GameRules rules;
        private FogOfWar fogOfWar;
        private ZoneSettings zoneSettings;

        private readonly List<Edge> edges = new();

        // Generated once per rebuild and dropped with it, like a cut icon sprite: made at runtime,
        // so they must never be written into an asset. The standing pieces are cut from authored
        // art and carry no texture of their own - see ReleaseSprites.
        private Sprite risingSprite;
        private Sprite fallingSprite;
        private Texture2D risingTexture;
        private Texture2D fallingTexture;

        private readonly List<Sprite> standingSprites = new();

        /// <summary>
        /// Puts the border on an object of its own. The rules go in live like everywhere else, so
        /// <see cref="GameRules.ShowZoneBorders"/> can be flipped during play, and the fog is what
        /// says when a seam has been scouted.
        /// </summary>
        public static ZoneBorder Create(TileSpawner tiles, GameRules gameRules, FogOfWar fog)
        {
            var border = new GameObject(nameof(ZoneBorder)).AddComponent<ZoneBorder>();

            border.Setup(tiles, gameRules, fog);

            return border;
        }

        public void Setup(TileSpawner tiles, GameRules gameRules, FogOfWar fog)
        {
            tileSpawner = tiles;
            rules = gameRules;
            fogOfWar = fog;
            zoneSettings = ZoneRules.Settings;

            if (rules != null)
                rules.Changed += ApplyVisibility;

            // The rings themselves are authored on a settings asset that says when it has been
            // edited, so a radius moved in the inspector redraws the lines instead of being polled
            // for drift.
            if (zoneSettings != null)
                zoneSettings.Changed += Rebuild;

            // Pushed at rather than polled, like everything else the fog decides: a seam is drawn
            // once the ground on either side of it has been seen.
            if (fogOfWar != null)
                fogOfWar.Recomputed += ApplyFog;

            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (rules != null)
                rules.Changed -= ApplyVisibility;

            if (zoneSettings != null)
                zoneSettings.Changed -= Rebuild;

            if (fogOfWar != null)
                fogOfWar.Recomputed -= ApplyFog;

            ReleaseSprites();
        }

        /// <summary>
        /// Lays the seams out afresh for the board as it now stands. Called whenever the grid is
        /// built, since the rings are measured against it.
        /// </summary>
        public void Rebuild()
        {
            Clear();

            if (tileSpawner == null || ZoneRules.Count <= 1)
                return;

            BuildSprites();

            var half = tileSpawner.HalfTileSize;

            foreach (var tile in tileSpawner.AllTiles)
            {
                if (tile == null)
                    continue;

                var zone = ZoneRules.IndexAt(tile.Position);

                // The tile's own place on the grid, with nothing added: what is drawn is lifted
                // onto the surface by its pivot instead (see PivotFor), so an object stands where
                // the board sorts it - between the two tiles it divides - while its art stands on
                // the ground they share. The line is never raised with hills either, so a border
                // runs level across ground that steps up and down and gives away nothing about
                // terrain the fog is still hiding.
                var center = tileSpawner.GridIndexToWorldPosition(tile.Position);

                // The corner the two seams share, and the corner each of them runs to. Only the
                // edges towards the neighbours ahead of this tile, so a seam between two tiles is
                // drawn once rather than from each side.
                var right = center + new Vector3(half.x, 0f);

                AddEdgeIfDivided(tile, zone, Vector2Int.right, right, center + new Vector3(0f, -half.y), true);
                AddEdgeIfDivided(tile, zone, Vector2Int.up, right, center + new Vector3(0f, half.y), false);
            }

            ApplyFog();
        }

        /// <summary>
        /// Puts a piece on the seam towards <paramref name="direction"/> when the tile there lies in
        /// another ring. The seam wears the colour of the ring being entered - the outer of the two
        /// - since it marks the danger ahead rather than the ground behind.
        /// </summary>
        private void AddEdgeIfDivided(Tile tile, int zone, Vector2Int direction, Vector3 from, Vector3 to,
            bool rising)
        {
            var neighbour = tileSpawner.GetTileAtPosition(tile.Position + direction);

            if (neighbour == null)
                return;

            var neighbourZone = ZoneRules.IndexAt(neighbour.Position);

            if (neighbourZone == zone)
                return;

            var color = ColorOf(Mathf.Max(zone, neighbourZone));

            // The generated line is one piece: it is drawn along the stair inside its own texture,
            // so it already follows the seam and belongs at its middle.
            if (standingSprites.Count == 0)
            {
                AddSegment(Vector3.Lerp(from, to, 0.5f), rising ? risingSprite : fallingSprite, color,
                    tile, neighbour);
                return;
            }

            // A standing wall is built from several pieces set along the line, each straight up.
            for (var i = 0; i < PiecesPerSeam; i++)
            {
                var along = (i + 0.5f) / PiecesPerSeam;

                AddSegment(Vector3.Lerp(from, to, along), SpriteFor(rising), color, tile, neighbour);
            }
        }

        /// <summary>
        /// What one seam is drawn with: a piece of the authored strip - a different one each time,
        /// so a wall does not repeat every half tile - or the generated line for its direction.
        /// </summary>
        private Sprite SpriteFor(bool rising)
        {
            if (standingSprites.Count > 0)
                return standingSprites[Random.Range(0, standingSprites.Count)];

            return rising ? risingSprite : fallingSprite;
        }

        /// <summary>
        /// How far above the bare grid a border is drawn: onto the surface the tiles are seen at,
        /// less however far its foot is meant to sit in the ground.
        /// </summary>
        private float Lift()
        {
            var sink = zoneSettings != null ? zoneSettings.BorderSink : 0f;

            return tileSpawner.SurfaceOffset - sink;
        }

        private Color ColorOf(int zoneIndex)
        {
            var zone = ZoneRules.Settings != null ? ZoneRules.Settings.At(zoneIndex) : null;
            var color = zone != null ? zone.Color : Color.white;

            color.a *= zoneSettings != null ? zoneSettings.BorderOpacity : 1f;

            return color;
        }

        private void AddSegment(Vector3 position, Sprite sprite, Color color, Tile a, Tile b)
        {
            if (sprite == null)
                return;

            var segment = new GameObject("ZoneEdge");
            segment.transform.SetParent(transform, false);
            segment.transform.position = position;

            var renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = zoneSettings != null ? zoneSettings.OrderInLayer : 0;

            // Sorted by where it stands, not by the middle of its art: a piece is drawn well above
            // its own place on the ground, and the board sorts along Y - taking the middle of a
            // flame would put it a whole tile further back than the seam it stands on.
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            edges.Add(new Edge(segment, renderer, a, b, color));
        }

        /// <summary>
        /// Draws each seam as much as the fog allows: not at all while neither side has been
        /// scouted, dimmed while both are only remembered, and in full where the ground is in sight.
        /// </summary>
        private void ApplyFog()
        {
            foreach (var edge in edges)
            {
                if (edge.Renderer == null)
                    continue;

                var seen = IsExplored(edge.A) || IsExplored(edge.B);

                edge.Renderer.enabled = seen;

                if (!seen)
                    continue;

                var lit = IsVisible(edge.A) || IsVisible(edge.B);
                var color = edge.Color;

                if (!lit)
                    color *= new Color(RememberedDim, RememberedDim, RememberedDim, 1f);

                edge.Renderer.color = color;
            }
        }

        private static bool IsExplored(Tile tile)
        {
            return tile != null && tile.Visibility != TileVisibility.Hidden;
        }

        private static bool IsVisible(Tile tile)
        {
            return tile != null && tile.Visibility == TileVisibility.Visible;
        }

        private void Clear()
        {
            foreach (var edge in edges)
            {
                if (edge.Object == null)
                    continue;

                // Switched off before it is dropped: Destroy takes effect at the end of the frame,
                // and the sprites these are drawn with are released right below.
                edge.Object.SetActive(false);
                Destroy(edge.Object);
            }

            edges.Clear();
            ReleaseSprites();
        }

        /// <summary>Hides the seams rather than dropping them, so the switch costs no rebuild.</summary>
        private void ApplyVisibility()
        {
            gameObject.SetActive(rules == null || rules.ShowZoneBorders);
        }

        #region Sprites

        /// <summary>
        /// What the seams are drawn with: pieces cut from the authored strip, else a line generated
        /// per direction. Both are measured against the board's own geometry, so a piece is exactly
        /// as wide as the seam it stands on however the tiles are sized.
        /// </summary>
        private void BuildSprites()
        {
            var half = tileSpawner.HalfTileSize;

            if (half.x <= 0f || half.y <= 0f)
                return;

            if (BuildStandingSprites(half))
                return;

            var width = Mathf.Max(4, zoneSettings != null ? zoneSettings.BorderPixelWidth : 16);
            var thickness = Mathf.Max(1, zoneSettings != null ? zoneSettings.BorderThickness : 2);

            // The stair the line climbs is the tiles' own: half a tile across is half a tile of
            // height in the same proportion, so a board drawn in some other ratio than 2:1 gets a
            // line at its ratio rather than one crossing its edges.
            var climb = Mathf.Max(1, Mathf.RoundToInt(width * half.y / half.x));

            // Pixels per unit measured off the edge: the drawn line is exactly as wide as the half
            // tile it lies on, so nothing has to be scaled into place afterwards.
            var pixelsPerUnit = width / half.x;

            // Compared rather than coalesced: an unassigned reference to a Unity object is not the
            // CLR's null, so ??= would take one for a sprite already in hand and draw nothing.
            // The line lies flat on the seam rather than standing on it, so it hangs from its
            // middle - but from the same place on the ground, for the same sorting.
            var pivotY = 0.5f - Lift() * pixelsPerUnit / (climb + thickness + 1);

            if (risingSprite == null)
                risingSprite = CreateEdgeSprite(width, climb, thickness, pixelsPerUnit, pivotY, true,
                    out risingTexture);

            if (fallingSprite == null)
                fallingSprite = CreateEdgeSprite(width, climb, thickness, pixelsPerUnit, pivotY, false,
                    out fallingTexture);
        }

        /// <summary>
        /// Cuts the authored strip into upright pieces, each as wide as a seam, and reports whether
        /// there was one to cut. The strip's own pixels decide the scale: its height is drawn at
        /// <see cref="ZoneSettings.BorderHeight"/>, and a piece is cut to whatever width that makes
        /// a half tile - so nothing is ever stretched and the art keeps the size it was drawn at.
        /// <para>
        /// Several pieces rather than one, cut side by side along the strip, so a wall of flame
        /// standing along a whole ring does not repeat itself every half tile.
        /// </para>
        /// </summary>
        private bool BuildStandingSprites(Vector2 half)
        {
            var strip = zoneSettings != null ? zoneSettings.BorderSprite : null;

            if (strip == null || strip.texture == null)
                return false;

            var source = strip.textureRect;
            var height = zoneSettings.BorderHeight;

            if (source.width < 1f || source.height < 1f || height <= 0f)
                return false;

            var pixelsPerUnit = source.height / height;

            // A piece covers its share of the seam's width on screen, a little over so the steps
            // between them close up. It is cut to that, never scaled to it: the art keeps the pixel
            // size it was drawn at.
            var share = half.x / PiecesPerSeam * StandingOverlap;
            var pieceWidth = Mathf.Max(1f, Mathf.Round(share * pixelsPerUnit));
            var pieces = Mathf.Max(1, Mathf.FloorToInt(source.width / pieceWidth));

            // Below the art, so the piece hangs above where it stands: the object sits on the bare
            // grid, where the board sorts it against the tiles, and its flame is drawn up on the
            // surface those tiles are seen at.
            var pivot = new Vector2(0.5f, -Lift() * pixelsPerUnit / source.height);

            for (var i = 0; i < pieces; i++)
            {
                var rect = new Rect(source.x + i * pieceWidth, source.y, pieceWidth, source.height);

                var piece = Sprite.Create(strip.texture, rect, pivot, pixelsPerUnit);

                piece.name = $"{strip.name} {i + 1}";
                piece.hideFlags = HideFlags.HideAndDontSave;

                standingSprites.Add(piece);
            }

            return standingSprites.Count > 0;
        }

        /// <summary>
        /// Draws one direction of the line: a <paramref name="thickness"/>-pixel core climbing
        /// <paramref name="climb"/> pixels over <paramref name="width"/>, with a darker pixel above
        /// and below it. White, so the zone's own colour is what tints it.
        /// </summary>
        private static Sprite CreateEdgeSprite(int width, int climb, int thickness, float pixelsPerUnit,
            float pivotY, bool rising, out Texture2D texture)
        {
            // One row above the core and one below it, which is what centres the drawn line in the
            // texture and lets the sprite hang from its middle onto the seam.
            var height = climb + thickness + 1;
            var pixels = new Color32[width * height];

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = rising ? "ZoneEdgeRising" : "ZoneEdgeFalling"
            };

            for (var x = 0; x < width; x++)
            {
                var step = width > 1 ? Mathf.RoundToInt((float)x * (climb - 1) / (width - 1)) : 0;
                var bottom = 1 + step;

                Plot(pixels, width, height, x, bottom - 1, rising, OutlineColor);
                Plot(pixels, width, height, x, bottom + thickness, rising, OutlineColor);

                for (var t = 0; t < thickness; t++)
                    Plot(pixels, width, height, x, bottom + t, rising, LineColor);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, pivotY),
                pixelsPerUnit);

            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }

        /// <summary>
        /// Writes one pixel, mirrored top to bottom for the falling direction - the two edges of a
        /// tile meet at its corner, so one is the other upside down.
        /// </summary>
        private static void Plot(Color32[] pixels, int width, int height, int x, int y, bool rising, Color32 color)
        {
            var row = rising ? y : height - 1 - y;

            if (row < 0 || row >= height)
                return;

            pixels[row * width + x] = color;
        }

        private void ReleaseSprites()
        {
            Release(ref risingSprite, ref risingTexture);
            Release(ref fallingSprite, ref fallingTexture);

            // Cut from authored art, so there is a sprite to drop but no texture: the strip belongs
            // to the asset it was cut from and is not this object's to destroy.
            foreach (var piece in standingSprites)
                Drop(piece);

            standingSprites.Clear();
        }

        /// <summary>
        /// Drops a generated sprite and the texture it was drawn on, leaving an authored one alone -
        /// a sprite it did not make is not its to destroy, which is what the texture says: only a
        /// generated line has one.
        /// </summary>
        private static void Release(ref Sprite sprite, ref Texture2D texture)
        {
            if (texture != null)
            {
                Drop(sprite);
                Drop(texture);
            }

            sprite = null;
            texture = null;
        }

        /// <summary>
        /// Destroys something made at runtime, in whichever way the moment allows - a rebuild can
        /// be asked for from the inspector, where <c>Destroy</c> does nothing but complain.
        /// </summary>
        private static void Drop(Object made)
        {
            if (made == null)
                return;

            if (Application.isPlaying)
                Destroy(made);
            else
                DestroyImmediate(made);
        }

        #endregion
    }
}
