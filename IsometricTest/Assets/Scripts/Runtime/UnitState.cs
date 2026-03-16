using System;

namespace Runtime
{
    [Serializable]
    public class UnitState
    {
        public int Health;
        public Tile Position;
        public int Range;
        public Team Team;

        public UnitState(UnitState other)
        {
            Health = other.Health;
            Position = null;
            Range = other.Range;
            Team = other.Team;
        }
    }

    
}