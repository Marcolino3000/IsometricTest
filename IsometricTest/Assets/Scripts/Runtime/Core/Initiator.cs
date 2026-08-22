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

        [Tooltip("How a found item is announced: the tall card, symbol under the name and everything " +
                 "centered, or the wide one with the symbol beside it.")]
        [SerializeField] private bool verticalItemPopup;

        [Tooltip("Where the symbol on the merge screen's corner button is cut from. The same table " +
                 "the action point preview reads, since merging is a kind of action too.")]
        [SerializeField] private ActionIconSet actionIcons;


        [Tooltip("Screen-space UI documents. A click that lands on one of them must not raycast into the world.")]
        [SerializeField] private UIDocument[] hudDocuments;

        // Created in SetupReferences, like the attack preview: it holds nothing worth authoring and
        // nothing worth keeping across a restart, only the verdict it reads off the board.
        private MatchOutcomeWatcher matchOutcomeWatcher;


        private void Awake()
        {
            SetupReferences();
            SpawnEntities();
            CenterCameraOnPlayer();
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
            CenterCameraOnPlayer();
            // A restart replaces every unit: the ones the recorded snapshots refer to, and the
            // character the inventory belongs to. Both start over.
            itemManager.Begin(unitSpawner.PlayerUnit);
            actionHistory.Begin();
            StartGame();
        }

        /// <summary>
        /// The match opens looking at the character. Right after <see cref="SpawnEntities"/>, since
        /// that is when it stands where the spawn zone put it, and on a restart too - the new
        /// character lands somewhere else and the camera would otherwise stay where it was left.
        /// </summary>
        private void CenterCameraOnPlayer()
        {
            var player = unitSpawner.PlayerUnit;

            if (player != null)
                inputHandler.CenterCameraOn(player.transform);
        }

        private void SetupUI()
        {
            nextTurnButton.Setup(gameStateManager, inputHandler, matchOutcomeWatcher);
            itemBar.Setup(inputHandler);
            // The rules go in live, like they do into CombatRules: whether the same draught may be
            // carried twice is switchable during play.
            itemManager.Setup(itemBar, CreateItemPopup(), CreateMergeScreen(), gameRules);
            CreateUnitCard();
            CreateGameOverScreen();
        }

        /// <summary>
        /// The card shown while a unit is hovered. Built at runtime on the HUD's panel settings like
        /// the find popup, and lowest of the three cards that sort above the bar: a find and a
        /// verdict are both news, and the hovered unit is only ever the thing being looked at while
        /// they arrive. It listens to the selector itself, so nothing has to push a unit at it, and
        /// it is not rebuilt on a restart - it holds no match state, only what it was last shown.
        /// </summary>
        private void CreateUnitCard()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            UnitCard.Create(hud.panelSettings, hud.sortingOrder + 1).Setup(selector, gameRules);
        }

        /// <summary>
        /// The end screen is built at runtime on the HUD's panel settings like the find popup, and
        /// sorts above it - a match that ends on a find shows both, and the verdict is the news. It
        /// subscribes to the watcher itself, the way the next-turn button subscribes to the state
        /// manager, so nothing has to push a result at it. Not rebuilt on a restart: it holds no
        /// match state, only what it was last told.
        /// </summary>
        private void CreateGameOverScreen()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            GameOverScreen.Create(hud.panelSettings, hud.sortingOrder + 3)
                .Setup(matchOutcomeWatcher, actionHistory.UndoKey);
        }

        /// <summary>
        /// The find popup is built at runtime like the attack preview, so the Systems prefab needs no
        /// further scene object. It borrows the item bar's panel settings, so it scales with the rest
        /// of the HUD, and sorts above it, so a card that reaches the bar is not drawn behind it.
        /// The two layouts differ in nothing but arrangement, so which one is used is a choice here.
        /// It is registered with the input handler, so the press that reads it away is spent on it
        /// and reaches neither the world nor the bar.
        /// </summary>
        private ItemPopup CreateItemPopup()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            var popup = verticalItemPopup
                ? ItemPopup.Create<VerticalItemPopup>(hud.panelSettings, hud.sortingOrder + 2)
                : ItemPopup.Create<ItemPopup>(hud.panelSettings, hud.sortingOrder + 2);

            inputHandler.AddBlocker(popup);

            return popup;
        }

        /// <summary>
        /// The merge workbench is built at runtime like the find popup and borrows the same panel
        /// settings, which is also what keeps a click on its corner button out of the world: the
        /// Raycaster picks every document on the HUD's panel together. It sorts above the other
        /// cards - it is a modal the player opened on purpose, and nothing else can arrive while it
        /// swallows input. Registered with the input handler for that swallowing.
        /// </summary>
        private MergeScreen CreateMergeScreen()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            var screen = MergeScreen.Create(hud.panelSettings, hud.sortingOrder + 4,
                actionIcons != null ? actionIcons.For(ActionKind.Merge) : null);

            inputHandler.AddBlocker(screen);

            return screen;
        }

        private void SetupReferences()
        {
            CombatRules.Setup(gameRules);
            // Before anything asks what can be seen or shot at: the line is walked over the tiles.
            SightRules.Setup(tileSpawner);
            CombatLog.Setup(gameRules);
            gameStateManager.Setup();
            // Early: the selector and the AI are both told to stand down by it, so it has to exist
            // before either is wired.
            CreateMatchOutcomeWatcher();
            unitSpawner.Setup(gameStateManager, selector, fogOfWar, gameRules);
            tileSpawner.Setup(selector, gameRules);
            selector.Setup(gameStateManager, raycaster, unitSpawner, matchOutcomeWatcher);
            raycaster.Setup(inputHandler, hudDocuments);
            outlineManager.Setup(selector);
            CreateAttackPreview();
            actionHistory.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar, aiController, selector,
                lootSpawner, itemManager);
            actionAssigner.Setup(selector);
            fogOfWar.Setup(tileSpawner, unitSpawner, gameStateManager, aiController, gameRules);
            aiController.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar, matchOutcomeWatcher);
            lootSpawner.Setup(tileSpawner, unitSpawner, itemManager, gameStateManager, inputHandler, gameRules);
            Direction.Setup(gameStateManager);
        }
        
        /// <summary>
        /// The watcher of the win and loss conditions. Created here rather than placed on the Systems
        /// prefab because it is nothing but a question asked of the board, and it goes on the
        /// Initiator's own object since it has no transform of its own to speak of. The rules go in
        /// live like everywhere else, so a condition can be switched on and off during play.
        /// </summary>
        private void CreateMatchOutcomeWatcher()
        {
            matchOutcomeWatcher = gameObject.AddComponent<MatchOutcomeWatcher>();
            matchOutcomeWatcher.Setup(gameRules, unitSpawner, tileSpawner, fogOfWar, lootSpawner, gameStateManager);
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