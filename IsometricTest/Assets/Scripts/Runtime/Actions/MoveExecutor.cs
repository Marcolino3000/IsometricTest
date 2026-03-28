using System;

namespace Runtime.Actions
{
    public class MoveExecutor : ExecutorBase<Tile, Move>
    {
        public MoveExecutor(Func<Tile, bool> actionTestArg, Func<Tile, bool> actionArg)
            : base(actionTestArg, actionArg) { }
    }
}