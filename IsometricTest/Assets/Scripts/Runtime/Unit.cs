using System.Collections.Generic;
using UnityEngine;

namespace Runtime
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitState currentState;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;

        public void Init(UnitState state, TileSpawner tileSpawner, UnitSpawner unitSpawner)
        {
            currentState = state;
            this.tileSpawner = tileSpawner;
            this.unitSpawner = unitSpawner;
        }

        private List<Vector2Int> GetMoveableTilePositions()
        {
            var tiles = new List<Vector2Int>();
            
            Direction.SetContext(new Context{Team = currentState.Team});

            foreach (var direction in Direction.ForwardAndSides)
            {
                Vector2Int positionToCheck = currentState.Position + direction;

                if (tileSpawner.CheckGridPosition(positionToCheck.x, positionToCheck.y))
                {
                    tiles.Add(positionToCheck);
                }    
            }
            
            return tiles;
        }

        public void HighlightMoveableTiles()
        {
            foreach (var tilePosition in GetMoveableTilePositions())
            {
                tileSpawner.HighlightTile(tilePosition);
            }
        }

        public void MoveToTile(Tile selectedTile)
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
    }

    public enum Team
    {
        Player,
        Opponent
    }
}