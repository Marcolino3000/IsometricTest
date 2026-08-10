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
