using System.Collections.Generic;
using Data;
using UnityEngine;

namespace Runtime
{
    public class UnitSpawner : MonoBehaviour
    {
        [SerializeField] private List<GameObject> units;
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;

        public void RemoveUnit(Unit unit)
        {
            units.Remove(unit.gameObject);
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
            for(int i = 0; i < settings.Amount; i++)
            {
                var instance = Instantiate(settings.Prefab, transform);
                instance.transform.localScale = settings.Scale;

                instance.GetComponent<SpriteRenderer>().sortingOrder = settings.OrderInLayer;
                instance.layer = 7;
                
                var unit = instance.GetComponent<Unit>();
                unit.Init(tileSpawner, this, team);
                
                PlaceUnit(unit, team);

                if(team == Team.Opponent)
                {
                    instance.GetComponent<SpriteRenderer>().flipX = true;
                    instance.GetComponent<SpriteRenderer>().color = Color.red;
                }
                
                units.Add(instance);
            }
        }
        
        private void PlaceUnit(Unit unit, Team team)
        {
            var gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            while(!unit.TryPlaceAtTile(tileSpawner.GetTileAtPosition(gridPosition)))
                gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
            
            unit.transform.position = tileSpawner.GridIndexToWorldPosition(gridPosition) + settings.PositionOffset;
            unit.transform.rotation = Quaternion.Euler(settings.RotationOffset);
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return tileSpawner.GridIndexToWorldPosition(gridPosition) + settings.PositionOffset;
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