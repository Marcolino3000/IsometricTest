using Runtime.Core.Spawning;
using UnityEngine;

namespace Runtime.Entities
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
        
        #region Helpers

        public void Setup(UnitState unitState, TileSpawner tileSpawner)
        {
            state = unitState;
            _tileSpawner = tileSpawner;
        }

        #endregion
    }
}