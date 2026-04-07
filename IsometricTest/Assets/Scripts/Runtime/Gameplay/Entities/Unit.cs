using Data;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Global;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class Unit : MonoBehaviour, IClickable
    {
        public UnitState CurrentState => currentState;
        public UnitBlueprint Blueprint => blueprint;
        public ActionExecutor ActionExecutor => actionExecutor;
        
        [Header("Debug")]
        [SerializeField] private UnitState currentState;

        [Header("References")]
        public UnitTileHighlighter TileHighlighter;
        public UnitOutline Outline;

        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private ActionExecutor actionExecutor;

        public void Init(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, Team team,
            GameStateManager gameStateManagerArg)
        {
            currentState = blueprint.DefaultState;
            currentState.Team = team;
            currentState.SetHealthChangedCallback(HealthChangedCallback);
            
            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            
            gameStateManager = gameStateManagerArg;
            gameStateManager.OnGameStateChanged += HandleStateChange;
            
            healthBar.Setup(blueprint.DefaultState.Health);
            actionExecutor.Setup(this, CheckMoveValid, TryMoveToTile, CheckAttackValid, TryAttackUnit);
            
            TileHighlighter.Setup(currentState, tileSpawner);
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

        private bool TryAttackUnit(Unit targetUnit)
        {
            if (!IsTileWithinReach(targetUnit.CurrentState.Position, false))
                return false;
            
            AttackUnit(targetUnit);
            return true;
        }

        private void AttackUnit(Unit targetUnit)
        {
            CombatRunner.ResolveCombat(this, targetUnit);
        }

        private bool CheckMoveValid(Tile selectedTile)
        {
            return IsTileWithinReach(selectedTile, true);
        }
        
        private bool CheckAttackValid(Unit selectedUnit)
        {
            return IsTileWithinReach(selectedUnit.CurrentState.Position, false);
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

        public bool IsTileWithinReach(Tile selectedTile, bool filterOccupiedTiles)
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
        }

        public void HandleStateChange(ChangeEvent changeEvent)
        {
            if(changeEvent.previousValue.Team != changeEvent.newValue.Team)
                HandleNewTurn(changeEvent.newValue);
        }

        private void HandleNewTurn(State newState)
        {
            if(newState.Team == currentState.Team)
                currentState.ActionPoints = blueprint.DefaultState.ActionPoints;
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}