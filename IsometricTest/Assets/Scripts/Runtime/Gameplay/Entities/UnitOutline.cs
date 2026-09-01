using System;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    public class UnitOutline : MonoBehaviour
    {
        [SerializeField] private Color neutralColor = Color.white;
        [SerializeField] private Color attackColor = Color.red;
        
        private SpriteRenderer _spriteRenderer;
        private bool _hasOutline;
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessProp = Shader.PropertyToID("_OutlineThickness");

        private void OnDestroy()
        {
            Debug.Log("Outline Comp destroyed");
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // A renderer with no material is drawn by the error shader - the whole sprite quad in
            // magenta rather than the sprite - so it is said here rather than left to be read off
            // the screen, and nothing below touches the material that isn't there.
            if (_spriteRenderer.sharedMaterial == null)
                Debug.LogError($"{name} has no material on its SpriteRenderer, so it cannot be drawn.", this);
            else
                _hasOutline = _spriteRenderer.sharedMaterial.HasProperty(OutlineColorProp);

            Setup();
        }

        private void Setup()
        {
            //This needs to be called once at the start to create a new instance of the material for each unit!
            //Otherwise, the units will share the same material which leads to weird behavior.
            Hide();
        }

        public void Show(OutlineColor color, OutlineThickness thickness)
        {
            if (!_hasOutline)
                return;

            SetColor(color);
            SetThickness(thickness);
        }

        private void SetThickness(OutlineThickness thickness)
        {
            switch (thickness)
            {
                case OutlineThickness.Thin:
                    _spriteRenderer.material.SetFloat(OutlineThicknessProp, 0.5f);
                    break;
                case OutlineThickness.Thick:
                    _spriteRenderer.material.SetFloat(OutlineThicknessProp, 1f);
                    break;
            }
        }

        private void SetColor(OutlineColor color)
        {
            switch (color)
            {
                case OutlineColor.Neutral:
                    _spriteRenderer.material.SetColor(OutlineColorProp, neutralColor);
                    break;
                case OutlineColor.Attack:
                    _spriteRenderer.material.SetColor(OutlineColorProp, attackColor);
                    break;
            }
        }

        public void Hide()
        {
            if (!_hasOutline)
                return;

            _spriteRenderer.material.SetColor(OutlineColorProp, Color.clear);
        }
    }

    public enum OutlineColor
    {
        Neutral,
        Attack
    }
    
    public enum OutlineThickness
    {
        Thin,
        Thick
    }
}