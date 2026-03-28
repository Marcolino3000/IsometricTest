using System;
using System.Collections.Generic;
using Runtime.Actions;
using Runtime.Controls;
using UnityEngine;

namespace Runtime
{
    public class Selector : MonoBehaviour, IStateChangeHandler
    {
        public event Action OnTurnFinished;
        
        [Header("Debug")]
        [SerializeField] private Unit selectedUnit;
        [SerializeField] private Team activeTeam;
        [SerializeField] private bool isActionValid;

        [Header("References")]
        [SerializeField] private Raycaster raycaster;

        public void RegisterClickable(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        private void HandleClick(IClickable clickable)
        {
            TileSpawner.ResetHighlightedTiles();
            
            switch (clickable)
            {
                case Tile tile:
                    HandleTileClicked(tile);
                    break;
                case Unit unit:
                    HandleUnitClicked(unit);
                    break; 
            }
        }

        private void HandleMouseEnter(IClickable clickable)
        {
            if(selectedUnit == null)
                return;
            
            switch (clickable)
            {
                case Tile tile:
                    Debug.Log("Highlight path: " + tile.name);
                    UpdatePlannedActions(tile);
                    break;
                case Unit unit:
                    Debug.Log("show attack stuff: " + unit.name);
                    // UpdatePlannedActions(unit);
                    break;
            }   
        }

        private void UpdatePlannedActions(Tile tile)
        {
            int steps = ChebyshevDistance(selectedUnit.CurrentState.Position.Position, tile.Position);

            var actions = CreateActions(steps);
            
            isActionValid = selectedUnit.ActionExecutor.PlanActions(actions, tile);
        }

        private List<UnitAction> CreateActions(int steps)
        {
            List<UnitAction> actions = new List<UnitAction>();
            
            actions.Add(selectedUnit.Blueprint.MoveAction);
            
            return actions;
        }

        private void UpdatePlannedActions(Unit unit)
        {
            int steps = ChebyshevDistance(selectedUnit.CurrentState.Position.Position, unit.CurrentState.Position.Position);   
        }
        
        public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy);
        }

        private void HandleMouseExit(IClickable clickable)
        {
            // switch (clickable)
            // {
            //     case Tile tile:
            //         
            // }
        }

        private void HandleTileClicked(Tile tile)
        {
            if (!isActionValid)
                return;

            selectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(tile));
            // if(CheckForMovement(tile))
            OnTurnFinished?.Invoke();
            
            selectedUnit = null;
            isActionValid = false;
        }


        private void HandleUnitClicked(Unit unit)
        {
            if (CheckForAttackOnUnit(unit))
            {
                selectedUnit = null;
                OnTurnFinished?.Invoke();
                return;
            }

            CheckForSelectUnit(unit);
        }

        private void CheckForSelectUnit(Unit unit)
        {
            if(unit.CurrentState.Team != activeTeam)
                return;
            
            selectedUnit = unit;
            selectedUnit.HighlightMoveableTiles();
        }

        private bool CheckForAttackOnUnit(Unit unit)
        {
            if (activeTeam != unit.CurrentState.Team && selectedUnit != null)
            {
                return selectedUnit.TryAttackUnit(unit);
            }

            return false;
        }

        public void HandleStateChange(State newState)
        {
            activeTeam = newState.Team;
        }
    }
}