using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Debugger;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.AI;
using Runtime.Gameplay.Entities;
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

        [Tooltip("How fast the game is drawn - the speed every unit animation runs at. Its own asset " +
                 "rather than a rule: it decides nothing about the match, only how long it takes to " +
                 "watch. Applies immediately, also during play.")]
        [SerializeField] private AnimationSettings animationSettings;

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

        [Tooltip("Owns the view: pan, drag, zoom and how far it may travel. Separate from the input " +
                 "handler, which only reports what was pressed.")]
        [SerializeField] private CameraController cameraController;
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

        // Created here for the same reason: it is a query over the units plus a signal, so it holds
        // no copy of anything and there is nothing on it to author or to restore.
        private UnitStateManager unitStateManager;

        // The one owner of what the cursor is over. Both roads into the game report to it - the
        // world raycast and the item bar - so neither can undo what the other drew.
        private HoverTarget hoverTarget;

        // The lines drawn where one ring of the map ends and the next begins. Built from code like
        // the attack preview, and redrawn with the board: the rings are measured against the grid.
        private ZoneBorder zoneBorder;

        // What says a ring has been reached for the first time. On the Initiator's own object like
        // the outcome watcher, and for the same reason: it is a question asked of where the
        // character stands, so there is nothing on it to author and nothing in it to snapshot.
        private ZoneWatcher zoneWatcher;


        private void Awake()
        {
            SetupReferences();
            SpawnEntities();
            CenterCameraOnPlayer();
            SetupUI();
            itemManager.Begin(unitSpawner.PlayerUnit);
            zoneWatcher.Begin(unitSpawner.PlayerUnit);
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
            // And every ring is news again: a fresh character spawns somewhere else on a fresh map.
            zoneWatcher.Begin(unitSpawner.PlayerUnit);
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
            // The board exists by now, so how far the view may travel can be handed over with it.
            cameraController.SetBounds(tileSpawner.GetBoardBounds());

            var player = unitSpawner.PlayerUnit;

            if (player != null)
                cameraController.CenterOn(player.transform);
        }

        private void SetupUI()
        {
            nextTurnButton.Setup(gameStateManager, inputHandler, matchOutcomeWatcher, unitStateManager);
            itemBar.Setup(inputHandler, hoverTarget);
            // The rules go in live, like they do into CombatRules: whether the same draught may be
            // carried twice is switchable during play.
            itemManager.Setup(itemBar, CreateItemPopup(), CreateMergeScreen(), gameRules, hoverTarget);
            CreateTooltipView();
            CreateGameOverScreen();
            CreateZoneWatcher();
        }

        /// <summary>
        /// What tells the player they have crossed into a new ring of the map, and the screen it
        /// says it on. Both here rather than in <see cref="SetupReferences"/> because the
        /// announcement is a view built on the HUD's panel; the watcher itself holds no match state
        /// and is not rebuilt on a restart, only told to start over.
        /// </summary>
        private void CreateZoneWatcher()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            // Just above the bar and under every card: it is news read while playing on, so it
            // must not cover a card the player opened - and it is dimmed along with the rest when
            // the match ends. It blocks no input either, see AnnouncementScreen.
            var announcements = AnnouncementScreen.Create(hud.panelSettings, hud.sortingOrder + 1);

            zoneWatcher = gameObject.AddComponent<ZoneWatcher>();
            zoneWatcher.Setup(unitSpawner, announcements);

            // What a ring holds arrives when the character walks into it - the units from the
            // spawner that owns them, the boxes from the one that owns those. Wired here rather
            // than either of them watching the board, and said on every step, so a ring emptied by
            // an undo fills again when it is walked into again.
            zoneWatcher.ZoneReached += unitSpawner.ReleaseZone;
            zoneWatcher.ZoneReached += lootSpawner.ReleaseZone;
        }

        /// <summary>
        /// The one card that labels things - a slot, an entry, a unit, a tile, the box on it. Built
        /// at runtime on the HUD's panel settings like the find popup, and sorting above every other
        /// card: it labels the merge screen, which is itself a modal over the rest. It asks the hover
        /// target what the cursor is on, so nothing has to push anything at it, and it is not rebuilt
        /// on a restart - it holds no match state, only what it was last shown.
        /// </summary>
        private void CreateTooltipView()
        {
            var hud = itemBar.GetComponent<UIDocument>();

            TooltipView.Create(hud.panelSettings, hud.sortingOrder + 5).Setup(hoverTarget);
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
                actionIcons != null ? actionIcons.For(ActionKind.Merge) : null)
                .Setup(hoverTarget);

            inputHandler.AddBlocker(screen);

            return screen;
        }

        private void SetupReferences()
        {
            CombatRules.Setup(gameRules, tileSpawner);
            // Before anything asks what can be seen or shot at: the line is walked over the tiles.
            SightRules.Setup(tileSpawner);
            // Before anything is spawned: which ring a tile lies in is what says who guards it and
            // which kinds of box lie on it. The rings are loaded from Resources, so there is nothing
            // to wire and a project without an asset simply has an undivided map.
            ZoneRules.Setup(tileSpawner);
            CombatLog.Setup(gameRules);
            gameStateManager.Setup();
            // Before the spawner: every unit it puts on the board is handed to this to be tracked.
            CreateUnitStateManager();
            // Before the selector and the item bar: both report what the cursor is on to it.
            hoverTarget = gameObject.AddComponent<HoverTarget>();
            // Early: the selector and the AI are both told to stand down by it, so it has to exist
            // before either is wired.
            CreateMatchOutcomeWatcher();
            unitSpawner.Setup(gameStateManager, selector, fogOfWar, gameRules, animationSettings,
                unitStateManager);
            tileSpawner.Setup(selector, gameRules);
            selector.Setup(gameStateManager, raycaster, unitSpawner, matchOutcomeWatcher, hoverTarget);
            raycaster.Setup(inputHandler, hudDocuments);
            cameraController.Setup(inputHandler);
            outlineManager.Setup(selector);
            CreateAttackPreview();
            zoneBorder = ZoneBorder.Create(tileSpawner, gameRules, fogOfWar);
            actionHistory.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar, aiController, selector,
                lootSpawner, itemManager);
            actionAssigner.Setup(selector, hoverTarget);
            fogOfWar.Setup(tileSpawner, unitSpawner, gameStateManager, aiController, gameRules);
            aiController.Setup(gameStateManager, unitSpawner, tileSpawner, fogOfWar, matchOutcomeWatcher);
            lootSpawner.Setup(tileSpawner, unitSpawner, itemManager, gameStateManager, inputHandler, gameRules);
            Direction.Setup(gameStateManager);
        }
        
        /// <summary>
        /// Bundles the units' state for whoever asks about a whole team. On the Initiator's own object
        /// like the outcome watcher, and for the same reason: it is nothing but a question asked of
        /// the units, so there is nothing to author on it and nothing in it to snapshot.
        /// </summary>
        private void CreateUnitStateManager()
        {
            unitStateManager = gameObject.AddComponent<UnitStateManager>();
            unitStateManager.Setup(gameStateManager);
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
            // With the grid standing and before anything is put on it: the rings are measured
            // against the board, so a restart that lays out fresh terrain redraws their borders.
            zoneBorder.Rebuild();
            unitSpawner.SpawnUnits();
            lootSpawner.SpawnLootboxes();
        }

        private void StartGame()
        {
            gameStateManager.ToggleCurrentTeam();
        }
    }
}