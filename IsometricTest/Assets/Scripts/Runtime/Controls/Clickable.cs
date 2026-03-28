using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Controls
{
    public class Clickable : MonoBehaviour
        // , IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<IClickable> OnClick;
        public event Action<IClickable> OnMouseEnter;
        public event Action<IClickable> OnMouseExit;
        
        private IClickable clickable;

        private void Awake()
        {
            clickable = GetComponentInParent<IClickable>();
        }

        public void Click()
        {
            OnClick?.Invoke(clickable);
            Debug.Log("Click: " + name);
        }

        // public void OnPointerClick(PointerEventData eventData)
        // {
        //     OnClick?.Invoke(clickable);
        //     Debug.Log("Click: " + eventData.pointerCurrentRaycast.gameObject.name);
        // }
        //
        // public void OnPointerEnter(PointerEventData eventData)
        // {
        //     OnMouseEnter?.Invoke(clickable);
        // }
        //
        // public void OnPointerExit(PointerEventData eventData)
        // {
        //     OnMouseExit?.Invoke(clickable);
        // }
    }
}