using System;
using UnityEngine;

namespace Runtime.Controls
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
            Debug.Log("Click: " + name);
        }
    }
}