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
        [SerializeField] private bool isHoveredActionValid;

        [Header("References")]
        [SerializeField] private Raycaster raycaster;

        public void RegisterClickable(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        private void HandleMouseEnter(IClickable clickable)
        {
            switch (clickable)
            {
                case Unit unit:
                {
                    HandleUnitHover(unit);
                    break;
                }
                case Tile tile:
                {
                    if (selectedUnit != null)
                        isHoveredActionValid = selectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(tile, null));
                    break;
                }
                default:
                    Debug.LogError("Clicked object is not a tile or unit");
                    break;
            }
        }

        private void HandleUnitHover(Unit unit)
        {
            if(CheckIfFriendlyUnit(unit) && selectedUnit == null)
            {
                TileSpawner.ResetHighlightedTiles(); //todo: in highlight moveable tiles?
                unit.HighlightMoveableTiles();
                return;
            }
            
            if (CheckForAttackOnUnit(unit))
                isHoveredActionValid = selectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, unit));
        }

        private void HandleClick(IClickable clickable)
        {
            TileSpawner.ResetHighlightedTiles();
            
            bool executedAction = false;
            
            if (clickable is Unit unit)
            {
                executedAction = HandleUnitClick(unit);
            }
            else if (clickable is Tile tile)
            {
                executedAction = HandleTileClick(tile);
            }
            else
            {
                Debug.LogError("Clicked object is not a tile or unit");
            }

            if (!executedAction) 
                return;
            
            isHoveredActionValid = false;
            selectedUnit = null;
            TileSpawner.ResetHighlightedTiles();
            OnTurnFinished?.Invoke();
        }

        private bool HandleTileClick(Tile tile)
        {
            if (selectedUnit == null)
                return false;
            
            return selectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(tile, null));
        }

        private bool HandleUnitClick(Unit unit)
        {
            if (CheckForSelectUnit(unit))
            {
                unit.HighlightMoveableTiles();
                return false;
            }
            
            if (!CheckForAttackOnUnit(unit))
                return false;
            
            return selectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(null, unit));
        }

        // private void UpdatePlannedActions(IClickable clickable)
        // {
        //     ExecuteArgs executeArgs = CreateExecutionArgs(clickable);   
        //     
        //     int steps = ChebyshevDistance(selectedUnit.CurrentState.Position.Position, executeArgs.TargetPosition);
        //
        //     var actions = CreateActions(steps);
        //     
        //     isActionValid = selectedUnit.ActionExecutor.PlanActions(actions, executeArgs);
        // }

        private ExecuteArgs CreateExecutionArgs(IClickable clickable)
        {
            return clickable switch
            {
                Tile tile => new ExecuteArgs(tile, null),
                Unit unit => new ExecuteArgs(null, unit),
                _ => null
            };
        }

        // private List<UnitAction> CreateActions(int steps)
        // {
        //     List<UnitAction> actions = new List<UnitAction>();
        //     
        //     actions.Add(selectedUnit.Blueprint.MoveAction);
        //     
        //     return actions;
        // }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy);
        }

        private void HandleMouseExit(IClickable clickable)
        {
            isHoveredActionValid = false;
            
            switch (clickable)
            {
                case Unit unit:
                {
                    if (selectedUnit == null)
                        TileSpawner.ResetHighlightedTiles();
                    break;
                }
                case Tile tile:
                {
                    break;
                }
                default:
                    Debug.LogError("Clicked object is not a tile or unit");
                    break;
            }
        }
        
        private bool CheckIfFriendlyUnit(Unit unit)
        {
            return unit.CurrentState.Team == activeTeam;
        }

        private bool CheckForSelectUnit(Unit unit)
        {
            if(unit.CurrentState.Team != activeTeam)
                return false;
            
            selectedUnit = unit;
            
            return true;
        }

        private bool CheckForAttackOnUnit(Unit targetUnit)
        {
            if (activeTeam != targetUnit.CurrentState.Team && selectedUnit != null)
            {
                return true;
            }

            return false;
        }

        public void HandleStateChange(State newState)
        {
            activeTeam = newState.Team;
        }
    }
}