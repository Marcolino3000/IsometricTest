using Actions;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// A box lying on the map, holding the one item it hands over when it is taken. What is inside is
    /// rolled when the box is placed rather than when it is opened, so what a given board hands out
    /// stays the same no matter when the player walks over it.
    ///
    /// Deliberately not clickable and not occupying its tile: it is picked up by standing on it (see
    /// <see cref="Runtime.Core.Spawning.LootSpawner.TryPickup"/>), so it needs no collider and must
    /// not block the way to itself.
    /// </summary>
    public class Lootbox : MonoBehaviour
    {
        /// <summary>Where the box lies. Set once when it is placed, and kept after it was taken.</summary>
        public Tile Tile { get; private set; }

        /// <summary>
        /// False once the box has been taken. A taken box is kept - hidden, and off its tile - rather
        /// than destroyed, so undo can put it back with its contents intact, exactly as
        /// <see cref="Runtime.Core.Spawning.UnitSpawner.RemoveUnit"/> keeps a fallen unit around.
        /// </summary>
        public bool IsInPlay { get; private set; } = true;
        
        public AttackActionData Content { get; private set; }

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(Tile tile, AttackActionData content, int orderInLayer)
        {
            Tile = tile;
            Content = content;

            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = orderInLayer;

            // The tile is what fog of war drives, so it is also what tells the box to show itself.
            tile.SetLootbox(this);
        }

        /// <summary>
        /// Takes the box off the board or puts it back. Only its tile link and its visibility change -
        /// the box itself, and what is in it, survive so undo can restore both.
        /// </summary>
        public void SetInPlay(bool inPlay)
        {
            IsInPlay = inPlay;

            // The tile is what drives visibility, so going through it keeps the fog tint right.
            Tile.SetLootbox(inPlay ? this : null);

            if (!inPlay && spriteRenderer != null)
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
