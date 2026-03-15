using System.Collections.Generic;
using UnityEngine;

namespace Runtime
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private UnitState currentState;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;

        public void Init(TileSpawner tileSpawner, UnitSpawner unitSpawner, Vector2Int position)
        {
            currentState = blueprint.DefaultState;
            currentState.Position = position;
            
            this.tileSpawner = tileSpawner;
            this.unitSpawner = unitSpawner;
        }

        private List<Vector2Int> GetMoveableTilePositions()
        {
            var tiles = new List<Vector2Int>();
            
            CheckMoveDirectionsNew(tiles);
            
            return tiles;
        }

    private void CheckMoveDirections(List<Vector2Int> tiles)
    {
        foreach (var direction in Direction.ForwardAndSides)
        {
            for (int step = 1; step <= currentState.Range; step++)
            {
                var position = currentState.Position + direction * step;

                if (!tileSpawner.CheckGridPosition(position.x, position.y))
                {
                    continue;
                }

                tiles.Add(position);
            }
        }
    }
    
    private void CheckMoveDirectionsNew(List<Vector2Int> tiles)
    {
        // Pre-calc forward as Vector2 for dot product
        Vector2 forward = Direction.Forward;

        for (int dx = -currentState.Range; dx <= currentState.Range; dx++)
        {
            for (int dy = -currentState.Range; dy <= currentState.Range; dy++)
            {
                // skip own tile
                if (dx == 0 && dy == 0)
                    continue;

                // only keep tiles inside the diamond (Manhattan distance)
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > currentState.Range)
                    continue;

                var offset = new Vector2Int(dx, dy);

                // filter to "forward and sides" half-space:
                // dot(offset, forward) >= 0 => not behind the unit
                float dot = Vector2.Dot(offset, forward);
                if (dot < 0f)
                    continue;

                var position = currentState.Position + offset;

                if (!tileSpawner.CheckGridPosition(position.x, position.y))
                    continue;

                tiles.Add(position);
            }
        }
    }

        public void HighlightMoveableTiles()
        {
            foreach (var tilePosition in GetMoveableTilePositions())
            {
                tileSpawner.HighlightTile(tilePosition);
            }
        }

        public void MoveToTile(Tile selectedTile)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return;
            }

            currentState.Position = selectedTile.Position;
            transform.position = unitSpawner.GridToWorldPosition(selectedTile.Position);
            
            TileSpawner.ResetHighlightedTiles();
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}