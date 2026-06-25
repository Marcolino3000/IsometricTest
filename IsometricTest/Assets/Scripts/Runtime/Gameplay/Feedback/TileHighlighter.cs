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
                    // ShowMovePath(selection.SelectedUnit, selection.HoveredTile);
                    break;
            }
        }

        private void ShowAttackIndicatorTile(Tile tile)
        {
            tileSpawner.HighlightTile(tile.Position, MarkerColor.Orange);
        }

        // Marks every tile the unit would walk through to reach the hovered tile.
        // Tiles within the unit's movement range are solid green; any path tiles beyond
        // what it can move are transparent green.
        private void ShowMovePath(Unit unit, Tile target)
        {
            var path = tileSpawner.GetPath(unit.CurrentState.Position, target);
            if (path == null)
                return;

            var range = unit.CurrentState.Range;

            // path[0] is the unit's current tile, so the index equals the number of steps.
            for (var step = 1; step < path.Count; step++)
            {
                var markerColor = step <= range ? MarkerColor.Green : MarkerColor.Blue;
                tileSpawner.HighlightTile(path[step].Position, markerColor);
            }
        }
    }
}