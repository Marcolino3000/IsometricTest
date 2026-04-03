using Runtime.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        private Button _button;

        public void Highlight()
        {
            _button.AddToClassList("highlighted");   
        }
        
        public void ResetHighlight()
        {
            _button.RemoveFromClassList("highlighted");
        }
        
        public void Setup(GameStateManager gameStateManager)
        {
            _button.clicked += gameStateManager.SwitchActiveTeam;
        }
        
        private void Awake()
        {
            _button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("nextTurnButton");
        }
    }
}