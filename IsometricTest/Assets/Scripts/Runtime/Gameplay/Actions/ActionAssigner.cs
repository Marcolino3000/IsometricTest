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
                case SelectionStatus.SelectionTileHover:
                    selection.NewValue.SelectedUnit.ActionExecutor.PlanMoveAction(new ExecuteArgs(selection.NewValue.HoveredTile, null));
                    break;
                case SelectionStatus.SelectionEnemyHover:
                    selection.NewValue.SelectedUnit.ActionExecutor.PlanAttackAction(new ExecuteArgs(null, selection.NewValue.HoveredUnit));
                    break;
                case SelectionStatus.SelectionTileClick:
                    selection.NewValue.SelectedUnit.ActionExecutor.ExecuteMoveActions(new ExecuteArgs(selection.NewValue.ClickedTile, null));
                    break;
                case SelectionStatus.SelectionEnemyClick:
                    selection.NewValue.SelectedUnit.ActionExecutor.ExecuteAttackAction(new ExecuteArgs(null, selection.NewValue.ClickedUnit));
                    break;
                case SelectionStatus.SelectionNoHover:
                    selection.NewValue.SelectedUnit.ActionExecutor.ClearPreview();
                    break;
            }
        }
    }
}