using System;
using UnityEngine;

namespace Runtime.Gameplay.Controls
{
    public class Clickable : MonoBehaviour
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
            // Debug.Log("Click: " + name);
        }
        
        //todo: Select -> Unit nicht selectable wenn keine AP mehr, tile hingegen nie selectable

        public void HoverEnter()
        {
            OnMouseEnter?.Invoke(clickable);
            // Debug.Log("HoverEnter: " + name);
        }

        public void HoverExit()
        {
            OnMouseExit?.Invoke(clickable);
            // Debug.Log("HoverExit: " + name);
        }
    }
}