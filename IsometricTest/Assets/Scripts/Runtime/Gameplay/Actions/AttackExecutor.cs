using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Actions
{
    public class AttackExecutor : ExecutorBase<Unit, Attack>
    {
        public AttackExecutor(Func<Unit, bool> actionTestArg, Func<Unit, bool> actionArg)
            : base(actionTestArg, actionArg) { }
    }
}