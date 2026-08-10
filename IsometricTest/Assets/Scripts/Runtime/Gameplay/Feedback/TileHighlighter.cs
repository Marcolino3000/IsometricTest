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
            tileSpawner.ResetHighlightedTiles();

            var selection = changeEvent.NewValue;

            switch (selection.Status)
            {
                case SelectionStatus.NoSelectionFriendlyHover:
                    selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                // Nothing of the player's is selected, so the whole board is about the hovered enemy:
                // where it can walk, and around that the halo of where it can reach without walking.
                case SelectionStatus.NoSelectionEnemyHover:
                    selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
                    selection.HoveredUnit.TileHighlighter.HighlightThreatenedTiles(markReachable: true);
                    break;

                case SelectionStatus.SelectionNoHover:
                case SelectionStatus.SelectionFriendlyHover:
                case SelectionStatus.SelectionEnemyClick:
                case SelectionStatus.SelectionTileClick:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                // The threat goes over the selected unit's own reach rather than under it: the white
                // here is where *you* can go, so a tile that is both is one you can walk into and be
                // shot on, and that is the half worth showing. The attack indicator stays last.
                case SelectionStatus.SelectionEnemyHover:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    selection.HoveredUnit.TileHighlighter.HighlightThreatenedTiles();
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