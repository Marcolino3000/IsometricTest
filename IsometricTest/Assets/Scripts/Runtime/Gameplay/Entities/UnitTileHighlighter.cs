using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Feedback;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class UnitTileHighlighter : UnitComponent
    {
        [Header("References")]
        [SerializeField] private TileSpawner _tileSpawner;
        
        private UnitState state;

        public void HighlightMoveableTiles()
        {
            _tileSpawner.HighlightMoveableTiles(state.Position.Position, state.Range);
        }
        
        public void HighlightTilesAlongPath(List<Tile> path, int movementLimitReachedIndex)
        {
            // path[0] is the unit's own tile, so start at the first step.
            for (var i = 1; i < path.Count; i++)
            {
                var withinReach = movementLimitReachedIndex < 0 || i <= movementLimitReachedIndex;
                var markerColor = withinReach ? MarkerColor.Blue : MarkerColor.TransparentBlue;
                _tileSpawner.HighlightTile(path[i].Position, markerColor);
            }
        }
        
        #region Helpers

        public void Setup(UnitState unitState, TileSpawner tileSpawner)
        {
            state = unitState;
            _tileSpawner = tileSpawner;
        }

        #endregion
    }
}