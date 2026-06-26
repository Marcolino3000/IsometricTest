using System;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Unity.VisualScripting;
using UnityEngine;

namespace Runtime.Core.State
{
    public class GameStateManager : MonoBehaviour
    {
        /// <summary>
        /// Phase 1 of a turn change: prepares per-turn world state for the new active team — units
        /// refresh action points, fog and direction recompute, selection clears. Everything the turn's
        /// actor (the AI) depends on subscribes here, so it is ready before <see cref="TurnStarted"/>.
        /// Fires only on an actual team change, not on mid-turn updates such as SetActionsLeft.
        /// </summary>
        public event Action<ChangeEvent<State>> TurnReset;

        /// <summary>
        /// Phase 2: the prepared turn is now live and its actor may act (the AI plays its units).
        /// Fires only on an actual team change, immediately after <see cref="TurnReset"/>.
        /// </summary>
        public event Action<ChangeEvent<State>> TurnStarted;

        /// <summary>
        /// Any state change, including mid-turn ones such as SetActionsLeft. For observers that mirror
        /// state regardless of turn boundaries (e.g. the next-turn button).
        /// </summary>
        public event Action<ChangeEvent<State>> GameStateChanged;

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

            if (changeEvent.PreviousValue.Team != changeEvent.NewValue.Team)
            {
                TurnReset?.Invoke(changeEvent);

                TurnStarted?.Invoke(changeEvent);
            }

            GameStateChanged?.Invoke(changeEvent);

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
        }
        
        #region Helpers

        public void ToggleCurrentTeam()
        {
            State.Team = State.Team == Team.Player ? Team.Opponent : Team.Player;
            State.UnitsHaveActionsLeft = true;
            
            HandleStateChange();
        }
        #endregion
    }
}