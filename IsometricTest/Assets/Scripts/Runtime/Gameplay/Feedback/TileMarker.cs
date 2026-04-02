using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class TileMarker : MonoBehaviour
    {
        [SerializeField] private Sprite greenMarker;
        [SerializeField] private Sprite OrangeMarker;
        [SerializeField] private Sprite WhiteMarker;
        
        private SpriteRenderer spriteRenderer;
        
        public void SetMarkerColor(MarkerColor color)
        {
            switch (color)
            {
                case MarkerColor.None:
                    spriteRenderer.sprite = null;
                    break;
                case MarkerColor.Green:
                    spriteRenderer.sprite = greenMarker;
                    break;
                case MarkerColor.Orange:
                    spriteRenderer.sprite = OrangeMarker;
                    break;
                case MarkerColor.White:
                    spriteRenderer.sprite = WhiteMarker;
                    break;
            }
        }
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
    }

    public enum MarkerColor
    {
        None,
        Green,
        Orange,
        White
    }
}