using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// What a step costs, in one place - the movement counterpart to <see cref="CombatRules"/>.
    ///
    /// Four things used to work this out for themselves and had to be kept in step by hand: the
    /// pathfinder choosing a route, <c>TileSpawner</c> deciding which tiles are within reach, the AI
    /// budgeting its turn, and <c>MoveAction</c> actually charging for the step. They now all ask
    /// here, so a trait that makes hills cheap changes the route, the highlight, the AI's plan and
    /// the bill together instead of one of them.
    /// </summary>
    public static class MovementRules
    {
        /// <summary>
        /// A step never costs less than this, however much is discounted - a free step would let a
        /// unit cross the whole map, since the move action only tests cost against action points.
        /// </summary>
        private const int MinimumStepCost = 1;

        private static GameRules rules;

        /// <summary>
        /// Injected by the Initiator, beside <see cref="CombatRules.Setup"/>. Only
        /// <see cref="CanEnter"/> needs it - what a step costs is answered from its arguments alone.
        /// </summary>
        public static void Setup(GameRules gameRules)
        {
            rules = gameRules;
        }

        /// <summary>
        /// Never null: a missing asset yields a default-valued instance, so a step is still priced
        /// and nobody is confined.
        /// </summary>
        private static GameRules Rules
        {
            get
            {
                if (rules == null)
                    rules = ScriptableObject.CreateInstance<GameRules>();

                return rules;
            }
        }

        /// <summary>
        /// Whether <paramref name="mover"/> may set foot on <paramref name="destination"/> at all -
        /// the other half of a step, asked wherever its cost is. Today the one thing that can refuse
        /// is <see cref="GameRules.ConfineOpponentsToSpawnZone"/>: an opponent that may not leave the
        /// ring it spawned in (see <see cref="UnitState.HomeZone"/>) is turned back at its border.
        ///
        /// Asked by the pathfinder, so the route, the reachable-tile highlight, the threat overlay
        /// and the AI's plan all read the same board - and by <c>MoveAction.TestConditions</c>, so
        /// whoever planned the step is billed by the same answer. True for anything it cannot refuse:
        /// no mover, no tile, a unit belonging to no ring, or a map with no rings authored.
        ///
        /// <b>Movement only.</b> A confined unit still strikes across its border at whatever its
        /// weapon reaches - which is why this is asked of a step rather than of a whole plan.
        /// </summary>
        public static bool CanEnter(UnitState mover, Tile destination)
        {
            if (mover == null || destination == null)
                return true;

            if (!Rules.ConfineOpponentsToSpawnZone || mover.HomeZone == UnitState.NoZone)
                return true;

            if (ZoneRules.IndexAt(destination) == mover.HomeZone)
                return true;

            // Standing outside its own ring already - the switch is live, so it can be turned on
            // over a unit that has walked out. Let it walk back rather than freeze it where it
            // stands: a step is allowed while it closes the distance to home.
            var from = mover.Position;

            return from != null
                   && ZoneRules.DistanceOutside(mover.HomeZone, destination.Position)
                   < ZoneRules.DistanceOutside(mover.HomeZone, from.Position);
        }

        /// <summary>
        /// What entering <paramref name="destination"/> costs <paramref name="mover"/>: the base
        /// move cost, plus the tile's difficult-terrain surcharge, then every trait's say.
        /// </summary>
        public static int GetStepCost(UnitState mover, Tile destination, int baseCost)
        {
            var cost = baseCost + (destination != null ? destination.ExtraMoveCost : 0);

            if (mover != null && destination != null)
            {
                var context = new MoveContext(mover, destination);

                foreach (var trait in CombatRules.TraitsAffecting(mover, destination))
                    cost = trait.ModifyMoveCost(cost, context);
            }

            return Mathf.Max(MinimumStepCost, cost);
        }

        /// <summary>
        /// The same, reading the base cost off the mover's own move action. A caller with no mover
        /// at all (the pathfinder sizing a route for nobody in particular) gets the bare terrain cost.
        /// </summary>
        public static int GetStepCost(UnitState mover, Tile destination)
        {
            return GetStepCost(mover, destination, BaseCostOf(mover));
        }

        /// <summary>
        /// Total cost of walking <paramref name="path"/>, whose first entry is the tile already stood on.
        /// </summary>
        public static int GetPathCost(UnitState mover, List<Tile> path)
        {
            var baseCost = BaseCostOf(mover);
            var cost = 0;

            for (var i = 1; i < path.Count; i++)
                cost += GetStepCost(mover, path[i], baseCost);

            return cost;
        }

        private static int BaseCostOf(UnitState mover)
        {
            return mover?.MoveAction != null ? mover.MoveAction.Condition.Cost : MinimumStepCost;
        }
    }
}
