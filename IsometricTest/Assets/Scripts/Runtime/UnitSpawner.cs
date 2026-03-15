using System.Collections.Generic;
using Data;
using TMPro;
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
            
            for(int i = 0; i < settings.Amount; i++)
            {
                var gridPosition = tileSpawner.GetRandomGridPosition();
                var position = tileSpawner.GridIndexToWorldPosition(gridPosition) + settings.PositionOffset;
                var rotation = Quaternion.Euler(settings.RotationOffset);
                
                var unit = Instantiate(settings.Prefab, position, rotation, transform);
                unit.transform.localScale = settings.Scale;

                unit.GetComponent<SpriteRenderer>().sortingOrder = settings.OrderInLayer;
                unit.layer = 7;
                unit.GetComponent<Unit>().Init(new UnitState()
                {
                    Team = Team.Player,
                    Position = gridPosition
                },
                    tileSpawner,
                    this);
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