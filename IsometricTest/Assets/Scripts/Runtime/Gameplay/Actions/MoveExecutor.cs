using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Actions
{
    public class MoveExecutor : ExecutorBase<Tile, Move>
    {
        public MoveExecutor(Func<Tile, bool> actionTestArg, Func<Tile, bool> actionArg)
            : base(actionTestArg, actionArg) { }
    }
}