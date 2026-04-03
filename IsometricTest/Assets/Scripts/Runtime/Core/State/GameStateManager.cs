using System;
using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Unity.VisualScripting;
using UnityEngine;

namespace Runtime.Core.State
{
    public class GameStateManager : MonoBehaviour
    {
        public event Action<ChangeEvent> GameStateChanged;
        
        [Header("Current State")]
        [SerializeField] private Team CurrentTeam;

        [Header("References")] 
        [SerializeField] private Selector selector;

        // private List<IStateChangeHandler> stateChangeHandlers;

        // private void Start()
        // {
        //     Setup();
        // }

        public void Setup()
        {
            selector.OnTurnFinished += SwitchActiveTeam;
            
            // FindStateChangeHandlers();
            var changeEvent = new ChangeEvent
            {
                previousValue = new State{Team = CurrentTeam},
                newValue = new State{Team = CurrentTeam}
            };
            // NotifyStateChangeHandlers(changeEvent);
            GameStateChanged?.Invoke(changeEvent);
        }

        private void SwitchActiveTeam()
        {
            var changeEvent = new ChangeEvent{previousValue = new State{Team = CurrentTeam}};
            
            ToggleCurrentTeam();
            
            changeEvent.newValue = new State{Team = CurrentTeam};
            
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
            CurrentTeam = CurrentTeam == Team.Player ? Team.Opponent : Team.Player;
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