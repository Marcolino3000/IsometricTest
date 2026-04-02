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
            selection.OnSelectionChanged += sel => OnSelectionChanged?.Invoke(sel);
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
                    HandleTileHover(tile);
                    break;
                }
                default:
                    Debug.LogError("Clicked object is not a tile or unit");
                    break;
            }
        }

        private void HandleTileHover(Tile tile)
        {
            selection.HoveredTile = tile;
            
            if (selectedUnit != null)
                isHoveredActionValid = selectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(tile, null));
        }

        private void HandleUnitHover(Unit unit)
        {
            selection.HoveredUnit = unit;
            
            if(CheckIfFriendlyUnit(unit) && selectedUnit == null)
            {
                return;
            }
            
            if (CheckForAttackOnUnit(unit))
                isHoveredActionValid = selectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, unit));
        }

        private void HandleClick(IClickable clickable)
        {
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
            
            // isHoveredActionValid = false;
            selectedUnit = null;
            selection.SelectedUnit = null;
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
                    selection.HoveredUnit = null;
                    break;
                }
                case Tile tile:
                {
                    selection.HoveredTile = null;
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
            selection.SelectedUnit = unit;
            
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

        public void HandleStateChange(ChangeEvent changeEvent)
        {
            activeTeam = changeEvent.newValue.Team;
        }
    }

    [Serializable]
    public class Selection
    {
        public event Action<Selection> OnSelectionChanged;

        public Unit SelectedUnit
        {
            get => selectedUnit;
            set
            {
                selectedUnit = value;
                OnSelectionChanged?.Invoke(this);
            }
        }

        public Unit HoveredUnit
        {
            get => hoveredUnit;
            set
            {
                hoveredUnit = value;
                OnSelectionChanged?.Invoke(this);
            }
        }

        public Tile SelectedTile
        {
            get => selectedTile;
            set
            {
                selectedTile = value;
                OnSelectionChanged?.Invoke(this);
            }
        }

        public Tile HoveredTile
        {
            get => hoveredTile;
            set
            {
                hoveredTile = value;
                OnSelectionChanged?.Invoke(this);
            }
        }

        private Unit selectedUnit;
        private Unit hoveredUnit;
        private Tile selectedTile;
        private Tile hoveredTile;
    }
}