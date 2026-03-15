using System;
using UnityEngine;

namespace Runtime
{
    [Serializable]
    public class UnitState
    {
        public int Health;
        public Vector2Int Position;
        public int Range;
        public Team Team;
    }
}