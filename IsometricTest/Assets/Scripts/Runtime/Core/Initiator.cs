using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.AI;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Fog;
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
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private Raycaster raycaster;
        [SerializeField] private Selector selector;
        [SerializeField] private OutlineManager outlineManager;
        [SerializeField] private ActionAssigner actionAssigner;
        [SerializeField] private FogOfWar fogOfWar;
        [SerializeField] private AiController aiController;

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
            fogOfWar.ResetExploration();
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
            unitSpawner.Setup(gameStateManager, selector, fogOfWar);
            tileSpawner.Setup(selector);
            selector.Setup(gameStateManager, raycaster);
            raycaster.Setup(inputHandler);
            outlineManager.Setup(selector);
            CreateAttackPreview();
            actionAssigner.Setup(selector);
            fogOfWar.Setup(tileSpawner, unitSpawner, gameStateManager);
            aiController.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar);
            Direction.Setup(gameStateManager);
        }

        /// <summary>
        /// The attack preview (ghost + red attack line) is created at runtime like the floating
        /// text popups, so the Systems prefab needs no extra scene object. Unparented on purpose:
        /// the ghost copies world-space unit scales, which a scaled parent would distort.
        /// </summary>
        private void CreateAttackPreview()
        {
            var attackPreview = new GameObject("AttackPreview").AddComponent<AttackPreview>();
            attackPreview.Setup(selector, tileSpawner);
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