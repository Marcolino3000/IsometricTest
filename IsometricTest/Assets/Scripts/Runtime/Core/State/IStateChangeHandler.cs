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
        public T previousValue;
        public T newValue;

        // Convenience properties (some callers use PascalCase)
        public T PreviousValue { get => previousValue; set => previousValue = value; }
        public T NewValue { get => newValue; set => newValue = value; }

        // Construct with previous/new values
        public ChangeEvent() { }

        public ChangeEvent(T previous, T @new)
        {
            previousValue = previous;
            newValue = @new;
        }
    }
}