using System;
using Runtime.Controls;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime
{
    public class Raycaster : MonoBehaviour
    {
        [SerializeField] private LayerMask unitLayerMask;
        [SerializeField] private LayerMask tileLayerMask;

        private InputAction clickAction;

        private void OnClickPerformed(InputAction.CallbackContext ctx)
        {
            DoRaycast();
        }

        private void DoRaycast()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            CheckForClickable(ray);
        }

        private void CheckForClickable(Ray ray)
        {
            Clickable clickable = ClickUnit(ray)?.GetComponentInChildren<Clickable>();
            
            if(clickable == null)
                clickable = ClickTile(ray)?.GetComponentInChildren<Clickable>();
            
            clickable?.Click();
        }
        
        private Tile ClickTile(Ray ray)
        {
            if (!GetYSortedHits(ray, tileLayerMask, out var hits)) 
                return null;
            
            var selectedTile = hits[0].collider.gameObject.GetComponentInChildren<Tile>();
            
            if (selectedTile == null)
            {
                Debug.LogWarning("No Tile component found on object on Tiles Layermask.");
                return null;
            }
            
            return selectedTile;
        }

        private Unit ClickUnit(Ray ray)
        {
            if (!GetYSortedHits(ray, unitLayerMask, out var hits)) 
                return null;
            
            var selectedUnit = hits[0].collider.gameObject.transform.parent.GetComponent<Unit>();
            
            if (selectedUnit == null)
            {
                Debug.LogWarning("No Unit component found on object on Units Layermask.");
                return null;
            }
            
            return selectedUnit;
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

        private void OnEnable()
        {
            clickAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Mouse>/leftButton");

            clickAction.performed += OnClickPerformed;
            clickAction.Enable();
        }

        private void OnDisable()
        {
            if (clickAction != null)
            {
                clickAction.performed -= OnClickPerformed;
                clickAction.Disable();
            }
        }
    }
}