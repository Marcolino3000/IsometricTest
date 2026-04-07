using System;
using UnityEngine;
using UnityEngine.UI;

namespace Runtime.Gameplay.Entities
{
    public class UnitOutline : MonoBehaviour
    {
        [SerializeField] private Color outlineColor = Color.red;
        
        private SpriteRenderer _spriteRenderer; 
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            Setup();
        }

        private void Setup()
        {
            //This needs to be called once at the start to create a new instance of the material for each unit!
            //Otherwise, the units will share the same material which leads to weird behavior.
            Hide();
        }

        public void Show()
        {
            if (_spriteRenderer != null && _spriteRenderer.sharedMaterial != null && _spriteRenderer.sharedMaterial.HasProperty(OutlineColorProp))
            {
                _spriteRenderer.material.SetColor(OutlineColorProp, outlineColor);
            }
        }

        public void Hide()
        {
            if (_spriteRenderer.sharedMaterial.HasProperty(OutlineColorProp))
            {
                _spriteRenderer.material.SetColor(OutlineColorProp, Color.clear);
            }
            
        }
    }
}