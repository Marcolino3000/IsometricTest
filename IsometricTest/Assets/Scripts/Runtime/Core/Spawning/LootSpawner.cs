using System.Collections.Generic;
using System.Linq;
using Actions;
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

        // Weapons are dealt from a shuffled copy of the pool and only refilled once it runs out:
        // rolling every box on its own would keep handing out the same one out of a handful.
        private readonly List<AttackActionData> weaponBag = new();

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
            weaponBag.Clear();
        }

        private void SpawnLootbox(Tile tile)
        {
            var position = tileSpawner.GridIndexToWorldPosition(tile.Position) + Vector3.up * tile.HeightOffset + new Vector3(0, 0.25f, 0);

            var lootbox = Instantiate(settings.LootboxPrefab, position, Quaternion.identity, transform);
            lootbox.name = $"Lootbox {tile.Position.x}-{tile.Position.y}";
            lootbox.Setup(tile, TakeWeapon(), settings.OrderInLayer);

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

            // Fisher-Yates shuffle
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates;
        }

        private AttackActionData TakeWeapon()
        {
            if (weaponBag.Count == 0)
                RefillWeaponBag();

            if (weaponBag.Count == 0)
                return null;

            var index = weaponBag.Count - 1;
            var weapon = weaponBag[index];
            weaponBag.RemoveAt(index);

            return weapon;
        }

        private void RefillWeaponBag()
        {
            if (settings.Weapons == null)
                return;

            foreach (var weapon in settings.Weapons)
            {
                if (weapon != null)
                    weaponBag.Add(weapon);
            }

            // Fisher-Yates shuffle
            for (var i = weaponBag.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (weaponBag[i], weaponBag[j]) = (weaponBag[j], weaponBag[i]);
            }
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

            if (settings.Weapons == null || settings.Weapons.Count == 0)
            {
                Debug.LogWarning($"No weapons to fill lootboxes with in {nameof(LootSpawnerSettings)}.", settings);
                return;
            }

            var tiles = GetShuffledLootTiles();
            var count = Mathf.Min(settings.LootboxCount, tiles.Count);

            for (var i = 0; i < count; i++)
                SpawnLootbox(tiles[i]);
        }

        #endregion
    }
}
