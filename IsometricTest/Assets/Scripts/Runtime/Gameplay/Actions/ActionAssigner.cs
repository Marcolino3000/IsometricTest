using Runtime.Core.State;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    public class ActionAssigner : MonoBehaviour
    {
        public void Setup(Selector selector)
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(ChangeEvent<Selection> selection)
        {
            switch (selection.NewValue.Status)
            {
                case SelectionStatus.SelectionEnemyHover:
                    selection.NewValue.SelectedUnit.ActionExecutor.PlanActionsNew(new ExecuteArgs(null, selection.NewValue.HoveredUnit));
                    break;
                case SelectionStatus.SelectionEnemyClick:
                    selection.NewValue.SelectedUnit.ActionExecutor.ExecuteActions(new ExecuteArgs(null, selection.NewValue.ClickedUnit));
                    break;
            }
        }
    }
}