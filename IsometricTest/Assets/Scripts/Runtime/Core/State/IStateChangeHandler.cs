using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Core.State
{
    // public interface IStateChangeHandler
    // {
    //     public void HandleStateChange(ChangeEvent<T> changeEvent); 
    // }
    
    [Serializable]
    public class State
    { 
        public Team Team;
        public bool UnitsHaveActionsLeft;
        
        public State Clone()
        {
            return new State
            {
                Team = Team,
                UnitsHaveActionsLeft = UnitsHaveActionsLeft
            };
        }
    }

    public class ChangeEvent<T>
    {
        public ChangeEvent (T previousValue, T newValue)
        {
            PreviousValue = previousValue;
            NewValue = newValue;
        }

        public readonly T PreviousValue;
        public readonly T NewValue;
    }
}