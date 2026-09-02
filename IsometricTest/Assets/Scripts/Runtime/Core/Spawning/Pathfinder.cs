using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    /// <summary>
    /// Simple grid pathfinder using A* (Manhattan heuristic) for 4-direction movement.
    /// By default occupied tiles are treated as blocked; set ignoreOccupied=true to ignore occupancy entirely.
    /// The goal tile may only be reached when it is unoccupied, unless ignoreGoalOccupied==true.
    /// Set excludeGoal=true to drop the goal tile from the returned path (e.g. to stop next to a target).
    /// </summary>
    public class Pathfinder
    {
        private readonly TileSpawner _tileSpawner;

        public Pathfinder(TileSpawner tileSpawner)
        {
            _tileSpawner = tileSpawner;
        }

        public List<Tile> FindPath(Tile start, Tile goal, bool ignoreOccupied = false, bool ignoreGoalOccupied = false, bool excludeGoal = false, UnitState mover = null)
        {
            if (start == null || goal == null || _tileSpawner == null || !goal.IsPassable || goal.IsOccupied && !ignoreGoalOccupied)
                return new List<Tile>();

            var startPos = start.Position;
            var goalPos = goal.Position;

            var openSet = new List<Vector2Int> { startPos };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            var gScore = new Dictionary<Vector2Int, int> { [startPos] = 0 };
            var fScore = new Dictionary<Vector2Int, int> { [startPos] = Heuristic(startPos, goalPos) };

            while (openSet.Count > 0)
            {
                // get node in openSet with lowest fScore
                var current = openSet.OrderBy(p => fScore.ContainsKey(p) ? fScore[p] : int.MaxValue).First();

                if (current == goalPos)
                {
                    var path = ReconstructPath(cameFrom, current);
                    if (excludeGoal && path.Count > 0)
                        path.RemoveAt(path.Count - 1);
                    return path;
                }

                openSet.Remove(current);

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!_tileSpawner.GetTileAtPosition(neighbor))
                    {
                        // no tile at this position
                        continue;
                    }

                    var neighborTile = _tileSpawner.GetTileAtPosition(neighbor);
                    if (neighborTile == null)
                        continue;

                    // impassable terrain (e.g. mountains) can never be entered, not even as the goal
                    if (!neighborTile.IsPassable)
                        continue;

                    // Ground this particular unit is not allowed on - an opponent confined to the
                    // ring it spawned in. Asked here rather than only where the step is charged, so
                    // the route, the reachable-tile highlight and the threat overlay all show the
                    // board it is actually allowed to walk instead of one it will be refused.
                    if (!MovementRules.CanEnter(mover, neighborTile))
                        continue;

                    if (!ignoreOccupied)
                    {
                        //do not traverse through occupied tiles
                        if (neighbor != goalPos && neighborTile.IsOccupied)
                            continue;
                    }

                    // Asked rather than worked out here, so a unit that finds hills cheap is also
                    // routed over them instead of around. Without a mover this is the bare terrain cost.
                    var stepCost = MovementRules.GetStepCost(mover, neighborTile);
                    var tentativeG = gScore.ContainsKey(current) ? gScore[current] + stepCost : int.MaxValue;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, goalPos);
                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }

            // no path found (e.g. the goal is walled off by mountains or occupied tiles)
            return new List<Tile>();
        }

        /// <summary>
        /// Finds a path toward <paramref name="target"/> but stops as soon as the unit is within
        /// <paramref name="range"/> (Manhattan) of it, dropping the remaining steps. Lets ranged
        /// attackers close the distance only enough to reach instead of walking right up to it.
        /// </summary>
        /// <summary>
        /// The walk up to <paramref name="target"/>, cut at the first tile <paramref name="attacker"/>
        /// could actually strike from. "Could strike" is <see cref="CombatRules.CanAttackFrom"/>, the
        /// same question the attack condition asks, so the path never ends somewhere the strike is
        /// then refused - which is what makes a unit walk on around a mountain rather than stop
        /// behind it with the target in range and no line to it.
        /// </summary>
        public List<Tile> FindAttackApproachPath(Unit attacker, Tile target, bool ignoreOccupied = false)
        {
            var start = attacker != null ? attacker.CurrentState.Position : null;
            var path = FindPath(start, target, ignoreOccupied, ignoreGoalOccupied: true, excludeGoal: true,
                mover: attacker != null ? attacker.CurrentState : null);

            var result = new List<Tile>();

            if (path == null)
                return result;

            foreach (var tile in path)
            {
                result.Add(tile);

                if (CombatRules.CanAttackFrom(attacker, tile, target))
                    break;
            }

            return result;
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
        {
            yield return new Vector2Int(pos.x + 1, pos.y);
            yield return new Vector2Int(pos.x - 1, pos.y);
            yield return new Vector2Int(pos.x, pos.y + 1);
            yield return new Vector2Int(pos.x, pos.y - 1);
        }

        private List<Tile> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var total = new List<Vector2Int> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                total.Add(current);
            }

            total.Reverse();

            var result = new List<Tile>();
            foreach (var pos in total)
            {
                var tile = _tileSpawner.GetTileAtPosition(pos);
                if (tile != null)
                    result.Add(tile);
            }

            return result;
        }
    }
}

