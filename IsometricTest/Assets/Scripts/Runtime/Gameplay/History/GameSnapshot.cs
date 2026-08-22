using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Runtime.Gameplay.History
{
    /// <summary>
    /// A complete picture of the match at one moment: where every unit stood, its vitals, whose turn
    /// it was and what each team had explored.
    ///
    /// Undo/redo restores these wholesale instead of replaying inverse actions. That matters for two
    /// reasons: outcomes that involved randomness (a critical hit) come back exactly as they were
    /// rather than being re-rolled, and any new mechanic that mutates unit or turn state is undoable
    /// without writing a matching "undo" for it.
    /// </summary>
    public sealed class GameSnapshot
    {
        public Team ActiveTeam;
        public bool UnitsHaveActionsLeft;

        public readonly List<UnitSnapshot> Units = new();

        // Where every box stood, and what the player owned at the time. Taking a box costs action
        // points, so it is a turn action and has to come back on undo - box and loot together, or
        // undoing one would hand out the same item twice. Using up an active item is a turn action
        // for the same reason, and it shortens this very list. A box a fallen enemy left behind
        // comes back the same way: undoing the kill puts the box back out of play with it.
        public readonly List<LootboxSnapshot> Lootboxes = new();
        public readonly List<Item> Items = new();

        // Explored ground is cumulative history - unlike visibility it cannot be recomputed from
        // where the units currently stand, so it has to travel with the snapshot.
        public readonly Dictionary<Team, HashSet<Vector2Int>> Explored = new();

        public static GameSnapshot Capture(UnitSpawner unitSpawner, GameStateManager gameStateManager,
            FogOfWar fogOfWar, LootSpawner lootSpawner, ItemManager itemManager)
        {
            var snapshot = new GameSnapshot
            {
                ActiveTeam = gameStateManager.State.Team,
                UnitsHaveActionsLeft = gameStateManager.State.UnitsHaveActionsLeft
            };

            // Removed units are included too (they are kept aside rather than destroyed), so undoing
            // the blow that killed one can put it back on the board.
            foreach (var unit in unitSpawner.AllSpawnedUnits)
            {
                if (unit != null)
                    snapshot.Units.Add(new UnitSnapshot(unit));
            }

            // Taken boxes are kept aside the same way, so undoing a pickup can put one back.
            foreach (var lootbox in lootSpawner.AllSpawnedLootboxes)
            {
                if (lootbox != null)
                    snapshot.Lootboxes.Add(new LootboxSnapshot(lootbox));
            }

            snapshot.Items.AddRange(itemManager.CaptureItems());

            foreach (var pair in fogOfWar.CaptureExplored())
                snapshot.Explored[pair.Key] = pair.Value;

            return snapshot;
        }

        /// <summary>
        /// Puts the world back into this state. Order matters: the turn is restored first so per-turn
        /// world state (fog owner, facing, selection) is rebuilt, then the units are laid back down
        /// over it - their recorded action points overwrite the turn's action point refresh.
        /// </summary>
        public void RestoreTo(UnitSpawner unitSpawner, TileSpawner tileSpawner, GameStateManager gameStateManager,
            FogOfWar fogOfWar, LootSpawner lootSpawner, ItemManager itemManager)
        {
            gameStateManager.RestoreTurn(ActiveTeam, UnitsHaveActionsLeft);

            fogOfWar.RestoreExplored(Explored);

            // Clear the board first: units are put back in recorded order, and one of them may be
            // moving onto a tile another one has not yet vacated.
            tileSpawner.ResetOccupiedTiles();

            foreach (var unitSnapshot in Units)
            {
                if (unitSnapshot.Unit == null)
                    continue;

                if (unitSnapshot.Alive)
                    unitSpawner.RestoreUnit(unitSnapshot.Unit);
                else
                    unitSpawner.RemoveUnit(unitSnapshot.Unit);

                unitSnapshot.ApplyTo();
            }

            // Loot after the units: a box puts itself back on its tile, and the tile has just been
            // handed its occupant. The inventory follows the boxes so the two never disagree about
            // what has been found.
            foreach (var lootboxSnapshot in Lootboxes)
                lootboxSnapshot.ApplyTo(lootSpawner);

            itemManager.RestoreItems(Items);

            // A single fog pass for the whole board instead of one per unit placement.
            fogOfWar.Recompute();

            // Restoring action points can make a unit report "no actions left" mid-restore, which
            // flips the turn flag off the recorded value - assert it once everything is back.
            gameStateManager.SetActionsLeft(UnitsHaveActionsLeft);
        }
    }

    /// <summary>
    /// One unit's placement and vitals at the moment of capture. Holds the unit itself, which is safe
    /// because removed units are only hidden (see <see cref="UnitSpawner.RemoveUnit"/>) and the whole
    /// history is dropped whenever units are respawned.
    /// </summary>
    public readonly struct UnitSnapshot
    {
        public readonly Unit Unit;
        public readonly bool Alive;
        public readonly Tile Position;
        public readonly int Health;
        public readonly int ActionPoints;

        // What items have permanently added to the unit's stats. Recorded although the weapon in hand
        // is not, and the difference is the point: a gain that cost action points and cannot be given
        // back by choosing differently is world state, so it can only come back by being written down.
        public readonly int[] StatBonuses;

        // The equipped weapon and the worn passive are deliberately not recorded: they are the
        // player's loadout rather than world state - free to swap, costing no turn and reported as no
        // action - and the item bar owns which of them is in use. Both are re-derived from the
        // recorded inventory when it is restored; storing them here would only add a second copy of
        // the truth to disagree with.

        public UnitSnapshot(Unit unit)
        {
            Unit = unit;
            Alive = unit.IsAlive;
            Position = unit.CurrentState.Position;
            Health = unit.CurrentState.Health;
            ActionPoints = unit.CurrentState.ActionPoints;
            StatBonuses = unit.CurrentState.CaptureBonuses();
        }

        public void ApplyTo()
        {
            Unit.RestoreSnapshot(Position, Health, ActionPoints, StatBonuses);
        }
    }

    /// <summary>
    /// Where one box stood at the moment of capture: which of the three states it was in, and the
    /// tile that goes with it. What is in it is not recorded - that is rolled once when the box is
    /// made and never changes.
    ///
    /// The tile is recorded because a box *can* move: a scattered one never leaves the tile it was
    /// put down on, but a dropped one is off the board until an opposing unit falls and then lies
    /// where it fell, so undoing that kill has to take the box back off that tile.
    ///
    /// Holding the box itself is safe for the same reason it is for units - a taken box is only
    /// hidden, and the whole history is dropped whenever the board is respawned.
    /// </summary>
    public readonly struct LootboxSnapshot
    {
        public readonly Lootbox Lootbox;
        public readonly LootboxState State;
        public readonly Tile Tile;

        public LootboxSnapshot(Lootbox lootbox)
        {
            Lootbox = lootbox;
            State = lootbox.State;
            Tile = lootbox.Tile;
        }

        public void ApplyTo(LootSpawner lootSpawner)
        {
            lootSpawner.RestoreLootbox(Lootbox, State, Tile);
        }
    }
}
