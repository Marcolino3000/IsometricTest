using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private UnitState currentState;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;

        public void Init(TileSpawner tileSpawner, UnitSpawner unitSpawner, Vector2Int position)
        {
            currentState = blueprint.DefaultState;
            currentState.Position = position;
            
            this.tileSpawner = tileSpawner;
            this.unitSpawner = unitSpawner;
        }
        
        public bool TryMoveToTile(Tile selectedTile)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return false;
            }
            
            if (!tileSpawner.GetReachableTiles(currentState.Position, currentState.Range, out var reachableTiles))
                return false;
            
            if(!reachableTiles.Contains(selectedTile))
                return false;
            
            MoveToTile(selectedTile);
            return true;
        }

        private void MoveToTile(Tile selectedTile)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return;
            }

            currentState.Position = selectedTile.Position;
            transform.position = unitSpawner.GridToWorldPosition(selectedTile.Position);
            
            TileSpawner.ResetHighlightedTiles();
        }

        public void HighlightMoveableTiles()
        {
            tileSpawner.HighlightMoveableTiles(currentState.Position, currentState.Range);
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}