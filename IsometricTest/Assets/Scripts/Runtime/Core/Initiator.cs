using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Debugger;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.AI;
using Runtime.Gameplay.Feedback;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;
using Runtime.Gameplay.Items;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Runtime.Core
{
    public class Initiator : MonoBehaviour
    {
        [Header("Rules")]
        [Tooltip("Match-wide rule switches (retaliation, ...). Toggling one applies immediately, also during play.")]
        [SerializeField] private GameRules gameRules;

        [Header("References")]
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
        [SerializeField] private ActionHistory actionHistory;
        [SerializeField] private ItemManager itemManager;
        [SerializeField] private LootSpawner lootSpawner;

        [Header("UI")]
        [SerializeField] private NextTurnButton nextTurnButton;
        [SerializeField] private ItemBar itemBar;


        [Tooltip("Screen-space UI documents. A click that lands on one of them must not raycast into the world.")]
        [SerializeField] private UIDocument[] hudDocuments;


        private void Awake()
        {
            SetupReferences();
            SpawnEntities();
            SetupUI();
            itemManager.Begin(unitSpawner.PlayerUnit);
            actionHistory.Begin();
            StartGame();
        }

        /// <summary>
        /// Resets the match to its starting conditions at runtime: clears the current selection,
        /// resets game state, then despawns and respawns all tiles and units.
        /// Deliberately skips <see cref="SetupReferences"/> and <see cref="SetupUI"/> so the
        /// persistent singletons (selector, outline manager, UI, ...) don't re-subscribe to events.
        /// </summary>
        [ContextMenu("Restart")]
        [DebugHotkey(Key.R, HotkeyMods.None, "Restart match")]
        public void Restart()
        {
            selector.ResetSelection();
            gameStateManager.Setup();
            fogOfWar.ResetExploration();
            SpawnEntities();
            // A restart replaces every unit: the ones the recorded snapshots refer to, and the
            // character the inventory belongs to. Both start over.
            itemManager.Begin(unitSpawner.PlayerUnit);
            actionHistory.Begin();
            StartGame();
        }

        private void SetupUI()
        {
            nextTurnButton.Setup(gameStateManager, inputHandler);
            itemBar.Setup(inputHandler);
            itemManager.Setup(itemBar);
        }

        private void SetupReferences()
        {
            CombatRules.Setup(gameRules);
            gameStateManager.Setup();
            unitSpawner.Setup(gameStateManager, selector, fogOfWar);
            tileSpawner.Setup(selector);
            selector.Setup(gameStateManager, raycaster, unitSpawner);
            raycaster.Setup(inputHandler, hudDocuments);
            outlineManager.Setup(selector);
            CreateAttackPreview();
            actionHistory.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar, aiController, selector,
                lootSpawner, itemManager);
            actionAssigner.Setup(selector);
            fogOfWar.Setup(tileSpawner, unitSpawner, gameStateManager, aiController, gameRules);
            aiController.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar);
            lootSpawner.Setup(tileSpawner, unitSpawner, itemManager, gameStateManager, inputHandler);
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

        /// <summary>
        /// Loot goes down last: the boxes avoid tiles somebody stands on, so the units have to be
        /// placed before they are scattered.
        /// </summary>
        private void SpawnEntities()
        {
            tileSpawner.SpawnTiles();
            unitSpawner.SpawnUnits();
            lootSpawner.SpawnLootboxes();
        }

        private void StartGame()
        {
            gameStateManager.ToggleCurrentTeam();
        }
    }
}