using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.AI;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Gameplay.History
{
    /// <summary>
    /// Records every action taken since the match started and lets the game move back and forth along
    /// that record: Page Up steps back and Page Down steps forward while the Game view has focus, and
    /// the Action History window can jump straight to any point.
    ///
    /// Every step restores a whole <see cref="GameSnapshot"/> rather than inverting the actions in
    /// between. Actions only announce themselves through <see cref="ActionReporter"/>; the outcome is
    /// read off the snapshots. So a mechanic added later becomes undoable as soon as it reports, and
    /// randomness (critical hits) is reproduced exactly instead of re-rolled on redo.
    ///
    /// Created at runtime by the <see cref="Runtime.Core.Initiator"/>, so it needs no scene object.
    /// </summary>
    public class ActionHistory : MonoBehaviour
    {
        [Header("Shortcuts")]
        [Tooltip("Steps one action back. Plain keys on purpose: the editor claims Ctrl/Cmd+Z for its " +
                 "own undo, so it never reaches the game. Only fires while the Game view has focus.")]
        [SerializeField] private Key undoKey = Key.PageUp;

        [Tooltip("Steps one action forward.")]
        [SerializeField] private Key redoKey = Key.PageDown;

        private readonly List<HistoryEntry> entries = new();

        private GameStateManager gameStateManager;
        private UnitSpawner unitSpawner;
        private TileSpawner tileSpawner;
        private FogOfWar fogOfWar;
        private AiController aiController;
        private Selector selector;

        // State of the match as it stands right now. Kept because an action announces itself only
        // after it ran, so the state it was taken in has to already be on hand.
        private GameSnapshot currentSnapshot;

        // How many entries are applied - equivalently, the index of the next action to be executed.
        private int cursor;
        private int turnNumber;

        // Restoring re-enters the very code paths that report actions; without this the restore
        // would record itself as a new action.
        private bool restoring;

        public IReadOnlyList<HistoryEntry> Entries => entries;

        /// <summary>Position in the history: the index of the next action to be (re)done.</summary>
        public int Cursor => cursor;

        /// <summary>
        /// Bumped on every recorded action and every move along the history, so editor tooling can
        /// tell "nothing happened" apart from "rebuild me" without subscribing across domain reloads.
        /// </summary>
        public int Version { get; private set; }

        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor < entries.Count;

        public void Setup(GameStateManager gameStateManagerArg, UnitSpawner unitSpawnerArg,
            TileSpawner tileSpawnerArg, FogOfWar fogOfWarArg, AiController aiControllerArg, Selector selectorArg)
        {
            gameStateManager = gameStateManagerArg;
            unitSpawner = unitSpawnerArg;
            tileSpawner = tileSpawnerArg;
            fogOfWar = fogOfWarArg;
            aiController = aiControllerArg;
            selector = selectorArg;

            gameStateManager.TurnStarted += HandleTurnStarted;
        }

        /// <summary>
        /// Starts a fresh history from the state the board is in right now. Called once the entities
        /// are spawned and again on restart, because a restart replaces every unit the older
        /// snapshots refer to.
        /// </summary>
        public void Begin()
        {
            entries.Clear();
            cursor = 0;
            turnNumber = 0;
            currentSnapshot = GameSnapshot.Capture(unitSpawner, gameStateManager, fogOfWar);
            MarkChanged();
        }

        public void Undo() => GoTo(cursor - 1);

        public void Redo() => GoTo(cursor + 1);

        /// <summary>
        /// Restores the state the match was in when action <paramref name="index"/> was taken,
        /// undoing or redoing everything in between. <c>index == 0</c> is the start of the match,
        /// <c>index == Entries.Count</c> the latest state.
        /// </summary>
        public void GoTo(int index)
        {
            index = Mathf.Clamp(index, 0, entries.Count);

            if (index == cursor || currentSnapshot == null)
                return;

            var snapshot = index == 0
                ? (entries.Count > 0 ? entries[0].Before : currentSnapshot)
                : entries[index - 1].After;

            cursor = index;
            Restore(snapshot);
        }

        public void GoToLatest() => GoTo(entries.Count);

        /// <summary>
        /// Hands the current turn back to the AI after restoring a snapshot cancelled it (see
        /// <see cref="Restore"/>). Does nothing when it is not the AI's turn.
        /// </summary>
        public void ResumeAi()
        {
            if (aiController != null)
                aiController.ResumeTurn();
        }

        private void Restore(GameSnapshot snapshot)
        {
            restoring = true;

            // A turn being played out would keep acting on top of the restored state and would hand
            // the turn over at the end, so it is dropped. The AI can be told to take over again.
            if (aiController != null)
                aiController.CancelTurn();

            // Whatever was selected or planned belongs to the state being left behind - the unit may
            // not even be on the board any more. Cleared first because it also wipes the tile
            // markers, which the restore below puts back.
            if (selector != null)
                selector.ResetSelection();

            snapshot.RestoreTo(unitSpawner, tileSpawner, gameStateManager, fogOfWar);
            currentSnapshot = snapshot;

            restoring = false;

            RecountTurns();
            MarkChanged();
        }

        private void HandleActionExecuted(ActionReport report) => Record(report);

        private void HandleTurnStarted(ChangeEvent<State> changeEvent) =>
            Record(ActionReport.TurnChange(changeEvent.NewValue.Team));

        private void Record(ActionReport report)
        {
            // Nothing to record against before the match is set up, or while the history itself is
            // putting the world back together.
            if (restoring || currentSnapshot == null)
                return;

            var before = currentSnapshot;
            var after = GameSnapshot.Capture(unitSpawner, gameStateManager, fogOfWar);

            // Acting after an undo makes the new action the future: what had been undone is dropped.
            if (cursor < entries.Count)
                entries.RemoveRange(cursor, entries.Count - cursor);

            if (report.Kind == ActionKind.TurnChange)
                turnNumber++;

            entries.Add(new HistoryEntry(report, before, after, turnNumber));

            currentSnapshot = after;
            cursor = entries.Count;

            MarkChanged();
        }

        private void RecountTurns()
        {
            turnNumber = 0;
            for (var i = 0; i < cursor; i++)
            {
                if (entries[i].Kind == ActionKind.TurnChange)
                    turnNumber++;
            }
        }

        private void MarkChanged()
        {
            Version++;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[undoKey].wasPressedThisFrame)
                Undo();
            else if (keyboard[redoKey].wasPressedThisFrame)
                Redo();
        }

        private void OnEnable()
        {
            ActionReporter.ActionExecuted += HandleActionExecuted;
        }

        private void OnDisable()
        {
            ActionReporter.ActionExecuted -= HandleActionExecuted;
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnStarted -= HandleTurnStarted;
        }
    }
}
