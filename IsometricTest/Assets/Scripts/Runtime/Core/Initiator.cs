using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Global;
using UI;
using UnityEngine;

namespace Runtime.Core
{
    public class Initiator : MonoBehaviour
    {
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private Raycaster raycaster;
        [SerializeField] private Selector selector;
        
        [Header("UI")]
        [SerializeField] private NextTurnButton nextTurnButton;

        private void Awake()
        {
            SetupReferences();
            SpawnEntities();
            SetupUI();
            StartGame();
        }

        private void SetupUI()
        {
            nextTurnButton.Setup(gameStateManager);
        }

        private void SetupReferences()
        {
            gameStateManager.Setup();
            unitSpawner.Setup(gameStateManager, selector);
            tileSpawner.Setup(selector);
            selector.Setup(gameStateManager);
            Direction.Setup(gameStateManager);
        }

        private void SpawnEntities()
        {
            tileSpawner.SpawnTiles();
            unitSpawner.SpawnUnits();
        }

        private void StartGame()
        {
            gameStateManager.ToggleCurrentTeam();
        }
    }
}