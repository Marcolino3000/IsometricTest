using UnityEngine;

namespace Runtime
{
    public class Unit : MonoBehaviour
    {
        public UnitState CurrentState => currentState;
        
        [SerializeField] private UnitState currentState;
        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;

        public void Init(TileSpawner tileSpawner, UnitSpawner unitSpawner, Team team)
        {
            currentState = blueprint.DefaultState;
            currentState.Team = team;
            
            this.tileSpawner = tileSpawner;
            this.unitSpawner = unitSpawner;
        }
        
        public bool TryPlaceAtTile(Tile selectedTile)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return false;
            }

            if (selectedTile.IsOccupied)
                return false;
            
            PlaceOnTile(selectedTile);
            return true;
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
            
            PlaceOnTile(selectedTile);
            return true;
        }

        private void PlaceOnTile(Tile selectedTile)
        {
            var currentTile = tileSpawner.GetTileAtPosition(currentState.Position);
            currentTile.SetOccupied(false);

            currentState.Position = selectedTile.Position;
            transform.position = unitSpawner.GridToWorldPosition(selectedTile.Position);
            
            selectedTile.SetOccupied(true);
            
            // TileSpawner.ResetHighlightedTiles();
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