using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime
{
    public class MouseDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("Enter: " + eventData.pointerCurrentRaycast.gameObject.name);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log("Exit: " + eventData.pointerCurrentRaycast.gameObject.name);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("Click: " + eventData.pointerCurrentRaycast.gameObject.name);
        }
    }
}