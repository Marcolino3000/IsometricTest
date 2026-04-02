using Runtime.Core.Spawning;
using Runtime.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Feedback
{
    public class TileHighlighter : MonoBehaviour
    {
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;

        private void Awake()
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(Selection selection)
        {
            TileSpawner.ResetHighlightedTiles();
            
            if(selection.SelectedUnit != null)
                HandleUnitSelectedCases(selection);
            
            else
                HandleNoUnitSelectedCases(selection);
        }

        private void HandleNoUnitSelectedCases(Selection selection)
        {
            if(selection.HoveredUnit != null)
                selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
        }

        private void HandleUnitSelectedCases(Selection selection)
        {
            selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
            
            if(selection.HoveredUnit != null && 
               selection.HoveredUnit.CurrentState.Team != selection.SelectedUnit.CurrentState.Team)
                ShowAttackIndicatorTile(selection.HoveredUnit.CurrentState.Position);
        }

        private void ShowAttackIndicatorTile(Tile tile)
        {
            tileSpawner.HighlightTile(tile.Position, MarkerColor.Orange);
        }
    }
}