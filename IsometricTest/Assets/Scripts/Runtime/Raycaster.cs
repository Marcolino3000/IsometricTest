using System;
using UnityEngine;

namespace Runtime
{
    public class Raycaster : MonoBehaviour
    {
        public event Action<Unit> OnUnitClicked;
        public event Action<Tile> OnTileClicked;

        [SerializeField] private LayerMask unitLayerMask;
        [SerializeField] private LayerMask tileLayerMask;

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

            if (ClickUnit(ray))
                return;

            ClickTile(ray);
        }

        private bool ClickTile(Ray ray)
        {
            if (!GetYSortedHits(ray, tileLayerMask, out var hits)) 
                return false;
            
            var selectedTile = hits[0].collider.gameObject.GetComponent<Tile>();
            
            if (selectedTile == null)
            {
                Debug.LogWarning("No Tile component found on object on Tiles Layermask.");
                return false;
            }
            
            OnTileClicked?.Invoke(selectedTile);
            return true;
        }

        private bool ClickUnit(Ray ray)
        {
            if (!GetYSortedHits(ray, unitLayerMask, out var hits)) 
                return false;
            
            var selectedUnit = hits[0].collider.gameObject.GetComponent<Unit>();
            
            if (selectedUnit == null)
            {
                Debug.LogWarning("No Unit component found on object on Units Layermask.");
                return false;
            }
            
            OnUnitClicked?.Invoke(selectedUnit);
            return true;
        }

        private static bool GetYSortedHits(Ray ray, LayerMask mask, out RaycastHit2D[] hits)
        {
            hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, mask);
            
            if (hits == null || hits.Length == 0)
                return false;
            
            SortByYAxis(hits);
            return true;
        }

        private static void SortByYAxis(RaycastHit2D[] hits)
        {
            Array.Sort(hits, (a, b) => a.collider.transform.position.y.CompareTo(b.collider.transform.position.y));
        }
    }
}