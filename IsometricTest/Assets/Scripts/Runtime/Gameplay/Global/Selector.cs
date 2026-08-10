using System;
using Runtime.Core.Spawning;
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

        [Header("References")]
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private MatchOutcomeWatcher outcomeWatcher;

        #region Setup

        public void RegisterClickable(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        public void Setup(GameStateManager gameStateManagerArg, Raycaster raycaster, UnitSpawner unitSpawnerArg,
            MatchOutcomeWatcher matchOutcomeWatcher)
        {
            unitSpawner = unitSpawnerArg;
            outcomeWatcher = matchOutcomeWatcher;
            gameStateManagerArg.TurnReset += HandleTurnReset;
            gameStateManagerArg.TurnStarted += HandleTurnStarted;
            raycaster.OnClickedNothing += HandleClickNothing;
        }

        public void ResetSelection()
        {
            selection = new Selection { ActiveTeam = activeTeam };
            selection.HoveredTile = null; // touch a setter so Status recomputes to NoSelectionNoHover
            previousSelection = selection.Clone();

            CreateSelectionChangedEvent();
        }

        /// <summary>
        /// Picks the player's character up. It is the only one they command, so selecting it by hand
        /// would be a click with exactly one possible outcome.
        ///
        /// Public because undo/redo restores a turn without a <see cref="GameStateManager.TurnStarted"/>
        /// and has to ask for this itself once the board is back in place.
        /// </summary>
        public void SelectPlayerUnit()
        {
            var playerUnit = PlayerUnitThisTurn;

            if (playerUnit == null || selection.SelectedUnit == playerUnit)
                return;

            selection.SelectedUnit = playerUnit;

            CreateSelectionChangedEvent();
        }

        /// <summary>
        /// Forgets a unit that has left the board. A removed unit is only hidden, so a selection or
        /// hover pointing at one goes on looking perfectly valid while the sprite child that carries
        /// its visuals - and that the attack preview measures - is no longer active.
        /// </summary>
        public void DropUnit(Unit unit)
        {
            if (unit == null || (selection.SelectedUnit != unit && selection.HoveredUnit != unit))
                return;

            if (selection.HoveredUnit == unit)
                selection.HoveredUnit = null;

            if (selection.SelectedUnit == unit)
                selection.SelectedUnit = null;

            CreateSelectionChangedEvent();
        }

        /// <summary>
        /// The player's character while the turn is its own, otherwise null - the opponent's turn is
        /// not theirs to point at. Removed units are kept around hidden for undo, so IsAlive is the
        /// in-play test rather than a null check.
        /// </summary>
        private Unit PlayerUnitThisTurn
        {
            get
            {
                var playerUnit = unitSpawner != null ? unitSpawner.PlayerUnit : null;

                return playerUnit != null && playerUnit.IsAlive && playerUnit.CurrentState.Team == activeTeam
                    ? playerUnit
                    : null;
            }
        }

        private void HandleTurnReset(ChangeEvent<State> changeEvent)
        {
            activeTeam = changeEvent.NewValue.Team;
            ResetSelection();
        }

        /// <summary>
        /// Waits for phase 2 to pick the character up: units subscribe to <see cref="GameStateManager.TurnReset"/>
        /// only once they spawn, so they refresh their action points after this selector has already
        /// handled it. Selecting in phase 1 would mark the reachable tiles of the turn just ended.
        /// </summary>
        private void HandleTurnStarted(ChangeEvent<State> changeEvent)
        {
            SelectPlayerUnit();
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

        /// <summary>
        /// A click on the board. Ignored once the match has been decided: this is the head of the
        /// selection pipeline, so refusing here is what stops the player moving and attacking under
        /// the end screen, and one refusal covers every action the pipeline leads to. Hovering is
        /// left alone - a preview changes nothing - and an undo lifts the block with the verdict.
        /// </summary>
        private void HandleClick(IClickable clickable)
        {
            if (outcomeWatcher != null && outcomeWatcher.IsOver)
                return;

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
        
        private void HandleClickNothing()
        {
            selection.HoveredUnit = null;
            selection.HoveredTile = null;
            selection.ClickedTile = null;
            selection.SelectedTile = null;

            // The character is picked straight back up: with no second unit to switch to, dropping it
            // would only cost the player the click to select it again. Set last so the status
            // recomputes against the cleared hovers.
            selection.SelectedUnit = PlayerUnitThisTurn;

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
                case (null, null, { } hovered, null) when hovered.CurrentState.Team == ActiveTeam:
                    Status = SelectionStatus.NoSelectionFriendlyHover;
                    break;
                case (null, null, { } hovered, null) when hovered.CurrentState.Team != ActiveTeam:
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
                case ({ } selected, null, { } hovered,null) when hovered.CurrentState.Team == ActiveTeam:
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

        private Unit selectedUnit;
        private Unit clickedUnit; 
        private Unit hoveredUnit;
        private Tile selectedTile;
        private Tile clickedTile; 
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
