using UnityEngine;

namespace Runtime
{
    public class Tile : MonoBehaviour
    {
        public Vector2Int Position;
        public bool IsOccupied {get; private set;}
        
        [SerializeField] private Unit unit;
        [SerializeField] private TileMarker marker;

        public void SetUnit(Unit unit)
        {
            this.unit = unit;
            SetOccupied(unit != null);
        }
        
        private void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            
            if(occupied)
                marker.SetMarkerColor(MarkerColor.Orange);
            else
                marker.SetMarkerColor(MarkerColor.None);
        }
    }
}