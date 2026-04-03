using Runtime.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class NextTurnButton : MonoBehaviour
    {
        // private GameStateManager _gameStateManager;
        private Button _button;

        public void Setup(GameStateManager gameStateManager)
        {
            // _gameStateManager = gameStateManager;
            _button.clicked += gameStateManager.SwitchActiveTeam;
        }
        
        private void Awake()
        {
            _button = GetComponent<UIDocument>().rootVisualElement.Q<Button>("nextTurnButton");
        }
    }
}