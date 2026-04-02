using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Core.State
{
    public class Direction : IStateChangeHandler
    {
        public static List<Vector2Int> ForwardAndSides
        {
            get
            {
                return new List<Vector2Int>
                {
                    Forward,
                    Left,
                    Right,
                    ForwardLeft,
                    ForwardRight
                };
            }
        }
        public static Vector2Int Forward => forward;
        public static Vector2Int Back => back;
        public static Vector2Int Left => left;
        public static Vector2Int Right => right;
        public static Vector2Int ForwardLeft => forwardLeft;
        public static Vector2Int ForwardRight => forwardRight;
        public static Vector2Int BackLeft => backLeft;
        public static Vector2Int BackRight => backRight;

        private static Vector2Int forward;
        private static Vector2Int back;
        private static Vector2Int left;
        private static Vector2Int right;
        private static Vector2Int forwardLeft;
        private static Vector2Int forwardRight;
        private static Vector2Int backLeft;
        private static Vector2Int backRight;

        private static void SetContext(State newState)
        {
            switch (newState.Team)
            {
                case Team.Player:
                    forward      = new Vector2Int(0,  1);
                    back         = new Vector2Int(0, -1);
                    left         = new Vector2Int(-1, 0);
                    right        = new Vector2Int(1,  0);
                    forwardLeft  = new Vector2Int(-1, 1);
                    forwardRight = new Vector2Int(1,  1);
                    backLeft     = new Vector2Int(-1, -1);
                    backRight    = new Vector2Int(1,  -1);
                    break;

                case Team.Opponent:
                    forward      = new Vector2Int(0,  -1);
                    back         = new Vector2Int(0,   1);
                    left         = new Vector2Int(1,   0);
                    right        = new Vector2Int(-1,  0);
                    forwardLeft  = new Vector2Int(1,  -1);
                    forwardRight = new Vector2Int(-1, -1);
                    backLeft     = new Vector2Int(1,   1);
                    backRight    = new Vector2Int(-1,  1);
                    break;
            }
        }

        public void HandleStateChange(ChangeEvent changeEvent)
        {
            SetContext(changeEvent.newValue);
        }
    }
}