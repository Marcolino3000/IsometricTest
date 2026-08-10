using System;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.History;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Watches the board and says when the match has been decided. It holds no answer of its own: every
    /// verdict comes from <see cref="VictoryRules"/>, read off the world as it stands, so undo needs no
    /// help here - putting the board back puts the verdict back with it, and a lost match becomes an
    /// open one again the moment the killing blow is taken back.
    ///
    /// Created at runtime by the <see cref="Core.Initiator"/>, so the Systems prefab needs no further
    /// component. Whoever cares subscribes to <see cref="OutcomeChanged"/>: the end screen puts itself
    /// up on it, and the AI and the next-turn button stop the match moving on.
    /// </summary>
    public class MatchOutcomeWatcher : MonoBehaviour
    {
        /// <summary>Raised when the match becomes won, lost, or open again after an undo.</summary>
        public event Action<MatchResult> OutcomeChanged;

        private GameRules rules;
        private UnitSpawner unitSpawner;
        private TileSpawner tileSpawner;
        private FogOfWar fogOfWar;
        private GameStateManager gameStateManager;

        // A state change that may have decided the match, to be read at the end of the frame rather
        // than where it was raised. A restore takes the board apart and puts it back together over
        // several of them, and half a board is not worth asking about - waiting out the frame asks
        // once, of a board that is whole again either way.
        private bool dirty;

        public MatchResult Result { get; private set; } = MatchResult.Open;

        /// <summary>Whether the match has been decided. Nothing may carry it forward while it has.</summary>
        public bool IsOver => Result.IsOver;

        public void Setup(GameRules gameRules, UnitSpawner unitSpawnerArg, TileSpawner tileSpawnerArg,
            FogOfWar fogOfWarArg, GameStateManager gameStateManagerArg)
        {
            rules = gameRules;
            unitSpawner = unitSpawnerArg;
            tileSpawner = tileSpawnerArg;
            fogOfWar = fogOfWarArg;
            gameStateManager = gameStateManagerArg;

            // Two channels cover everything that can decide a match. An action announces the deaths and
            // the ground uncovered while the match is played; a state change covers the rest, including
            // the last thing a snapshot restore does - which is how undo and redo are noticed without
            // the history knowing anything about victory.
            gameStateManager.GameStateChanged += HandleStateChanged;

            // A board that starts out decided (nothing to explore, no opponents) is one too.
            dirty = true;
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
                gameStateManager.GameStateChanged -= HandleStateChanged;
        }

        /// <summary>
        /// An action announces itself once it is over and the board is whole, so this one is answered
        /// on the spot rather than at the end of the frame. It has to be: a turn behind the fog is
        /// paced by nothing and plays itself out within the frame, so a verdict held back until
        /// <see cref="LateUpdate"/> would arrive after the actions it was meant to stop.
        /// </summary>
        private void HandleActionExecuted(ActionReport report) => Evaluate();

        private void HandleStateChanged(ChangeEvent<State> changeEvent) => dirty = true;

        private void LateUpdate()
        {
            if (!dirty)
                return;

            dirty = false;
            Evaluate();
        }

        /// <summary>
        /// Asks the rules and announces an answer that differs from the one standing. Only the outcome
        /// is compared: a match cannot be won twice over, and the reason merely words the one verdict.
        /// </summary>
        private void Evaluate()
        {
            var result = VictoryRules.Evaluate(rules, unitSpawner, tileSpawner, fogOfWar);

            if (result.Outcome == Result.Outcome)
                return;

            Result = result;
            OutcomeChanged?.Invoke(result);
        }
    }
}
