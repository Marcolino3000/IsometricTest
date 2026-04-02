using Runtime.Entities;

namespace Runtime.Core.State
{
    public interface IStateChangeHandler
    {
        public void HandleStateChange(ChangeEvent changeEvent); 
    }

    public class State
    {
        public Team Team;
    }

    public class ChangeEvent
    {
        public State previousValue;
        public State newValue;
    }
}