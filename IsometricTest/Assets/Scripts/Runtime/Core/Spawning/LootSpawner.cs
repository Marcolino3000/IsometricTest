using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    /// <summary>
    /// Scatters lootboxes over the map once it has been generated and owns them, the way the other
    /// spawners own their tiles and units. Taking one happens here too: the boxes are its, so the
    /// inventory only has to be told what was found.
    /// </summary>
    public class LootSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LootSpawnerSettings settings;

        [Header("Debug")]
        [SerializeField] private List<Lootbox> lootboxes = new();

        // Boxes that have been taken. Kept (hidden) rather than destroyed so undo can put them back,
        // the same way UnitSpawner keeps a fallen unit; a respawn clears them out for good.
        [SerializeField] private List<Lootbox> takenLootboxes = new();

        // One bag per category, so each category's box count can be honoured on its own. Each is
        // dealt from a shuffled copy and only refilled once it runs out: rolling every box
        // independently would keep handing out the same one out of a handful.
        private readonly List<Item>[] bags = new List<Item>[(int)SlotKind.None];

        private TileSpawner tileSpawner;
        private UnitSpawner unitSpawner;
        private ItemManager itemManager;
        private GameStateManager gameStateManager;

        public IReadOnlyList<Lootbox> AllLootboxes => lootboxes;

        /// <summary>Every box of this match, lying about or taken. Used to snapshot the board.</summary>
        public IEnumerable<Lootbox> AllSpawnedLootboxes => lootboxes.Concat(takenLootboxes);

        /// <summary>
        /// Takes the box the player's character is standing on. A turn action like any other: it costs
        /// <see cref="LootSpawnerSettings.PickupCost"/> action points, so it only works on the
        /// character's own turn and only while it can still afford it, and it announces itself so the
        /// history can undo it.
        /// </summary>
        public void TryPickup()
        {
            // A removed unit is only hidden, so IsAlive is the in-play test rather than a null check.
            var unit = unitSpawner.PlayerUnit;

            if (!unit.IsAlive || unit.CurrentState.Team != gameStateManager.State.Team)
                return;

            var lootbox = unit.CurrentState.Position.Lootbox;

            // The two conditions of the action: something to take, and the points to take it with.
            if (lootbox == null || unit.CurrentState.ActionPoints < settings.PickupCost)
                return;

            itemManager.Pickup(lootbox.Content);
            TakeLootbox(lootbox);

            unit.CurrentState.ActionPoints -= settings.PickupCost;

            ActionReporter.Report(ActionReport.Pickup(unit));
        }

        /// <summary>
        /// Takes a box off the board. It is kept aside rather than destroyed - see
        /// <see cref="Lootbox.IsInPlay"/> - so undo can put it back. Does nothing for one already gone.
        /// </summary>
        public void TakeLootbox(Lootbox lootbox)
        {
            if (!lootboxes.Remove(lootbox))
                return;

            takenLootboxes.Add(lootbox);
            lootbox.SetInPlay(false);
        }

        /// <summary>Puts a previously taken box back. Does nothing for one still lying about.</summary>
        public void RestoreLootbox(Lootbox lootbox)
        {
            if (!takenLootboxes.Remove(lootbox))
                return;

            lootboxes.Add(lootbox);
            lootbox.SetInPlay(true);
        }

        #region Helpers

        private void ClearLootboxes()
        {
            foreach (var lootbox in AllSpawnedLootboxes.ToList())
            {
                if (lootbox == null)
                    continue;

                // A respawn rebuilds the grid first, so the tile a box remembers may already be gone.
                if (lootbox.Tile != null)
                    lootbox.Tile.SetLootbox(null);

                Destroy(lootbox.gameObject);
            }

            lootboxes.Clear();
            takenLootboxes.Clear();

            foreach (var bag in bags)
                bag?.Clear();
        }

        private void SpawnLootbox(Tile tile, Item content)
        {
            var position = tileSpawner.GridIndexToWorldPosition(tile.Position) + Vector3.up * tile.HeightOffset + new Vector3(0, 0.25f, 0);

            var lootbox = Instantiate(settings.LootboxPrefab, position, Quaternion.identity, transform);
            lootbox.name = $"Lootbox {tile.Position.x}-{tile.Position.y}";
            lootbox.Setup(tile, content, settings.OrderInLayer);

            lootboxes.Add(lootbox);
        }

        /// <summary>
        /// The tiles a box may lie on, in randomized order: walkable ground with nobody standing on
        /// it. Impassable terrain is ruled out because a box there could never be reached, and an
        /// occupied tile because the unit spawned there would be standing on free loot.
        /// </summary>
        private List<Tile> GetShuffledLootTiles()
        {
            var candidates = new List<Tile>();

            foreach (var tile in tileSpawner.AllTiles)
            {
                if (tile.IsPassable && !tile.IsOccupied)
                    candidates.Add(tile);
            }

            Shuffle(candidates);

            return candidates;
        }

        private static void Shuffle<T>(List<T> list)
        {
            // Fisher-Yates
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The bag <paramref name="kind"/> is dealt from, created on first use.</summary>
        private List<Item> BagFor(SlotKind kind)
        {
            return bags[(int)kind] ??= new List<Item>();
        }

        /// <summary>The next item of <paramref name="kind"/>, or null if none is authored at all.</summary>
        private Item TakeItem(SlotKind kind)
        {
            var bag = BagFor(kind);

            if (bag.Count == 0)
                RefillBag(bag, kind);

            if (bag.Count == 0)
                return null;

            var index = bag.Count - 1;
            var item = bag[index];
            bag.RemoveAt(index);

            return item;
        }

        private void RefillBag(List<Item> bag, SlotKind kind)
        {
            if (settings.Items == null)
                return;

            foreach (var item in settings.Items)
            {
                if (item != null && item.Slot == kind)
                    bag.Add(item);
            }

            Shuffle(bag);
        }

        #endregion

        #region Setup

        public void Setup(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, ItemManager itemManagerArg,
            GameStateManager gameStateManagerArg, InputHandler inputHandler)
        {
            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            itemManager = itemManagerArg;
            gameStateManager = gameStateManagerArg;

            inputHandler.InteractPressed += TryPickup;
            inputHandler.RightClicked += TryPickup;
        }

        /// <summary>
        /// Scatters a fresh set of boxes. Runs after the units are placed so no box ends up under
        /// someone, which is why the Initiator calls it last in its spawning step.
        /// </summary>
        [ContextMenu("Spawn Lootboxes")]
        public void SpawnLootboxes()
        {
            ClearLootboxes();

            if (settings == null || settings.LootboxPrefab == null)
            {
                Debug.LogWarning($"No lootbox prefab set in {nameof(LootSpawnerSettings)}.", settings);
                return;
            }

            if (settings.Items == null || settings.Items.Count == 0)
            {
                Debug.LogWarning($"No items to fill lootboxes with in {nameof(LootSpawnerSettings)}.", settings);
                return;
            }

            var tiles = GetShuffledLootTiles();
            var placed = 0;

            // One category at a time, so each gets the number of boxes it was asked for. The tiles
            // were shuffled beforehand, so handing them out in order still scatters the categories.
            for (var kind = 0; kind < (int)SlotKind.None; kind++)
            {
                var wanted = settings.CountFor((SlotKind)kind);

                for (var i = 0; i < wanted; i++)
                {
                    if (placed >= tiles.Count)
                        return;

                    var content = TakeItem((SlotKind)kind);

                    // Nothing of this category is authored, so no further box of it can be filled.
                    if (content == null)
                    {
                        Debug.LogWarning($"{nameof(LootSpawnerSettings)} asks for {wanted} " +
                                         $"{(SlotKind)kind} box(es) but lists no item of that category.", settings);
                        break;
                    }

                    SpawnLootbox(tiles[placed++], content);
                }
            }
        }

        #endregion
    }
}
