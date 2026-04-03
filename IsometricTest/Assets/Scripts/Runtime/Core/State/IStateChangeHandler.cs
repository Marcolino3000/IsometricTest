using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Core.State
{
    public interface IStateChangeHandler
    {
        public void HandleStateChange(ChangeEvent changeEvent); 
    }
    
    [Serializable]
    public class State
    {
        public Team Team;
        public bool UnitsHaveActionsLeft;
    }

    public class ChangeEvent
    {
        public State previousValue;
        public State newValue;
    }
}