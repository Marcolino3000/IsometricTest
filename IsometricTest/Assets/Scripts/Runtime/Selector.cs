using System;
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

        [Header("References")]
        [SerializeField] private Raycaster raycaster;

        public void RegisterClickable(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        private void HandleMouseExit(IClickable obj)
        {
            
        }

        private void HandleMouseEnter(IClickable obj)
        {
            
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

        private void HandleTileClicked(Tile tile)
        {
            if(CheckForMovement(tile))
                OnTurnFinished?.Invoke();
            
            selectedUnit = null;
        }

        private bool CheckForMovement(Tile tile)
        {
            if (selectedUnit != null)
            {
                if (selectedUnit.TryMoveToTile(tile))
                {
                    selectedUnit = null;
                    return true;
                }
            }
            
            return false;
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