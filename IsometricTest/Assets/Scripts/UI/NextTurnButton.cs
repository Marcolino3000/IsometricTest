using Runtime.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        private Button _button;

        private void Highlight(ChangeEvent changeEvent)
        {
            if(!changeEvent.newValue.UnitsHaveActionsLeft)
                _button.AddToClassList("highlighted");   
        }
        
        private void ResetHighlight(ChangeEvent changeEvent)
        {
            if(changeEvent.previousValue.Team != changeEvent.newValue.Team)
                _button.RemoveFromClassList("highlighted");
        }
        
        public void Setup(GameStateManager gameStateManager)
        {
            _button.clicked += gameStateManager.ToggleCurrentTeam;
            gameStateManager.OnGameStateChanged += HandleStateChange;
        }

        private void HandleStateChange(ChangeEvent changeEvent)
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