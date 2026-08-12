using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Actions
{
    public class ActionContext
    {
        public Unit Unit;
        public int ActionPoints;
        public Unit TargetUnit;
        public Tile TargetTile;

        /// <summary>
        /// The tile the action is performed from, which is not always the one the unit stands on: an
        /// attack planned behind a walk is made from the end of that walk. Null where it makes no
        /// difference; the unit's own tile is then the answer.
        ///
        /// It replaced a precomputed distance, which was the same thing said twice and could disagree
        /// with the tile the range and the line of fire were measured from.
        /// </summary>
        public Tile FromTile;
    }
}