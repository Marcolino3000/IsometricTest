using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
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

        public List<Tile> FindPath(Tile start, Tile goal, bool ignoreOccupied = false, bool ignoreGoalOccupied = false, bool excludeGoal = false)
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

                    if (!ignoreOccupied)
                    {
                        //do not traverse through occupied tiles
                        if (neighbor != goalPos && neighborTile.IsOccupied)
                            continue;
                    }

                    // base step cost of 1, plus any extra cost for difficult terrain (e.g. hills)
                    var stepCost = 1 + neighborTile.ExtraMoveCost;
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
        public List<Tile> FindPathWithinRange(Tile start, Tile target, int range, bool ignoreOccupied = false)
        {
            var path = FindPath(start, target, ignoreOccupied, ignoreGoalOccupied: true, excludeGoal: true);
            return TruncateWithinRange(path, target, range);
        }

        private static List<Tile> TruncateWithinRange(List<Tile> path, Tile target, int range)
        {
            var result = new List<Tile>();

            if (path == null)
                return result;

            foreach (var tile in path)
            {
                result.Add(tile);

                if (Heuristic(tile.Position, target.Position) <= range)
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

