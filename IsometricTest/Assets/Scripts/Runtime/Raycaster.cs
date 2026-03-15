using System;
using System.Linq;
using UnityEngine;

namespace Runtime
{
    public class Raycaster : MonoBehaviour
    {
        public event Action OnTurnFinished;
        
        [SerializeField] private Unit selectedUnit;
        [SerializeField] private Tile selectedTile;
        
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                DoRaycast();
            }
        }

        private void DoRaycast()
        {
            TileSpawner.ResetHighlightedTiles();
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
            SortByYAxis(hits);
            
            if (SelectUnit(hits))
            {
                selectedUnit.HighlightMoveableTiles();
                return;
            }

            if (selectedUnit != null && SelectTile(hits))
            {
                if(selectedUnit.TryMoveToTile(selectedTile))
                {
                    selectedUnit = null;
                    selectedUnit = null;
                    OnTurnFinished?.Invoke();
                }
            }

            // Debug.Log(hits[0].collider.name);
            // TileSpawner.SetTileColor(hits[0].collider.gameObject);
        }

        private bool SelectTile(RaycastHit2D[] hits)
        {
            var tiles = hits
                .Select(hit => hit.collider.gameObject.GetComponent<Tile>())
                .Where(unit => unit != null)
                .ToList();
            
            if (tiles.Count == 0) return false;
            
            selectedTile = tiles[0];

            Debug.Log("Selected Tile: " + selectedTile.gameObject.name);
            return true;
        }

        private bool SelectUnit(RaycastHit2D[] hits)
        {
            var units = hits
                .Select(hit => hit.collider.gameObject.GetComponent<Unit>())
                .Where(unit => unit != null)
                .ToList();

            if (units.Count == 0) return false;
            
            selectedUnit = null;
            selectedUnit = units[0];

            if (selectedUnit == null) return false;
            
            Debug.Log($"Selected unit: {selectedUnit.name}");
            return true;

        }

        private static void SortByYAxis(RaycastHit2D[] hits)
        {
            System.Array.Sort(hits, (a, b) => a.collider.transform.position.y.CompareTo(b.collider.transform.position.y));
        }
    }
}