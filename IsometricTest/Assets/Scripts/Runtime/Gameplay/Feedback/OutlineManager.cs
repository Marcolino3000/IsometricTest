using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class OutlineManager : MonoBehaviour
    {
        private Selector _selector;
        // private GameStateManager _gameStateManager;

        private Unit lastSelectedUnit;
        private Unit lastHoveredUnit;
        private Team _activeTeam;

        // private void Awake()
        // {
        //     _selector.OnSelectionChanged += HandleSelectionChanged;
        // }

        private void HandleSelectionChanged(Selection selection)
        {
            HandleHoveredUnits(selection);
            
            HandleSelectedUnits(selection);
            // if (selection.SelectedUnit == lastSelectedUnit)
            //     return;
            //     
            //
            // if (selection.SelectedUnit == null)
            //     return;
            //     
            // selection.SelectedUnit.Outline.Show();
            //
            // if(selection.SelectedUnit != lastSelectedUnit && lastSelectedUnit != null)
            // {
            //     lastSelectedUnit.Outline.Hide();
            //     lastSelectedUnit = selection.SelectedUnit;
            // }
        }

        private void HandleSelectedUnits(Selection selection)
        {
            if(selection.SelectedUnit != null)
                selection.SelectedUnit.Outline.Show(OutlineColor.Neutral, OutlineThickness.Thick);
            
            lastSelectedUnit?.Outline.Hide();
            lastSelectedUnit = selection.SelectedUnit;
        }

        private void HandleHoveredUnits(Selection selection)
        {
            if(selection.SelectedUnit == null)
            {
                if(selection.HoveredUnit != null)
                    selection.HoveredUnit.Outline.Show(OutlineColor.Neutral, OutlineThickness.Thin);
            }

            if (selection.SelectedUnit != null)
            {
                if (selection.HoveredUnit == null || selection.HoveredUnit == selection.SelectedUnit) 
                    return;
                
                if(selection.HoveredUnit.CurrentState.Team != _activeTeam)
                    selection.HoveredUnit.Outline.Show(OutlineColor.Attack, OutlineThickness.Thin);
                else
                    selection.HoveredUnit.Outline.Show(OutlineColor.Neutral, OutlineThickness.Thin);
            }
            
            lastHoveredUnit?.Outline.Hide();
            lastHoveredUnit = selection.HoveredUnit;
        }

        public void Setup(Selector selector, GameStateManager gameStateManager)
        {
            _selector = selector;
            _selector.OnSelectionChanged += HandleSelectionChanged;
            gameStateManager.OnGameStateChanged += HandleStateChange;
            
        }

        private void HandleStateChange(ChangeEvent changeEvent)
        {
            _activeTeam = changeEvent.newValue.Team;
        }
    }
}