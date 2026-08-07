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

        public void Setup(GameStateManager gameStateManager, InputHandler inputHandler)
        {
            _gameStateManager = gameStateManager;
            _inputHandler = inputHandler;
            _button.clicked += gameStateManager.ToggleCurrentTeam;
            // The key is the button's shortcut, so it ends the turn on exactly the same terms.
            _inputHandler.EndTurnPressed += gameStateManager.ToggleCurrentTeam;
            gameStateManager.GameStateChanged += HandleStateChange;
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
            if (_button != null) _button.clicked -= _gameStateManager.ToggleCurrentTeam;
            if (_inputHandler != null) _inputHandler.EndTurnPressed -= _gameStateManager.ToggleCurrentTeam;
        }
    }
}