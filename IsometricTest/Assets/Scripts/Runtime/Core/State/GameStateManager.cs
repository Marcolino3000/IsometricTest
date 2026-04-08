using System;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Unity.VisualScripting;
using UnityEngine;

namespace Runtime.Core.State
{
    public class GameStateManager : MonoBehaviour
    {
        public event Action<ChangeEvent<State>> OnGameStateChanged;

        [Header("Current State")] 
        public State State { get; private set; }
       
        [Header("References")] 
        [SerializeField] private Selector selector;

        private State previousState;

        public void SetActionsLeft(bool teamHasActionsLeft)
        {
            State.UnitsHaveActionsLeft = teamHasActionsLeft;
            
            HandleStateChange();
        }
        
        private void HandleStateChange()
        {
            var changeEvent = new ChangeEvent<State>(previousState.Clone(), State.Clone());
            
            OnGameStateChanged?.Invoke(changeEvent);

            previousState = State.Clone();
        }
        
        public void Setup()
        {
            State = new State
            {
                Team = Team.Opponent,
                UnitsHaveActionsLeft = true
            };

            previousState = State.Clone();
            
            HandleStateChange();
            // State.OnStateChanged += HandleStateChange;
        }
        
        #region Helpers

        public void ToggleCurrentTeam()
        {
            State.Team = State.Team == Team.Player ? Team.Opponent : Team.Player;
            
            HandleStateChange();
        }
        #endregion
    }
}