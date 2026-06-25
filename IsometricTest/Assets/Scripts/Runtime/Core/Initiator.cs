using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Feedback;
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
        [SerializeField] private OutlineManager outlineManager;
        [SerializeField] private ActionAssigner actionAssigner;
        
        [Header("UI")]
        [SerializeField] private NextTurnButton nextTurnButton;

        private void Awake()
        {
            SetupReferences();
            SpawnEntities();
            SetupUI();
            StartGame();
        }

        /// <summary>
        /// Resets the match to its starting conditions at runtime: clears the current selection,
        /// resets game state, then despawns and respawns all tiles and units.
        /// Deliberately skips <see cref="SetupReferences"/> and <see cref="SetupUI"/> so the
        /// persistent singletons (selector, outline manager, UI, ...) don't re-subscribe to events.
        /// </summary>
        [ContextMenu("Restart")]
        public void Restart()
        {
            selector.ResetSelection();
            gameStateManager.Setup();
            SpawnEntities();
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
            outlineManager.Setup(selector, gameStateManager);
            actionAssigner.Setup(selector);
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