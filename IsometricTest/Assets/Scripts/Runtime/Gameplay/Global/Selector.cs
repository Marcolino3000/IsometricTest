using System;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    public class Selector : MonoBehaviour
    {
        public event Action<Selection> OnSelectionChanged; 
        
        [Header("Debug")]
        [SerializeField] private Selection selection;
        [SerializeField] private Team activeTeam;

        [Header("References")]
        [SerializeField] private Raycaster raycaster;


        private void Awake()
        {
            selection.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(Selection selectionArg)
        {
            OnSelectionChanged?.Invoke(selectionArg);
            
        }

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
            
            if (selection.SelectedUnit != null)
                selection.SelectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(tile, null));
        }

        private void HandleUnitHover(Unit unit)
        {
            selection.HoveredUnit = unit;
            
            if(CheckIfFriendlyUnit(unit) && selection.SelectedUnit == null)
            {
                return;
            }
            
            if (CheckForAttackOnUnit(unit))
                selection.SelectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, unit));
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
            
            selection.SelectedUnit = null;
        }

        private bool HandleTileClick(Tile tile)
        {
            if (selection.SelectedUnit == null)
                return false;
            
            return selection.SelectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(tile, null));
        }

        private bool HandleUnitClick(Unit unit)
        {
            if (CheckForSelectUnit(unit))
            {
                return false;
            }
            
            if (!CheckForAttackOnUnit(unit))
                return false;
            
            return selection.SelectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(null, unit));
        }

        private void HandleMouseExit(IClickable clickable)
        {
            if (clickable is Unit)
            {
                selection.HoveredUnit = null;
            }
            else if (clickable is Tile)
            {
                selection.HoveredTile = null;
            }
            else
            {
                Debug.LogError("Clicked object is not a tile or unit");
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
            
            selection.SelectedUnit = unit;
            
            return true;
        }

        private bool CheckForAttackOnUnit(Unit targetUnit)
        {
            if (activeTeam != targetUnit.CurrentState.Team && selection.SelectedUnit != null)
            {
                return true;
            }

            return false;
        }

        public void Setup(GameStateManager gameStateManagerArg)
        {
            gameStateManagerArg.OnGameStateChanged += HandleStateChange;
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