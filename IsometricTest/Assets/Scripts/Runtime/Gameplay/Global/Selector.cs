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
        public event Action<ChangeEvent<Selection>> OnSelectionChanged;
        
        [Header("Debug")]
        [SerializeField] private Selection selection;
        [SerializeField] private Selection previousSelection;
        [SerializeField] private Team activeTeam;

        [Header("References")]
        [SerializeField] private Raycaster raycaster;


        private void Awake()
        {
            // selection.OnSelectionChanged += HandleSelectionChanged;
        }

        // private void HandleSelectionChanged(Selection selectionArg)
        // {
        //     OnSelectionChanged?.Invoke(selectionArg);
        // }

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
            CreateSelectionChangedEvent();

            if (selection.SelectedUnit != null)
                selection.SelectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(tile, null));
        }

        private void HandleUnitHover(Unit unit)
        {
            if(selection.SelectedUnit != unit)
            {
                selection.HoveredUnit = unit;
                CreateSelectionChangedEvent();
            }

            if(CheckIfFriendlyUnit(unit) && selection.SelectedUnit == null)
            {
                // selection.Status = SelectionStatus.NoSelectionFriendlyHover;
                return;
            }

            if (CheckForAttackOnUnit(unit))
            {
                // selection.Status = SelectionStatus.SelectionEnemyHover;
                selection.SelectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, unit));
            }
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
            
            // selection.SelectedUnit = null;
        }

        private bool HandleTileClick(Tile tile)
        {
            if (selection.SelectedUnit == null)
                return false;

            return selection.SelectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(tile, null));
        }

        private bool HandleUnitClick(Unit unit)
        {
            selection.SelectedUnit = unit;
            CreateSelectionChangedEvent();
            
            if (CheckForSelectUnit(unit))
            {
                return false;
            }

            if (!CheckForAttackOnUnit(unit))
                return false;

            return selection.SelectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(null, unit));
        }

        private void CreateSelectionChangedEvent()
        {
            var changeEvent = new ChangeEvent<Selection>(previousSelection.Clone(), selection.Clone());

            OnSelectionChanged?.Invoke(changeEvent);

            previousSelection = selection.Clone();
        }

        private void HandleMouseExit(IClickable clickable)
        {
            if (clickable is Unit)
            {
                selection.HoveredUnit = null;
                CreateSelectionChangedEvent();
            }
            else if (clickable is Tile)
            {
                selection.HoveredTile = null;
                CreateSelectionChangedEvent();
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
            CreateSelectionChangedEvent();

            return true;
        }

        private bool CheckForAttackOnUnit(Unit targetUnit)
        {
            if (activeTeam != targetUnit.CurrentState.Team && selection.SelectedUnit != null)
            {
                // selection.Status = SelectionStatus.SelectionEnemyHover;
                return true;
            }

            return false;
        }

        public void Setup(GameStateManager gameStateManagerArg)
        {
            gameStateManagerArg.OnGameStateChanged += HandleStateChange;
        }

        private void HandleStateChange(ChangeEvent<State> changeEvent)
        {
            activeTeam = changeEvent.NewValue.Team;
            selection.ActiveTeam = changeEvent.NewValue.Team;
        }
    }

    [Serializable]
    public class Selection
    {
        // public event Action<Selection> OnSelectionChanged;

        public Team ActiveTeam;
        public SelectionStatus Status;
        // public Selection(Selection other)
        // {
        //     if (other == null)
        //         return;
        //
        //     Status = other.Status;
        //     selectedUnit = other.selectedUnit;
        //     hoveredUnit = other.hoveredUnit;
        //     selectedTile = other.selectedTile;
        //     hoveredTile = other.hoveredTile;
        // }
        
        public Selection Clone()
        {
            return new Selection
            {
                Status = Status,
                ActiveTeam = ActiveTeam,
                selectedUnit = selectedUnit,
                hoveredUnit = hoveredUnit,
                selectedTile = selectedTile,
                hoveredTile = hoveredTile
            };
        }

        public Unit SelectedUnit
        {
            get => selectedUnit;
            set
            {
                selectedUnit = value;
                UpdateStatus();
            }
        }

        public Unit HoveredUnit
        {
            get => hoveredUnit;
            set
            {
                hoveredUnit = value;
                UpdateStatus();
            }
        }

        public Tile SelectedTile
        {
            get => selectedTile;
            set
            {
                selectedTile = value;
                UpdateStatus();
            }
        }

        public Tile HoveredTile
        {
            get => hoveredTile;
            set
            {
                hoveredTile = value;
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            switch (selectedUnit, hoveredUnit)
            {
                case (null, null):
                    Status = SelectionStatus.NoSelectionNoHover;
                    break;
                case (null, { } hovered) when hovered.CurrentState.Team == ActiveTeam:
                    Status = SelectionStatus.NoSelectionFriendlyHover;
                    break;
                case (null, { } hovered) when hovered.CurrentState.Team != ActiveTeam:
                    Status = SelectionStatus.NoSelectionEnemyHover;
                    break;
                case ({ } selected, null):
                    Status = SelectionStatus.SelectionNoHover;
                    break;
                case ({ } selected, { } hovered) when selected.CurrentState.Team == ActiveTeam && hovered.CurrentState.Team == ActiveTeam:
                    Status = SelectionStatus.SelectionFriendlyHover;
                    break;
                case ({ } selected, { } hovered) when selected.CurrentState.Team == ActiveTeam && hovered.CurrentState.Team != ActiveTeam:
                    Status = SelectionStatus.SelectionEnemyHover;
                    break;
            }
        }

        private Unit selectedUnit;
        private Unit hoveredUnit;
        private Tile selectedTile;
        private Tile hoveredTile;
    }

    public enum SelectionStatus
    {
        NoSelectionNoHover,
        NoSelectionFriendlyHover,
        NoSelectionEnemyHover,
        SelectionNoHover,
        SelectionFriendlyHover,
        SelectionEnemyHover
    }
}
