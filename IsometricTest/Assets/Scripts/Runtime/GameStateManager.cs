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
        [SerializeField] private Raycaster raycaster;
        [SerializeField] private Selector selector;

        private List<IStateChangeHandler> stateChangeHandlers;

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            FindStateChangeHandlers();
            
            selector.OnTurnFinished += SwitchActiveTeam;
            Direction.SetContext(new Context{ Team = CurrentTeam });
        }

        private void SwitchActiveTeam()
        {
            CurrentTeam = CurrentTeam == Team.Player ? Team.Opponent : Team.Player;
            Direction.SetContext(new Context{ Team = CurrentTeam });
            
            NotifyStateChangeHandlers();
        }

        private void FindStateChangeHandlers()
        {
            stateChangeHandlers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IStateChangeHandler>()
                .ToList();
        }

        private void NotifyStateChangeHandlers()
        {
            foreach (var handler in stateChangeHandlers)
            {
                handler.HandleStateChange(new State{Team = CurrentTeam});
            }
        }
    }
}