using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Runtime
{
    public class GameStateManager : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private Team CurrentTeam;

        [Header("References")] 
        [SerializeField] private Selector selector;

        private List<IStateChangeHandler> stateChangeHandlers;

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            selector.OnTurnFinished += SwitchActiveTeam;
            
            FindStateChangeHandlers();
            NotifyStateChangeHandlers();
        }

        private void SwitchActiveTeam()
        {
            ToggleCurrentTeam();
            NotifyStateChangeHandlers();
        }

        private void NotifyStateChangeHandlers()
        {
            foreach (var handler in stateChangeHandlers)
            {
                handler.HandleStateChange(new State{Team = CurrentTeam});
            }
        }

        #region Helpers

        private void ToggleCurrentTeam()
        {
            CurrentTeam = CurrentTeam == Team.Player ? Team.Opponent : Team.Player;
        }

        private void FindStateChangeHandlers()
        {
            stateChangeHandlers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IStateChangeHandler>()
                .ToList();
            
            stateChangeHandlers.Add(new Direction());
        }

        #endregion
    }
}