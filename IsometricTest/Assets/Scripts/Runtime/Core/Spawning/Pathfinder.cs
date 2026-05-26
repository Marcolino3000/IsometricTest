using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    /// <summary>
    /// Simple grid pathfinder using A* (Manhattan heuristic) for 4-direction movement.
    /// By default occupied tiles are treated as blocked; set ignoreOccupied=true to ignore occupancy.
    /// If ignoreOccupied==false the algorithm will still allow reaching the goal tile even when it's occupied.
    /// </summary>
    public class Pathfinder
    {
        private readonly TileSpawner _tileSpawner;

        public Pathfinder(TileSpawner tileSpawner)
        {
            _tileSpawner = tileSpawner;
        }

        public List<Tile> FindPath(Tile start, Tile goal, bool ignoreOccupied = false)
        {
            if (start == null || goal == null || _tileSpawner == null)
                return null;

            // fast path
            if (start == goal)
                return new List<Tile> { start };

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
                    return ReconstructPath(cameFrom, current);

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

                    // occupancy check
                    if (!ignoreOccupied)
                    {
                        // allow reaching the goal even if occupied, but do not traverse through occupied tiles
                        if (neighbor != goalPos && neighborTile.IsOccupied)
                            continue;
                    }

                    var tentativeG = gScore.ContainsKey(current) ? gScore[current] + 1 : int.MaxValue;

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

            // no path found
            return null;
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

