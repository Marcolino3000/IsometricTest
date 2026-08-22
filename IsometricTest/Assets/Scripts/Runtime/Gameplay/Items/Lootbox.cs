using Data;
using Runtime.Gameplay.Entities;
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
    public class Lootbox : MonoBehaviour
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

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Makes the box what its kind says it is and fills it. It is not on the board yet - that is
        /// <see cref="SetState"/> - so it starts hidden: a drop is made with the rest of them and
        /// must not be seen standing at the origin until a unit falls.
        /// </summary>
        public void Setup(LootboxType type, Item content, int orderInLayer)
        {
            Type = type;
            Content = content;

            if (spriteRenderer == null)
                return;

            spriteRenderer.sortingOrder = orderInLayer;
            spriteRenderer.enabled = false;

            if (type != null && type.Sprite != null)
                spriteRenderer.sprite = type.Sprite;
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
