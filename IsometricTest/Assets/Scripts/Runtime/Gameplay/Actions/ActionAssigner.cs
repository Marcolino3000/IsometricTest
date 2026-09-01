using Runtime.Core.State;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    public class ActionAssigner : MonoBehaviour
    {
        // Asked before the world clears a preview: the cursor leaving the map is also the cursor
        // arriving on the item bar, and what the bar put up must survive that.
        private HoverTarget hoverTarget;

        public void Setup(Selector selector, HoverTarget hover)
        {
            hoverTarget = hover;
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
                    // Nothing under the cursor in the world - unless it is on the bar, which owns
                    // the preview while it is and shows what the hovered item would cost.
                    if (hoverTarget == null || !hoverTarget.UiHasCursor)
                        selection.NewValue.SelectedUnit.ActionExecutor.ClearPreview();
                    break;
            }
        }
    }
}