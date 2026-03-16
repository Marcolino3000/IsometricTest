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
            if (activeTeam != unit.CurrentState.Team)
            {
                return;
            }
            
            selectedUnit = unit;
            selectedUnit.HighlightMoveableTiles();
        }

        public void HandleStateChange(State newState)
        {
            activeTeam = newState.Team;
        }
    }
}