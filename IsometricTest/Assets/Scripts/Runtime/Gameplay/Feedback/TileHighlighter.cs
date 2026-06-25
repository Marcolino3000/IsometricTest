using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class TileHighlighter : MonoBehaviour
    {
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;

        private void Awake()
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(ChangeEvent<Selection> changeEvent)
        {
            TileSpawner.ResetHighlightedTiles();

            var selection = changeEvent.NewValue;

            switch (selection.Status)
            {
                case SelectionStatus.NoSelectionFriendlyHover:
                case SelectionStatus.NoSelectionEnemyHover:
                    selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                case SelectionStatus.SelectionNoHover:
                case SelectionStatus.SelectionFriendlyHover:
                case SelectionStatus.SelectionEnemyClick:
                case SelectionStatus.SelectionTileClick:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                case SelectionStatus.SelectionEnemyHover:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    ShowAttackIndicatorTile(selection.HoveredUnit.CurrentState.Position);
                    break;

                case SelectionStatus.SelectionTileHover:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    break;
            }
        }

        private void ShowAttackIndicatorTile(Tile tile)
        {
            tileSpawner.HighlightTile(tile.Position, MarkerColor.Orange);
        }
    }
}