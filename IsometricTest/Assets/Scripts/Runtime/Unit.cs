using UnityEngine;

namespace Runtime
{
    public class Unit : MonoBehaviour
    {
        public UnitState CurrentState => currentState;
        public UnitBlueprint Blueprint => blueprint;
        
        [SerializeField] private UnitState currentState;
        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private HealthBar healthBar;

        public void Init(TileSpawner tileSpawner, UnitSpawner unitSpawner, Team team)
        {
            currentState = blueprint.DefaultState;
            currentState.Team = team;
            currentState.SetHealthChangedCallback(HealthChangedCallback);
            
            this.tileSpawner = tileSpawner;
            this.unitSpawner = unitSpawner;
            
            healthBar.Setup(blueprint.DefaultState.Health);
        }

        private void HealthChangedCallback(int newHealth)
        {
            healthBar.SetBlobAmount(newHealth);
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

        public bool TryAttackUnit(Unit targetUnit)
        {
            if (!IsTileWithinReach(targetUnit.CurrentState.Position, false))
                return false;
            
            AttackUnit(targetUnit);
            return true;
        }

        private void AttackUnit(Unit targetUnit)
        {
            CombatRunner.ResolveCombat(this, targetUnit);
            // var targetPosition = targetUnit.CurrentState.Position;
            // targetUnit.Remove();
            // TryMoveToTile(targetPosition);
        }

        public bool TryMoveToTile(Tile selectedTile)
        {
            if (!IsTileWithinReach(selectedTile, true)) 
                return false;

            PlaceOnTile(selectedTile);
            return true;
        }

        public void Remove()
        {
            currentState.Position.SetUnit(null);
            unitSpawner.RemoveUnit(this);
        }

        private bool IsTileWithinReach(Tile selectedTile, bool filterOccupiedTiles)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return false;
            }
            
            if (!tileSpawner.GetTilesWithinReach(currentState.Position.Position, currentState.Range, out var reachableTiles))
                return false;
            
            if(filterOccupiedTiles)
                tileSpawner.FilterForOccupiedTiles(reachableTiles);
            
            if(!reachableTiles.Contains(selectedTile))
                return false;
            return true;
        }

        private void PlaceOnTile(Tile selectedTile)
        {
            var currentTile = currentState.Position;
            if(currentTile != null)
                currentTile.SetUnit(null);

            currentState.Position = selectedTile;
            transform.position = unitSpawner.GridToWorldPosition(selectedTile.Position);
            
            selectedTile.SetUnit(this);
            
            // TileSpawner.ResetHighlightedTiles();
        }

        public void HighlightMoveableTiles()
        {
            tileSpawner.HighlightMoveableTiles(currentState.Position.Position, currentState.Range);
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}