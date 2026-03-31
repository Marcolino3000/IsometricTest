using UnityEngine;

namespace Runtime
{
    public static class Helper
    {
        public static (int, int) GetRandomGridPosition(int gridSizeX, int gridSizeY)
        {
            var x = Random.Range(0, gridSizeX);
            var y = Random.Range(0, gridSizeY);
            return (x, y);
        }
        
   
    }
}