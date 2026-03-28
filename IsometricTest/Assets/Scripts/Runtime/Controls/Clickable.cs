using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Controls
{
    public class Clickable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<IClickable> OnClick;
        public event Action<IClickable> OnMouseEnter;
        public event Action<IClickable> OnMouseExit;
        
        private IClickable clickable;

        private void Awake()
        {
            clickable = GetComponent<IClickable>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("Click: " + eventData.pointerCurrentRaycast.gameObject.name);
            OnClick?.Invoke(clickable);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("Enter: " + eventData.pointerCurrentRaycast.gameObject.name);
            OnMouseEnter?.Invoke(clickable);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log("Exit: " + eventData.pointerCurrentRaycast.gameObject.name);
            OnMouseExit?.Invoke(clickable);
        }
    }
}