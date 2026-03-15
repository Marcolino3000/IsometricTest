using UnityEngine;

namespace Runtime
{
    public class Tile : MonoBehaviour
    {
        public Vector2Int Position;
        public bool IsOccupied;
        
        [SerializeField] private TileMarker marker;
        
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            
            if(occupied)
                marker.SetMarkerColor(MarkerColor.Orange);
            else
                marker.SetMarkerColor(MarkerColor.None);
        }
    }
}