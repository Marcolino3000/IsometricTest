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
            var fullOpacity = Color.white;
            var semiTransparent = new Color(1f, 1f, 1f, 0.5f);
            
            switch (color)
            {
                case MarkerColor.None:
                    spriteRenderer.sprite = null;
                    break;
                case MarkerColor.Green:
                    spriteRenderer.sprite = greenMarker;
                    spriteRenderer.color = fullOpacity;
                    break;
                case MarkerColor.Orange:
                    spriteRenderer.sprite = OrangeMarker;
                    spriteRenderer.color = fullOpacity;
                    break;
                case MarkerColor.White:
                    spriteRenderer.sprite = WhiteMarker;
                    spriteRenderer.color = fullOpacity;
                    break;
                case MarkerColor.Blue:
                    spriteRenderer.sprite = WhiteMarker;
                    spriteRenderer.color = Color.blue;
                    break;
                case MarkerColor.TransparentOrange:
                    spriteRenderer.sprite = OrangeMarker;
                    spriteRenderer.color = semiTransparent;
                    break;
                case MarkerColor.TransparentWhite:
                    spriteRenderer.sprite = WhiteMarker;
                    spriteRenderer.color = semiTransparent;
                    break;
                case MarkerColor.TransparentBlue:
                    spriteRenderer.sprite = WhiteMarker;
                    spriteRenderer.color = Color.lightBlue;
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
        White,
        Blue,
        TransparentOrange,
        TransparentWhite,
        TransparentBlue
    }
}