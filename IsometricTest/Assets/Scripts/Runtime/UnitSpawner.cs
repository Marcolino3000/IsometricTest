using System.Collections.Generic;
using Data;
using Runtime.Controls;
using UnityEngine;

namespace Runtime
{
    public class UnitSpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private List<Unit> units;
        
        [Header("Settings")]
        [SerializeField] private Color OpponentColor;
        
        [Header("References")]
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;

        public void RemoveUnit(Unit unit)
        {
            units.Remove(unit);
            Destroy(unit.gameObject);
        }
        
        private void Awake()
        {
            SpawnUnits();
        }

        [ContextMenu("Spawn Units")]
        private void SpawnUnits()
        {
            tileSpawner.ResetOccupiedTiles();
            ClearUnits();
            SpawnUnitsForTeam(Team.Player);
            SpawnUnitsForTeam(Team.Opponent);
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
                instance.transform.GetChild(1).localScale = settings.Scale;

                var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
                spriteRenderer.sortingOrder = settings.OrderInLayer;
                spriteRenderer.sprite = prefab.Blueprint.Sprite;
                
                var unit = instance.GetComponentInChildren<Unit>();
                unit.Init(tileSpawner, this, team);
                
                PlaceUnit(unit, team);

                if(team == Team.Opponent)
                {
                    spriteRenderer.flipX = true;
                    spriteRenderer.color = OpponentColor;
                    instance.name = $"Opponent {prefab.name} {i}";
                }
                
                units.Add(instance);
                
                selector.RegisterClickable(instance.GetComponentInChildren<Clickable>());
            }
        }

        private void PlaceUnit(Unit unit, Team team)
        {
            var gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            while(!unit.TryPlaceAtTile(tileSpawner.GetTileAtPosition(gridPosition)))
                gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            unit.transform.position = tileSpawner.GridIndexToWorldPosition(gridPosition);
            // unit.transform.rotation = Quaternion.Euler(settings.RotationOffset);
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
    }
}