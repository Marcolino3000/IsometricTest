using System;
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

        private void OnDestroy()
        {
            Debug.Log("Unit Comp destroyed");
        }

        public void Init(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, Team team,
            GameStateManager gameStateManagerArg)
        {
            currentState = blueprint.DefaultState;
            currentState.Team = team;
            currentState.SetValueChangedCallbacks(HealthChangedCallback, ActionPointsChangedCallback);
            
            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            
            gameStateManager = gameStateManagerArg;
            gameStateManager.OnGameStateChanged += HandleStateChange;
            
            healthBar.Setup(blueprint.DefaultState.Health);
            actionExecutor.Setup(this, tileSpawner);
            
            TileHighlighter.Setup(currentState, tileSpawner);
        }

        private void HealthChangedCallback(int amount)
        {
            healthBar.SetBlobAmount(amount);
        }
        
        private void ActionPointsChangedCallback(int amount)
        {
            actionExecutor.HandleActionPointsChanged(amount);
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
            if (!tileSpawner.IsTileWithinReach(currentState.Position, targetUnit.CurrentState.Position, currentState.Range, false))
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
            return tileSpawner.IsTileWithinReach(currentState.Position, selectedTile, currentState.Range, true);
        }
        
        private bool CheckAttackValid(Unit selectedUnit)
        {
            return tileSpawner.IsTileWithinReach(currentState.Position, selectedUnit.CurrentState.Position, currentState.Range, false);
        }

        public bool TryMoveToTile(Tile selectedTile)
        {
            // if (!tileSpawner.IsTileWithinReach(currentState.Position, selectedTile, currentState.Range, true)) 
            //     return false;

            PlaceOnTile(selectedTile);
            return true;
        }

        public void Remove()
        {
            currentState.Position.SetUnit(null);
            unitSpawner.RemoveUnit(this);
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

        public void HandleStateChange(ChangeEvent<State> changeEvent)
        {
            if(changeEvent.PreviousValue.Team != changeEvent.NewValue.Team)
                HandleNewTurn(changeEvent.NewValue);
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