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

        public IReadOnlyList<Unit> AllUnits => units;

        /// <summary>Every unit of this match, in play or removed. Used to snapshot the board.</summary>
        public IEnumerable<Unit> AllSpawnedUnits => units.Concat(removedUnits);

        [Header("References")]
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private FogOfWar fogOfWar;

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

        private void SpawnUnitsForTeam(Team team)
        {
            foreach (var unitAmount in settings.UnitAmounts)
            {
                SpawnUnitsFromPrefab(team, unitAmount.Amount, unitAmount.Prefab);
            }
        }

        private void SpawnUnitsFromPrefab(Team team, int amount, Unit prefab)
        {
            for(int i = 0; i < amount; i++)
            {
                var instance = Instantiate(prefab, transform);

                var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
                spriteRenderer.sortingOrder = settings.OrderInLayer;
                spriteRenderer.sprite = prefab.Blueprint.Sprite;
                
                var unit = instance.GetComponentInChildren<Unit>();
                unit.Init(tileSpawner, this, team, gameStateManager, fogOfWar);
                
                PlaceUnit(unit, team);

                if(team == Team.Opponent)
                {
                    spriteRenderer.flipX = true;
                    spriteRenderer.color = settings.OpponentColor;
                    instance.name = $"Opponent {prefab.name} {i}";
                }
                
                units.Add(instance);

                selector.RegisterClickable(instance.GetComponentInChildren<Clickable>());

                SubscribeToStateEvents(unit);
            }
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

        private void PlaceUnit(Unit unit, Team team)
        {
            var gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            while(!unit.TryPlaceAtTile(tileSpawner.GetTileAtPosition(gridPosition)))
                gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);

            var tile = tileSpawner.GetTileAtPosition(gridPosition);
            unit.transform.position = tileSpawner.GridIndexToWorldPosition(gridPosition)
                                      + Vector3.up * (tile != null ? tile.HeightOffset : 0f);
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
        }

        #region Setup

        public void Setup(GameStateManager gameStateManagerArg, Selector selectorArg, FogOfWar fogOfWarArg)
        {
            gameStateManager = gameStateManagerArg;
            selector = selectorArg;
            fogOfWar = fogOfWarArg;
        }

        [ContextMenu("Spawn Units")]
        public void SpawnUnits()
        {
            tileSpawner.ResetOccupiedTiles();
            ClearUnits();
            SpawnUnitsForTeam(Team.Player);
            SpawnUnitsForTeam(Team.Opponent);
        }

        #endregion
    }
}