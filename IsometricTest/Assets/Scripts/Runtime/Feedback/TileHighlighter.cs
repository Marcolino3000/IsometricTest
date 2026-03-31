using Runtime.Controls;
using UnityEngine;

namespace Runtime
{
    public class TileHighlighter : MonoBehaviour, IStateChangeHandler
    {
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;
        
        private State currentState;
        private Selection currentSelection;
        private Tile currentHighlightedTile;

        private void Awake()
        {
            ClickableRegistry.OnClickableSpawned += RegisterClickableEvents;
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(Selection selection)
        {
            currentSelection = selection;
            
            if(selection.Unit == null)
                TileSpawner.ResetHighlightedTiles();
            else
                selection.Unit.HighlightMoveableTiles();
        }

        private void RegisterClickableEvents(Clickable clickable)
        {
            clickable.OnClick += HandleClick;
            clickable.OnMouseEnter += HandleMouseEnter;
            clickable.OnMouseExit += HandleMouseExit;
        }

        private void HandleUnitClick(Unit unit) 
        {
            
                
        }

        private void HandleTileClick(Tile tile)
        {
            
        }

        private void HandleClick(IClickable clickable)
        {
            switch (clickable)
            {
                case Unit unit:
                    HandleUnitClick(unit);
                    break;
                case Tile tile:
                    HandleTileClick(tile);
                    break;
                default:
                    Debug.LogError("Clicked clickable is not a tile or unit");
                    break;
            }
        }

        private void HandleMouseExit(IClickable clickable)
        {
            if(currentHighlightedTile != null)
            {
                tileSpawner.HighlightTile(currentHighlightedTile.Position, MarkerColor.None);
                currentHighlightedTile = null;
            }
            
            if(currentSelection?.Unit == null)
                TileSpawner.ResetHighlightedTiles();
        }

        private void HandleMouseEnter(IClickable clickable)
        {
            if (clickable is not Unit unit)
                return;

            if(unit.CurrentState.Team == currentState.Team)
                unit.HighlightMoveableTiles();
            
            if (unit.CurrentState.Team != currentState.Team && currentSelection.Unit != null)
                ShowAttackIndicatorTile(unit.CurrentState.Position);
        }

        private void ShowAttackIndicatorTile(Tile tile)
        {
            tileSpawner.HighlightTile(tile.Position, MarkerColor.Orange);
            currentHighlightedTile = tile;
        }

        public void HandleStateChange(State newState)
        {
            currentState = newState;
        }
    }
}