using System;

namespace Runtime.Actions
{
    public class AttackExecutor : ExecutorBase<Unit, Move>
    {
        public AttackExecutor(Func<Unit, bool> actionTestArg, Func<Unit, bool> actionArg)
            : base(actionTestArg, actionArg) { }
    }
}