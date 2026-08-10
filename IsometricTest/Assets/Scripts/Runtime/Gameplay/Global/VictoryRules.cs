using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;

namespace Runtime.Gameplay.Global
{
    public enum MatchOutcome
    {
        /// <summary>The match is still being played. The default, so a fresh result is undecided.</summary>
        Undecided,
        Victory,
        Defeat
    }

    /// <summary>
    /// How the match stands, and why. The reason is the sentence the end screen shows, phrased where
    /// the condition is checked so a new condition brings its own wording rather than needing a case
    /// somewhere else to name it.
    /// </summary>
    public readonly struct MatchResult
    {
        public readonly MatchOutcome Outcome;
        public readonly string Reason;

        private MatchResult(MatchOutcome outcome, string reason)
        {
            Outcome = outcome;
            Reason = reason;
        }

        public bool IsOver => Outcome != MatchOutcome.Undecided;

        public static MatchResult Open => new(MatchOutcome.Undecided, null);

        public static MatchResult Won(string reason) => new(MatchOutcome.Victory, reason);

        public static MatchResult Lost(string reason) => new(MatchOutcome.Defeat, reason);
    }

    /// <summary>
    /// Whether the match has been decided, and by what. A pure query like <see cref="CombatRules"/> and
    /// <see cref="MovementRules"/>: it reads the board and answers, it changes nothing and remembers
    /// nothing. <see cref="MatchOutcomeWatcher"/> is what asks it and what tells anyone about the answer.
    ///
    /// Every condition is a function of state that <see cref="History.GameSnapshot"/> already records -
    /// who is alive and what has been explored - which is why the outcome is never stored anywhere:
    /// undo puts the board back and the answer follows it, so a match can be lost, taken back and
    /// played on without the end ever having to be undone as a thing of its own.
    ///
    /// Which conditions count is <see cref="GameRules"/>' business; all of them can be switched off,
    /// leaving a match that never ends.
    /// </summary>
    public static class VictoryRules
    {
        /// <summary>
        /// The state of the match right now. Defeat is asked first: a character that falls to the
        /// retaliation of the last enemy it struck down has lost, rather than won by a hair.
        /// </summary>
        public static MatchResult Evaluate(GameRules rules, UnitSpawner unitSpawner, TileSpawner tileSpawner,
            FogOfWar fogOfWar)
        {
            if (rules == null || unitSpawner == null)
                return MatchResult.Open;

            if (rules.LoseWhenCharacterFalls && IsWipedOut(unitSpawner, Team.Player))
                return MatchResult.Lost("Your character has fallen.");

            if (rules.WinByDefeatingAllEnemies && IsWipedOut(unitSpawner, Team.Opponent))
                return MatchResult.Won("Every enemy has fallen.");

            if (rules.WinByExploringMap && IsMapUncovered(tileSpawner, fogOfWar))
                return MatchResult.Won("The whole map has been uncovered.");

            return MatchResult.Open;
        }

        /// <summary>
        /// Whether the team had units and none of them is left in play. Removed units are counted too -
        /// they are only hidden, so they are what tells "wiped out" apart from "not spawned yet", which
        /// is the state the board is in for the first moments of a match.
        /// </summary>
        private static bool IsWipedOut(UnitSpawner unitSpawner, Team team)
        {
            var hadAny = false;

            foreach (var unit in unitSpawner.AllSpawnedUnits)
            {
                if (unit == null || unit.CurrentState.Team != team)
                    continue;

                if (unit.IsAlive)
                    return false;

                hadAny = true;
            }

            return hadAny;
        }

        /// <summary>
        /// Whether the player has explored every tile it could ever stand on. Impassable ground is left
        /// out: the inside of a mountain range may never come within sight of anywhere reachable, and a
        /// win nobody can reach is worse than none. An unbuilt grid is not a win either.
        /// </summary>
        private static bool IsMapUncovered(TileSpawner tileSpawner, FogOfWar fogOfWar)
        {
            if (tileSpawner == null || fogOfWar == null)
                return false;

            var hadAny = false;

            foreach (var tile in tileSpawner.AllTiles)
            {
                if (tile == null || !tile.IsPassable)
                    continue;

                if (!fogOfWar.IsExplored(Team.Player, tile.Position))
                    return false;

                hadAny = true;
            }

            return hadAny;
        }
    }
}
