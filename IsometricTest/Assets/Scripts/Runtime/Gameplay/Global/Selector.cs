using System;
using Runtime.Core.State;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using UnityEngine;
using Clickable = Runtime.Gameplay.Controls.Clickable;

namespace Runtime.Gameplay.Global
{
    public class Selector : MonoBehaviour
    {
        public event Action<ChangeEvent<Selection>> OnSelectionChanged;
        
        [Header("Debug")]
        [SerializeField] private Selection selection;
        [SerializeField] private Selection previousSelection;
        [SerializeField] private Team activeTeam;

        #region Setup

        public void RegisterClickable(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        public void Setup(GameStateManager gameStateManagerArg, Raycaster raycaster)
        {
            gameStateManagerArg.OnGameStateChanged += HandleStateChange;
            raycaster.OnClickedNothing += HandleClickNothing;
        }

        /// <summary>
        /// Clears any selection/hover state so it no longer references units or tiles that are
        /// about to be destroyed (e.g. on game restart), then notifies listeners of the empty selection.
        /// </summary>
        public void ResetSelection()
        {
            selection = new Selection { ActiveTeam = activeTeam };
            selection.HoveredTile = null; // touch a setter so Status recomputes to NoSelectionNoHover
            previousSelection = selection.Clone();

            CreateSelectionChangedEvent();
        }

        private void HandleStateChange(Core.State.ChangeEvent<State> changeEvent)
        {
            activeTeam = changeEvent.NewValue.Team;
            selection.ActiveTeam = changeEvent.NewValue.Team;

            if (changeEvent.PreviousValue.Team != changeEvent.NewValue.Team)
                ResetSelection();
        }

        #endregion

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

        private void HandleClick(IClickable clickable)
        {
            switch (clickable)
            {
                case Unit unit:
                    HandleUnitClick(unit);
                    break;
                case Tile tile:
                    HandleTileClick(tile);
                    break;
                default:
                    Debug.LogError("Clicked object is not a tile or unit");
                    break;
            }
        }

        /// <summary>
        /// Clicking empty space (neither a tile nor a unit) clears the current unit selection.
        /// </summary>
        private void HandleClickNothing()
        {
            if (selection.SelectedUnit == null)
                return;

            selection.SelectedUnit = null;
            CreateSelectionChangedEvent();
        }

        private void HandleTileHover(Tile tile)
        {
            selection.HoveredTile = tile;
            CreateSelectionChangedEvent();
        }

        private void HandleUnitHover(Unit unit)
        {
            if (selection.SelectedUnit == unit) 
                return;
            
            selection.HoveredUnit = unit;
            CreateSelectionChangedEvent();
        }

        private void HandleTileClick(Tile tile)
        {
            selection.ClickedTile = tile;
            CreateSelectionChangedEvent();
            selection.ClickedTile = null;
        }

        private void HandleUnitClick(Unit unit)
        {
            if (unit.CurrentState.Team == activeTeam)
                selection.SelectedUnit = unit;
            else
                selection.ClickedUnit = unit;

            CreateSelectionChangedEvent();
            selection.ClickedUnit = null;
        }

        private void CreateSelectionChangedEvent()
        {
            var changeEvent = new ChangeEvent<Selection>(previousSelection.Clone(), selection.Clone());

            OnSelectionChanged?.Invoke(changeEvent);

            previousSelection = selection.Clone();
        }
    }

    [Serializable]
    public class Selection
    {
        public Team ActiveTeam;
        public SelectionStatus Status;

    #region helpers
        public Selection Clone()
        {
            return new Selection
            {
                Status = Status,
                ActiveTeam = ActiveTeam,
                selectedUnit = selectedUnit,
                hoveredUnit = hoveredUnit,
                selectedTile = selectedTile,
                hoveredTile = hoveredTile,
                clickedTile = clickedTile,
                clickedUnit = clickedUnit
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
        public Unit ClickedUnit
        {
            get => clickedUnit;
            set
            {
                clickedUnit = value;
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
        public Tile ClickedTile
        {
            get => clickedTile;
            set
            {
                clickedTile = value;
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
    #endregion   
        private void UpdateStatus()
        {       
            // Debug.Log($"SelectedUnit: {selectedUnit != null}, ClickedUnit: {clickedUnit != null}, HoveredUnit: {hoveredUnit != null}, HoveredTile: {hoveredTile != null}");
            
            if (selectedUnit != null && clickedTile != null)
            {
                Status = SelectionStatus.SelectionTileClick;
                return;
            }

            
            switch (selectedUnit, clickedUnit, hoveredUnit, hoveredTile)
            {
                case (null, null, null, null):
                    Status = SelectionStatus.NoSelectionNoHover;
                    break;
                case(null, null, null, not null):
                    Status = SelectionStatus.NoSelectionTileHover;
                    break;
                case (null, null, { } hovered, not null) when hovered.CurrentState.Team == ActiveTeam:
                    Status = SelectionStatus.NoSelectionFriendlyHover;
                    break;
                case (null, null, { } hovered, not null) when hovered.CurrentState.Team != ActiveTeam:
                    Status = SelectionStatus.NoSelectionEnemyHover;
                    break;
                case (null, { } clicked, null, not null):
                    Status = SelectionStatus.NoSelectionEnemyClick;
                    break;
                case ({ } selected, null, null,null):
                    Status = SelectionStatus.SelectionNoHover;
                    break;
                case ({ } selected, null, null,not null):
                    Status = SelectionStatus.SelectionTileHover;
                    break;
                case ({ } selected, null, { } hovered,not null) when hovered.CurrentState.Team == ActiveTeam:
                    Status = SelectionStatus.SelectionFriendlyHover;
                    break;
                case ({ } selected, null, { } hovered,null) when hovered.CurrentState.Team != ActiveTeam:
                    Status = SelectionStatus.SelectionEnemyHover;
                    break;
                case ({ } selected, { } clicked, not null,null):
                    Status = SelectionStatus.SelectionEnemyClick;
                    break;
                default:
                    Status = SelectionStatus.UnexpectedCase;
                    break;
            }
        }

        private Unit selectedUnit; //stays selected until deselected or new selection occurs
        private Unit clickedUnit; //units that get clicked on but that can't be selected (e.g. enemy units)
        private Unit hoveredUnit;
        private Tile selectedTile;
        private Tile clickedTile; //tiles clicked to command the selected unit (e.g. move target)
        private Tile hoveredTile;
    }

    public enum SelectionStatus
    {
        UnexpectedCase,
        NoSelectionNoHover,
        NoSelectionFriendlyHover,
        NoSelectionEnemyHover,
        NoSelectionEnemyClick,
        SelectionNoHover,
        SelectionFriendlyHover,
        SelectionEnemyHover,
        SelectionEnemyClick,
        NoSelectionTileHover,
        SelectionTileHover,
        SelectionTileClick
    }
}
