using System;
using System.Collections.Generic;

namespace Runtime.Gameplay.Actions
{
    public abstract class ExecutorBase<Target, ActionType> where ActionType : UnitAction
    {
        protected readonly Func<Target, bool> _actionTest;
        protected readonly Func<Target, bool> _action;

        protected ExecutorBase(Func<Target, bool> actionTestArg, Func<Target, bool> actionArg)
        {
            _actionTest = actionTestArg;
            _action = actionArg;
        }

        public bool CheckActionValidity(List<UnitAction> actions, Target target)
        {
            foreach (var action in actions)
            {
                if (action is ActionType)
                {
                    if (target != null && !_actionTest.Invoke(target))
                        return false;
                }
            }

            return true;
        }
        
        public void Execute(Target target)
        {
            _action.Invoke(target);
        }
    }
}