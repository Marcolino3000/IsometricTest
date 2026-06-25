using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class OutlineManager : MonoBehaviour
    {
        private Unit lastSelectedUnit;
        private Unit lastHoveredUnit;
        private Team _activeTeam;

        public void Setup(Selector selector, GameStateManager gameStateManager)
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
            gameStateManager.OnGameStateChanged += HandleStateChange;
        }

        private void HandleSelectionChanged(ChangeEvent<Selection> changeEvent)
        {
            var selection = changeEvent.NewValue;

            UpdateSelectedOutline(selection.SelectedUnit);
            UpdateHoveredOutline(selection.SelectedUnit, selection.HoveredUnit);
        }

        /// <summary>
        /// The selected unit keeps a white outline until a different unit is selected or it is deselected.
        /// </summary>
        private void UpdateSelectedOutline(Unit selectedUnit)
        {
            if (lastSelectedUnit == selectedUnit)
                return;

            if (lastSelectedUnit != null)
                lastSelectedUnit.Outline.Hide();

            lastSelectedUnit = selectedUnit;

            if (selectedUnit != null)
                selectedUnit.Outline.Show(OutlineColor.Neutral, OutlineThickness.Thick);
        }

        /// <summary>
        /// Hovering a unit gives it a white outline (red when a selected unit is hovering an enemy it
        /// could attack). The selected unit is left untouched so it keeps its own outline.
        /// </summary>
        private void UpdateHoveredOutline(Unit selectedUnit, Unit hoveredUnit)
        {
            if (lastHoveredUnit == hoveredUnit)
                return;

            if (lastHoveredUnit != null && lastHoveredUnit != selectedUnit)
                lastHoveredUnit.Outline.Hide();

            lastHoveredUnit = hoveredUnit;

            if (hoveredUnit == null || hoveredUnit == selectedUnit)
                return;

            var isAttackTarget = selectedUnit != null && hoveredUnit.CurrentState.Team != _activeTeam;
            var color = isAttackTarget ? OutlineColor.Attack : OutlineColor.Neutral;

            hoveredUnit.Outline.Show(color, OutlineThickness.Thin);
        }

        private void HandleStateChange(ChangeEvent<State> changeEvent)
        {
            _activeTeam = changeEvent.NewValue.Team;
        }
    }
}
