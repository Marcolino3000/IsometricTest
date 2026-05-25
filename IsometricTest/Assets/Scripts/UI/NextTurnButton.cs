using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        private Button _button;

        public void Setup(GameStateManager gameStateManager)
        {
            _button.clicked += gameStateManager.ToggleCurrentTeam;
            gameStateManager.OnGameStateChanged += HandleStateChange;
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
    }
}