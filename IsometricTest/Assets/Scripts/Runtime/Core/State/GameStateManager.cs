using System;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Core.State
{
    public class GameStateManager : MonoBehaviour
    {
        public event Action<ChangeEvent> GameStateChanged;

        [Header("Current State")] 
        public State State;

        [Header("References")] 
        [SerializeField] private Selector selector;

        public void UpdateState()
        {   
            
        }
        public void Setup()
        {
            var changeEvent = new ChangeEvent
            {
                previousValue = new State{Team = State.Team},
                newValue = new State{Team = State.Team}
            };
            // NotifyStateChangeHandlers(changeEvent);
            GameStateChanged?.Invoke(changeEvent);
        }

        public void SwitchActiveTeam()
        {
            var changeEvent = new ChangeEvent{previousValue = new State{Team = State.Team}};
            
            ToggleCurrentTeam();
            
            changeEvent.newValue = new State{Team = State.Team};
            
            // NotifyStateChangeHandlers(changeEvent);
            GameStateChanged?.Invoke(changeEvent);
        }

        // private void NotifyStateChangeHandlers(ChangeEvent changeEvent)
        // {
        //     foreach (var handler in stateChangeHandlers)
        //     {
        //         handler.HandleStateChange(changeEvent);
        //     }
        // }

        #region Helpers

        private void ToggleCurrentTeam()
        {
            State.Team = State.Team == Team.Player ? Team.Opponent : Team.Player;
        }

        // private void FindStateChangeHandlers()
        // {
        //     stateChangeHandlers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        //         .OfType<IStateChangeHandler>()
        //         .ToList();
        //     
        //     stateChangeHandlers.Add(new Direction());
        // }

        #endregion
    }
}