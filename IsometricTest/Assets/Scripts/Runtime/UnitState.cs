using System;
using UnityEngine;

namespace Runtime
{
    [Serializable]
    public struct UnitState
    {
        public int Health;
        public Vector2Int Position;
        public int Range;
        public Team Team;
    }
}