using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Actions
{
    public class ActionContext
    {
        public Unit Unit;
        public int ActionPoints;
        public int Distance;
        public Unit TargetUnit;
        public Tile TargetTile;
    }
}