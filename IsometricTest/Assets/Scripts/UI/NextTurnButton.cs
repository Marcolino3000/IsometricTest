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
        private UnitStateManager _unitStateManager;

        public void Setup(GameStateManager gameStateManager, InputHandler inputHandler,
            MatchOutcomeWatcher outcomeWatcher, UnitStateManager unitStateManager)
        {
            _gameStateManager = gameStateManager;
            _inputHandler = inputHandler;
            _outcomeWatcher = outcomeWatcher;
            _unitStateManager = unitStateManager;
            _button.clicked += EndTurn;
            // The key is the button's shortcut, so it ends the turn on exactly the same terms.
            _inputHandler.EndTurnPressed += EndTurn;
            // Two things colour the button and they come from different places: whose turn it is is
            // turn state, whether anyone can still act is asked of the units.
            gameStateManager.GameStateChanged += HandleStateChange;
            _unitStateManager.Changed += UpdateButtonColor;

            UpdateButtonColor();
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
            UpdateButtonColor();
        }

        /// <summary>
        /// Gold once the active team has nothing left to do, otherwise the team's own colour. Whether
        /// anyone can still act is asked of <see cref="UnitStateManager"/> rather than read off turn
        /// state: it is derived from the units, so it is never a cached copy that can disagree with
        /// them - a unit falling is noticed as surely as one spending its last point.
        /// </summary>
        private void UpdateButtonColor()
        {
            if (_button == null)
                return;

            if (_unitStateManager != null && !_unitStateManager.ActiveTeamHasActionsLeft)
            {
                _button.style.backgroundColor = new StyleColor(new Color(1f, 215f/255f, 0f));
                return;
            }

            var team = _gameStateManager.State.Team;
            _button.style.backgroundColor = team == Team.Player ? new StyleColor(Color.green) : new StyleColor(Color.red);
        }

        private void Awake()
        {
            _button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("nextTurnButton");
        }

        private void OnDestroy()
        {
            if (_unitStateManager != null) _unitStateManager.Changed -= UpdateButtonColor;
            if (_gameStateManager == null) return;
            _gameStateManager.GameStateChanged -= HandleStateChange;
            if (_button != null) _button.clicked -= EndTurn;
            if (_inputHandler != null) _inputHandler.EndTurnPressed -= EndTurn;
        }
    }
}