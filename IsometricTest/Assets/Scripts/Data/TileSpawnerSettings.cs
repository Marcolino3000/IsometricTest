using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/TileSpawnerSettings")]
    public class TileSpawnerSettings : ScriptableObject
    {
        [Header("Tile Settings")]
        public GameObject TilePrefab;
        public float TileSize;
        public float HalfTileOffsetX;
        public float HalfTileOffsetY;

        [Header("Grid Settings")] 
        public Vector3 StartPosition;
        public int GridSizeX;
        public int GridSizeY;
    }
}