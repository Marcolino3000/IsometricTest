using System;
using Runtime.Actions;
using Runtime.Controls;
using UnityEngine;

namespace Runtime
{
    public class Selector : MonoBehaviour, IStateChangeHandler
    {
        public event Action<Selection> OnSelectionChanged; 
        public event Action OnTurnFinished;
        
        [Header("Debug")]
        [SerializeField] private Unit selectedUnit;
        [SerializeField] private Selection selection;
        [SerializeField] private Team activeTeam;
        [SerializeField] private bool isHoveredActionValid;

        [Header("References")]
        [SerializeField] private Raycaster raycaster;


        private void Awake()
        {
            ClickableRegistry.OnClickableSpawned += RegisterClickable;
            selection.OnSelectionChanged += selection => OnSelectionChanged?.Invoke(selection);
        }

        private void RegisterClickable(Clickable clickable)
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
                // TileSpawner.ResetHighlightedTiles(); //todo: in highlight moveable tiles?
                // unit.HighlightMoveableTiles();
                return;
            }
            
            if (CheckForAttackOnUnit(unit))
                isHoveredActionValid = selectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, unit));
        }

        private void HandleClick(IClickable clickable)
        {
            // TileSpawner.ResetHighlightedTiles();
            
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
            selection.Unit = null;
            // TileSpawner.ResetHighlightedTiles();
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
                // unit.HighlightMoveableTiles();
                return false;
            }
            
            if (!CheckForAttackOnUnit(unit))
                return false;
            
            return selectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(null, unit));
        }
        

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
                    // if (selectedUnit == null)
                        // TileSpawner.ResetHighlightedTiles();
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
            selection.Unit = unit;
            
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

    [Serializable]
    public class Selection
    {
        public event Action<Selection> OnSelectionChanged; 
        public Unit Unit { get => unit; set
        { 
            unit = value;
            OnSelectionChanged?.Invoke(this);
        }}

        private Unit unit;
    }
}