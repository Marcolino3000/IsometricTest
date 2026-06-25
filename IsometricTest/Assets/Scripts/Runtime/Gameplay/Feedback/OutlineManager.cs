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

        public void Setup(Selector selector)
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(ChangeEvent<Selection> changeEvent)
        {
            var selection = changeEvent.NewValue;

            UpdateSelectedOutline(selection.SelectedUnit);
            UpdateHoveredOutline(selection);
        }


        // The selected unit keeps a white outline until a different unit is selected or it is deselected.
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
        
        private void UpdateHoveredOutline(Selection selection)
        {
            if (lastHoveredUnit == selection.HoveredUnit)
                return;

            if (lastHoveredUnit != null && lastHoveredUnit != selection.SelectedUnit)
                lastHoveredUnit.Outline.Hide();

            lastHoveredUnit = selection.HoveredUnit;

            switch (selection.Status)
            {
                case SelectionStatus.NoSelectionFriendlyHover:
                case SelectionStatus.NoSelectionEnemyHover:
                case SelectionStatus.SelectionFriendlyHover:
                    selection.HoveredUnit.Outline.Show(OutlineColor.Neutral, OutlineThickness.Thin);
                    break;
                case SelectionStatus.SelectionEnemyHover:
                    selection.HoveredUnit.Outline.Show(OutlineColor.Attack, OutlineThickness.Thin);
                    break;
            }
        }
    }
}
