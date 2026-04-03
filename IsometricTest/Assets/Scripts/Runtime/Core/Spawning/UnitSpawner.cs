using System.Collections.Generic;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    public class UnitSpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private List<Unit> units;
        
        [Header("References")]
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;
        [SerializeField] private GameStateManager gameStateManager;

        public void RemoveUnit(Unit unit)
        {
            units.Remove(unit);
            Destroy(unit.gameObject);
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
                unit.Init(tileSpawner, this, team, gameStateManager);
                
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

        private void CheckIfNoneHaveActionsLeft()
        {
            var noneHaveActionsLeft = units.TrueForAll(u => 
                !u.CurrentState.HasActionsLeft && 
                gameStateManager.State.Team == u.CurrentState.Team);
            
            if(noneHaveActionsLeft)
                gameStateManager.State.UnitsHaveActionsLeft = false;
        }

        private void PlaceUnit(Unit unit, Team team)
        {
            var gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            while(!unit.TryPlaceAtTile(tileSpawner.GetTileAtPosition(gridPosition)))
                gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            unit.transform.position = tileSpawner.GridIndexToWorldPosition(gridPosition);
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return tileSpawner.GridIndexToWorldPosition(gridPosition);
        }

        private void ClearUnits()
        {
            foreach (var unit in units)
            {
                Destroy(unit);
            }
        }

        #region Setup

        public void Setup(GameStateManager gameStateManagerArg, Selector selectorArg)
        {
            gameStateManager = gameStateManagerArg;
            selector = selectorArg;
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