using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class UnitTileHighlighter : UnitComponent
    {
        [Header("References")]
        [SerializeField] private TileSpawner _tileSpawner;

        private Unit owner;
        private GameRules rules;

        private UnitState state => owner != null ? owner.CurrentState : null;

        /// <summary>
        /// Every white tile on the board goes through here, so one switch covers the reach of a
        /// selected unit, of a hovered one and the reach drawn over the threat zone. Unlike the
        /// threat overlay this predates its switch, so a missing rules asset leaves it on rather
        /// than blanking a board that always had it.
        /// </summary>
        private bool showMovementRange => rules == null || rules.ShowMovementRange;

        public void HighlightMoveableTiles()
        {
            if (!showMovementRange)
                return;

            var moveableTiles = _tileSpawner.GetMoveableTiles(state);

            foreach (var tile in moveableTiles)
                _tileSpawner.HighlightTile(tile, MarkerColor.TransparentWhite);
        }

        /// <summary>
        /// Every tile this unit could strike if the next turn were its own - anywhere its weapon
        /// reaches from anywhere it could walk to. Measured against the points it starts a turn with
        /// rather than the ones it has left, since on the player's turn an enemy has usually spent
        /// every one of them and would threaten nothing at all. The whole overlay hangs off one
        /// switch, so a board with it off looks exactly as it did before there was one.
        ///
        /// <paramref name="markReachable"/> paints the tiles it could actually stand on over the
        /// top. The reach is a subset of the threat, so putting it on top leaves the threat showing
        /// as a halo and both facts survive on one board - white is where it can walk, orange is
        /// where it can hit you without walking there. Only worth it where the white on the board
        /// means *this* unit: while one of the player's own is selected the white is that unit's
        /// reach, and the danger has to win over it instead. It follows the same switch as every
        /// other white tile, so with the reach overlay off the threat stays orange throughout.
        /// </summary>
        public void HighlightThreatenedTiles(bool markReachable = false)
        {
            if (rules == null || !rules.ShowThreatZone || owner == null || !owner.IsAlive)
                return;

            // Worked out once and handed on: it is half the answer to what is threatened, and each
            // sweep of it costs a pathfinding search per tile of the board.
            var reachable = _tileSpawner.GetMoveableTiles(state, owner.MaxActionPoints);

            foreach (var tile in _tileSpawner.GetThreatenedTiles(owner, reachable))
                _tileSpawner.HighlightTile(tile, MarkerColor.TransparentOrange);

            if (!markReachable || !showMovementRange)
                return;

            foreach (var tile in reachable)
                _tileSpawner.HighlightTile(tile, MarkerColor.TransparentWhite);
        }

        public void HighlightTilesAlongPath(List<Tile> path, int movementLimitReachedIndex)
        {
            // path[0] is the unit's own tile, so start at the first step.
            for (var i = 1; i < path.Count; i++)
            {
                var withinReach = movementLimitReachedIndex < 0 || i <= movementLimitReachedIndex;
                var markerColor = withinReach ? MarkerColor.Blue : MarkerColor.TransparentBlue;
                _tileSpawner.HighlightTile(path[i], markerColor);
            }
        }

        #region Helpers

        /// <summary>
        /// Takes the unit itself rather than its state: the threat overlay needs the points it starts
        /// a turn with, which only the unit knows, and the rules switch that decides whether it is
        /// drawn at all rides along instead of being wired into the scene a second time.
        /// </summary>
        public void Setup(Unit unit, TileSpawner tileSpawner, GameRules gameRules)
        {
            owner = unit;
            _tileSpawner = tileSpawner;
            rules = gameRules;
        }

        #endregion
    }
}
