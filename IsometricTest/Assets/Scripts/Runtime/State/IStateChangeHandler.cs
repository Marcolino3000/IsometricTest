namespace Runtime
{
    public interface IStateChangeHandler
    {
        public void HandleStateChange(State newState); 
    }

    public class State
    {
        public Team Team;
    }
     public class GameState : State
     {
         
     }

     public enum GameStateType
     {
         Setup,
         Battle
     }
}