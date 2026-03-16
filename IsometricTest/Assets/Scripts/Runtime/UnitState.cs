using System;
using UnityEngine;

namespace Runtime
{
    [Serializable]
    public struct UnitState
    {
        public int Health;
        public Tile Position;
        public int Range;
        public Team Team;
    }
}