using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        private Button _button;
        private GameStateManager _gameStateManager;
        private InputHandler _inputHandler;
        private MatchOutcomeWatcher _outcomeWatcher;

        public void Setup(GameStateManager gameStateManager, InputHandler inputHandler,
            MatchOutcomeWatcher outcomeWatcher)
        {
            _gameStateManager = gameStateManager;
            _inputHandler = inputHandler;
            _outcomeWatcher = outcomeWatcher;
            _button.clicked += EndTurn;
            // The key is the button's shortcut, so it ends the turn on exactly the same terms.
            _inputHandler.EndTurnPressed += EndTurn;
            gameStateManager.GameStateChanged += HandleStateChange;
        }

        /// <summary>
        /// Hands the turn over, unless the match has been decided - a settled match has no next turn,
        /// and letting one start would only bury the deciding action under further ones to undo. This
        /// button owns ending the player's turn, so it is the one place that has to know.
        /// </summary>
        private void EndTurn()
        {
            if (_outcomeWatcher != null && _outcomeWatcher.IsOver)
                return;

            _gameStateManager.ToggleCurrentTeam();
        }

        private void HandleStateChange(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            UpdateButtonColor(changeEvent);
        }

        private void UpdateButtonColor(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            if (!changeEvent.NewValue.UnitsHaveActionsLeft)
            {
                _button.style.backgroundColor = new StyleColor(new Color(1f, 215f/255f, 0f));
                return;
            }

            var team = changeEvent.NewValue.Team;
            _button.style.backgroundColor = team == Team.Player ? new StyleColor(Color.green) : new StyleColor(Color.red);
        }

        private void Awake()
        {
            _button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("nextTurnButton");
        }

        private void OnDestroy()
        {
            if (_gameStateManager == null) return;
            _gameStateManager.GameStateChanged -= HandleStateChange;
            if (_button != null) _button.clicked -= EndTurn;
            if (_inputHandler != null) _inputHandler.EndTurnPressed -= EndTurn;
        }
    }
}