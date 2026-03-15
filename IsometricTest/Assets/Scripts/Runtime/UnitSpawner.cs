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
        
        private void Awake()
        {
            SpawnUnits();
        }

        [ContextMenu("Spawn Units")]
        private void SpawnUnits()
        {
            ClearUnits();
            SpawnUnitsForTeam(Team.Player);
            SpawnUnitsForTeam(Team.Opponent);
        }
        private void SpawnUnitsForTeam(Team team)
        {
            for(int i = 0; i < settings.Amount; i++)
            {
                var gridPosition = tileSpawner.GetRandomSpawnZonePosition(team);
                var position = tileSpawner.GridIndexToWorldPosition(gridPosition) + settings.PositionOffset;
                var rotation = Quaternion.Euler(settings.RotationOffset);
                
                var unit = Instantiate(settings.Prefab, position, rotation, transform);
                unit.transform.localScale = settings.Scale;

                unit.GetComponent<SpriteRenderer>().sortingOrder = settings.OrderInLayer;
                unit.layer = 7;
                unit.GetComponent<Unit>().Init(tileSpawner, this, gridPosition);
                
                if(team == Team.Opponent)
                {
                    unit.GetComponent<SpriteRenderer>().flipX = true;
                    unit.GetComponent<SpriteRenderer>().color = Color.red;
                }
                
                units.Add(unit);
            }
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