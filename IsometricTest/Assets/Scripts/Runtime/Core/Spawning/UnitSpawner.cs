using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    public class UnitSpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private List<Unit> units;

        // Units that were removed from play. They are kept (hidden) rather than destroyed so undo can
        // put them back on the board; a respawn clears them out for good.
        [SerializeField] private List<Unit> removedUnits = new();

        // The player's single character, held from the moment it spawns so nothing has to scan the
        // board for it. Survives its own death: a removed unit is only hidden, and undo restores this
        // very object, so ask IsAlive rather than expecting null.
        [SerializeField] private Unit playerUnit;

        public IReadOnlyList<Unit> AllUnits => units;

        /// <summary>The player's character. Null only before the first spawn.</summary>
        public Unit PlayerUnit => playerUnit;

        /// <summary>Every unit of this match, in play or removed. Used to snapshot the board.</summary>
        public IEnumerable<Unit> AllSpawnedUnits => units.Concat(removedUnits);

        /// <summary>
        /// A unit has arrived on a tile - once per step of a move, since a move is one action per
        /// tile stepped on. Announced by the spawner that owns the units rather than by the unit
        /// itself, so whoever cares what lies on the ground (the loot) needs no reference handing
        /// down into every unit. Putting a unit back from a snapshot is not an arrival and is
        /// silent: undo restores what walking over something did, it does not do it again.
        /// </summary>
        public event Action<Unit> UnitEnteredTile;

        /// <summary>Says a unit has just been placed on a tile. Called by the unit that moved.</summary>
        public void NotifyEnteredTile(Unit unit)
        {
            UnitEnteredTile?.Invoke(unit);
        }

        [Header("References")]
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private FogOfWar fogOfWar;

        // Handed to every unit it spawns: whether a unit wears capability badges and whether it
        // shows a threat zone are match rules, and a unit is where both are drawn.
        [SerializeField] private GameRules gameRules;

        /// <summary>
        /// Takes a unit out of play. It is hidden instead of destroyed - that keeps it, and every
        /// reference to it, intact so <see cref="RestoreUnit"/> can bring it back when the action that
        /// killed it is undone.
        /// </summary>
        public void RemoveUnit(Unit unit)
        {
            if (unit == null || !units.Remove(unit))
                return;

            unit.CurrentState.OnNoActionsLeft -= CheckIfNoneHaveActionsLeft;
            removedUnits.Add(unit);
            unit.SetInPlay(false);

            // Nothing may go on pointing at a unit that is off the board: it is hidden rather than
            // destroyed, so a stale selection or hover survives its death looking valid.
            selector.DropUnit(unit);
        }

        /// <summary>Puts a previously removed unit back into play. Does nothing for a living unit.</summary>
        public void RestoreUnit(Unit unit)
        {
            if (unit == null || !removedUnits.Remove(unit))
                return;

            units.Add(unit);
            unit.SetInPlay(true);
            SubscribeToStateEvents(unit);
        }

        private void SpawnOpponentUnits()
        {
            foreach (var unitAmount in settings.OpponentUnits)
            {
                for (int i = 0; i < unitAmount.Amount; i++)
                {
                    SpawnUnit(Team.Opponent, unitAmount.Prefab, i);
                }
            }
        }

        private Unit SpawnUnit(Team team, Unit prefab, int index)
        {
            if (prefab == null)
            {
                Debug.LogError($"No unit prefab set for team {team} in {nameof(UnitSpawnerSettings)}.", settings);
                return null;
            }

            var instance = Instantiate(prefab, transform);

            var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sortingOrder = settings.OrderInLayer;
            spriteRenderer.sprite = prefab.Blueprint.Sprite;

            var unit = instance.GetComponentInChildren<Unit>();
            unit.Init(tileSpawner, this, team, gameStateManager, fogOfWar, gameRules);

            PlaceUnit(unit, team);

            if(team == Team.Opponent)
            {
                spriteRenderer.flipX = true;
                spriteRenderer.color = settings.OpponentColor;
                instance.name = $"Opponent {prefab.name} {index}";
            }
            else
            {
                instance.name = $"Player {prefab.name}";
            }

            units.Add(instance);

            selector.RegisterClickable(instance.GetComponentInChildren<Clickable>());

            SubscribeToStateEvents(unit);

            return instance;
        }

        private void SubscribeToStateEvents(Unit unit)
        {
            unit.CurrentState.OnNoActionsLeft += CheckIfNoneHaveActionsLeft;
        }

        private void CheckIfNoneHaveActionsLeft() //todo: also check when unit dies
        {
            var noneHaveActionsLeft = units
                .Where(u => u.CurrentState.Team == gameStateManager.State.Team)
                .All(u => !u.CurrentState.HasActionsLeft);
            
            if(noneHaveActionsLeft)
                gameStateManager.SetActionsLeft(false);     
            
        }

        /// <summary>
        /// Puts a freshly spawned unit on the first tile of its spawn zone that will take it. The zone is
        /// ranked rather than rolled: it can be walled off by mountains or filled by the units placed before
        /// it, and re-rolling inside it would spin forever - the tail of the list spills over its border
        /// instead.
        /// </summary>
        private void PlaceUnit(Unit unit, Team team)
        {
            foreach (var gridPosition in tileSpawner.GetSpawnZonePositions(team))
            {
                var tile = tileSpawner.GetTileAtPosition(gridPosition);

                if (!unit.TryPlaceAtTile(tile))
                    continue;

                unit.transform.position = tileSpawner.GridIndexToWorldPosition(gridPosition)
                                          + Vector3.up * tile.HeightOffset;
                return;
            }

            Debug.LogError($"No free tile left to spawn {unit.name} on.", settings);
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return tileSpawner.GridIndexToWorldPosition(gridPosition);
        }

        private void ClearUnits()
        {
            foreach (var unit in AllSpawnedUnits.ToList())
            {
                if (unit == null)
                    continue;

                unit.CurrentState.OnNoActionsLeft -= CheckIfNoneHaveActionsLeft;
                Destroy(unit.gameObject);
            }

            units.Clear();
            removedUnits.Clear();
            playerUnit = null;
        }

        #region Setup

        public void Setup(GameStateManager gameStateManagerArg, Selector selectorArg, FogOfWar fogOfWarArg,
            GameRules gameRulesArg)
        {
            gameStateManager = gameStateManagerArg;
            selector = selectorArg;
            fogOfWar = fogOfWarArg;
            gameRules = gameRulesArg;
        }

        [ContextMenu("Spawn Units")]
        public void SpawnUnits()
        {
            tileSpawner.ResetOccupiedTiles();
            ClearUnits();

            // The player fields a single character, the opponent a roster - hence the two paths.
            playerUnit = SpawnUnit(Team.Player, settings.PlayerUnit, 0);
            SpawnOpponentUnits();
        }

        #endregion
    }
}