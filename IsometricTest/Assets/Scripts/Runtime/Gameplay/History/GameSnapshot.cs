using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
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

        // Explored ground is cumulative history - unlike visibility it cannot be recomputed from
        // where the units currently stand, so it has to travel with the snapshot.
        public readonly Dictionary<Team, HashSet<Vector2Int>> Explored = new();

        public static GameSnapshot Capture(UnitSpawner unitSpawner, GameStateManager gameStateManager, FogOfWar fogOfWar)
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

            foreach (var pair in fogOfWar.CaptureExplored())
                snapshot.Explored[pair.Key] = pair.Value;

            return snapshot;
        }

        /// <summary>
        /// Puts the world back into this state. Order matters: the turn is restored first so per-turn
        /// world state (fog owner, facing, selection) is rebuilt, then the units are laid back down
        /// over it - their recorded action points overwrite the turn's action point refresh.
        /// </summary>
        public void RestoreTo(UnitSpawner unitSpawner, TileSpawner tileSpawner, GameStateManager gameStateManager, FogOfWar fogOfWar)
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

        public UnitSnapshot(Unit unit)
        {
            Unit = unit;
            Alive = unit.IsAlive;
            Position = unit.CurrentState.Position;
            Health = unit.CurrentState.Health;
            ActionPoints = unit.CurrentState.ActionPoints;
        }

        public void ApplyTo()
        {
            Unit.RestoreSnapshot(Position, Health, ActionPoints);
        }
    }
}
