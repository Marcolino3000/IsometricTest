using Runtime.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        private Button _button;

        private void Highlight(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            if(!changeEvent.NewValue.UnitsHaveActionsLeft)
                _button.AddToClassList("highlighted");   
        }
        
        private void ResetHighlight(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            if(changeEvent.PreviousValue.Team != changeEvent.NewValue.Team)
                _button.RemoveFromClassList("highlighted");
        }
        
        public void Setup(GameStateManager gameStateManager)
        {
            _button.clicked += gameStateManager.ToggleCurrentTeam;
            gameStateManager.OnGameStateChanged += HandleStateChange;
        }

        private void HandleStateChange(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            Highlight(changeEvent);
            ResetHighlight(changeEvent);
        }

        private void Awake()
        {
            _button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("nextTurnButton");
        }
    }
}