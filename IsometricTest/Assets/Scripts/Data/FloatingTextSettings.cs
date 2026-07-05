using UnityEngine;

namespace Data
{
    /// <summary>
    /// Tuning for the floating popup texts above units (see <see cref="UI.FloatingText"/>).
    /// Popups are spawned purely from code, so the default asset is loaded from
    /// Resources/Settings instead of being assigned through a scene or prefab reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Settings/FloatingTextSettings")]
    public class FloatingTextSettings : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Seconds the text spends fading in while rising to its hold position.")]
        public float FadeInDuration = 0.3f;
        [Tooltip("Seconds the text stays fully visible at its hold position.")]
        public float HoldDuration = 0.7f;
        [Tooltip("Seconds the text spends fading out while rising further.")]
        public float FadeOutDuration = 0.5f;

        [Header("Movement")]
        [Tooltip("How far the text rises while fading in, in world units.")]
        public float FadeInRise = 0.15f;
        [Tooltip("How far the text rises while fading out, in world units.")]
        public float FadeOutRise = 0.25f;
        [Tooltip("Offset from the unit's position where the text spawns (above the health bar, which sits at local y 0.88).")]
        public Vector3 SpawnOffset = new(0f, 1.05f, 0f);

        [Header("Appearance")]
        [Tooltip("Transform scale of the popup document; the unit bar documents use 0.05.")]
        public float DocumentScale = 0.05f;
        public int FontSize = 100;
        public float OutlineWidth = 4f;
        public Color OutlineColor = new(0.08f, 0.05f, 0.05f, 0.9f);
        public Color DamageColor = new(0.85f, 0.2f, 0.16f);
    }
}
