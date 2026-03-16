using System;
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


        private void Awake()
        {
            raycaster.OnUnitClicked += HandleUnitClicked;
            raycaster.OnTileClicked += HandleTileClicked;
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