using System.Collections.Generic;
using Data;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Where a box is in the life of a match. Three states rather than a bool, because a dropped box
    /// is made with the rest of them and then waits: it is neither on the board nor taken off it, and
    /// telling those two apart is what lets undo put a box back exactly where it was and the loot
    /// win condition wait for what has not been left behind yet.
    /// </summary>
    public enum LootboxState
    {
        /// <summary>Made, but not on the board yet - a drop waiting for a unit to fall.</summary>
        Pending,

        /// <summary>Lying on its tile, to be taken.</summary>
        InPlay,

        /// <summary>Taken. Kept, hidden, so undo can put it back on the tile it remembers.</summary>
        Taken
    }

    /// <summary>
    /// A box lying on the map, holding the one item it hands over when it is taken. What is inside is
    /// rolled when the box is made rather than when it is opened, so what a given board hands out
    /// stays the same no matter when the player walks over it - and so redo hands out what undo took
    /// away rather than rolling again.
    ///
    /// What it looks like, what it costs and what it may hold are its <see cref="Type"/>'s, so every
    /// kind of box shares this one component and this one prefab.
    ///
    /// Deliberately not clickable and not occupying its tile: it is picked up by standing on it (see
    /// <see cref="Runtime.Core.Spawning.LootSpawner.TryPickup"/>), so it needs no collider and must
    /// not block the way to itself.
    /// </summary>
    public class Lootbox : MonoBehaviour, ITooltipSource
    {
        /// <summary>What kind of box this is - the asset everything about it but its content is on.</summary>
        public LootboxType Type { get; private set; }

        /// <summary>
        /// Where the box lies, or null while it is still pending. Kept after it was taken, so undo
        /// can put it back on the tile it was taken from. A scattered box never leaves its first
        /// tile; a dropped one arrives on the tile a unit fell on, which is why this travels with
        /// the snapshot rather than being read once.
        /// </summary>
        public Tile Tile { get; private set; }

        public LootboxState State { get; private set; } = LootboxState.Pending;

        public Item Content { get; private set; }

        /// <summary>On the board and there to be taken.</summary>
        public bool IsInPlay => State == LootboxState.InPlay;

        /// <summary>Made but not put down yet - a drop still waiting for a unit to fall.</summary>
        public bool IsPending => State == LootboxState.Pending;

        /// <summary>Taken - as opposed to lying about *or* still waiting to be dropped.</summary>
        public bool IsTaken => State == LootboxState.Taken;

        /// <summary>What taking this box costs, which is its kind's business rather than its own.</summary>
        public int PickupCost => Type != null ? Type.PickupCost : 0;

        /// <summary>
        /// What taking it costs right now. Nothing at all while
        /// <see cref="GameRules.AutoCollectLootboxes"/> is on: a box is then had by stepping onto its
        /// tile, so pressing for the one already underfoot must not be the expensive way of doing
        /// what a step does for free. That is not a corner case - a box holding something there was
        /// no room for is left lying where it is, and no further arrival will pick it up, so pressing
        /// is the only way back to it once a slot has been freed.
        ///
        /// One query rather than a number read twice: what the pickup charges and what the card
        /// labelling the box says it costs are the same call, so they cannot disagree.
        /// </summary>
        public int Cost => rules != null && rules.AutoCollectLootboxes ? 0 : PickupCost;

        /// <summary>
        /// What the card labelling this box says. A box says its tier and what taking it costs, and
        /// nothing about what is inside - that is the whole point of a box. A kind that
        /// <see cref="LootboxType.ShowsContent"/> is not a box: the find lies open, so it names the
        /// item and its category instead, exactly as it is drawn with the item's symbol.
        /// </summary>
        public TooltipContent Describe()
        {
            if (Type == null || !IsInPlay)
                return TooltipContent.Empty;

            bool open = Type.ShowsContent && Content != null;

            var stats = new List<string>();

            // Only when it costs something: with auto-collect on there is no number worth printing,
            // and a box is then had by walking over it.
            if (Cost > 0)
                stats.Add($"Cost {Cost} AP");

            return new TooltipContent(
                open ? Content.Title : Type.Title,
                open ? Item.NameOf(Content.Slot) : "Lootbox",
                stats: stats,
                icon: spriteRenderer != null ? spriteRenderer.sprite : null);
        }

        /// <summary>The top of the box, so a card labelling it hangs over the tile it lies on.</summary>
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

        private SpriteRenderer spriteRenderer;

        // Held live rather than read once, like everywhere else the rules are: auto-collect can be
        // switched during play, and what a box costs follows it.
        private GameRules rules;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Makes the box what its kind says it is and fills it. It is not on the board yet - that is
        /// <see cref="SetState"/> - so it starts hidden: a drop is made with the rest of them and
        /// must not be seen standing at the origin until a unit falls.
        ///
        /// The size is applied here beside the sprite and the sorting order, so what the shared
        /// prefab carries is replaced in one place rather than authored per prefab.
        /// </summary>
        public void Setup(LootboxType type, Item content, int orderInLayer, float scale, GameRules gameRules)
        {
            Type = type;
            Content = content;
            rules = gameRules;

            transform.localScale = Vector3.one * scale;

            if (spriteRenderer == null)
                return;

            spriteRenderer.sortingOrder = orderInLayer;
            spriteRenderer.enabled = false;

            var sprite = SpriteFor(type, content);

            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// What lies on the tile. Usually the kind's own sprite - the box, telling its tier and
        /// nothing about what is inside. A kind that <see cref="LootboxType.ShowsContent"/> is not a
        /// box at all: the find lies open, so it is drawn with the item's own symbol and can be read
        /// from across the map. An item with no symbol yet falls back to the sprite rather than
        /// leaving nothing on the tile.
        /// </summary>
        private static Sprite SpriteFor(LootboxType type, Item content)
        {
            if (type == null)
                return null;

            if (type.ShowsContent && content != null && content.Symbol != null)
                return content.Symbol;

            return type.Sprite;
        }

        /// <summary>
        /// Moves the box between the three states, and onto or off a tile with it. The two go
        /// together on purpose: a box that is in play is exactly a box some tile is showing, so there
        /// is no way to set one without the other.
        ///
        /// A taken box keeps the tile it was taken from - it is only hidden, never destroyed - so
        /// undo puts it back where it was rather than somewhere it never lay.
        /// </summary>
        public void SetState(LootboxState state, Tile tile)
        {
            // Whatever tile was showing this box stops: it may be leaving the board, or - being a
            // drop - arriving on a different tile than the one it last lay on.
            if (Tile != null && Tile.Lootbox == this)
                Tile.SetLootbox(null);

            State = state;
            Tile = tile;

            // The tile is what fog of war drives, so it is also what tells the box to show itself.
            if (state == LootboxState.InPlay && tile != null)
                tile.SetLootbox(this);
            else if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Shows or hides the box and tints it like the ground it lies on, so a box on remembered
        /// ground fades with it instead of shining out of the fog. Driven by the tile.
        /// </summary>
        public void SetVisibility(bool revealed, Color tint)
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.enabled = revealed && IsInPlay;
            spriteRenderer.color = tint;
        }
    }
}
