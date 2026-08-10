using Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// Floating popup text above a unit (damage numbers, later heals or status symbols).
    /// Fades in while rising, holds in place, then rises further while fading out.
    /// Spawns as an unparented world-space UIDocument so the popup keeps playing
    /// even when the unit that triggered it is destroyed in the same frame (killing blow).
    /// Tuned via the <see cref="FloatingTextSettings"/> asset in Resources/Settings.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private const string SettingsResourcePath = "Settings/Default FloatingTextSettings";

        private static FloatingTextSettings settings;

        private static FloatingTextSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = Resources.Load<FloatingTextSettings>(SettingsResourcePath);
                    if (settings == null)
                    {
                        Debug.LogError($"No FloatingTextSettings asset at Resources/{SettingsResourcePath}, using built-in defaults.");
                        settings = ScriptableObject.CreateInstance<FloatingTextSettings>();
                    }
                }

                return settings;
            }
        }

        private VisualElement root;
        private Vector3 basePosition;
        private float age;

        public static void ShowDamage(int healthDelta, Vector3 unitPosition, PanelSettings panelSettings)
        {
            Show(healthDelta.ToString(), Settings.DamageColor, unitPosition + Settings.SpawnOffset, panelSettings);
        }

        /// <summary>Health going back up, e.g. from an active item. Signed, so it reads as a gain.</summary>
        public static void ShowHeal(int healthDelta, Vector3 unitPosition, PanelSettings panelSettings)
        {
            Show($"+{healthDelta}", Settings.HealColor, unitPosition + Settings.SpawnOffset, panelSettings);
        }

        /// <summary>
        /// Why something the player asked for did not happen - a lootbox left where it lies because
        /// there is no slot for what is in it. Words rather than a number, but it belongs over the
        /// character's head like the numbers do, and goes away by itself the same way.
        /// </summary>
        public static void ShowNotice(string text, Vector3 unitPosition, PanelSettings panelSettings)
        {
            Show(text, Settings.NoticeColor, unitPosition + Settings.SpawnOffset, panelSettings,
                Settings.NoticeFontSize);
        }

        /// <summary>
        /// Puts <paramref name="text"/> over a world position. <paramref name="fontSize"/> defaults to
        /// the settings' - a whole sentence needs to be smaller than a damage number to fit.
        /// </summary>
        public static void Show(string text, Color color, Vector3 worldPosition, PanelSettings panelSettings,
            int fontSize = 0)
        {
            var popup = new GameObject("FloatingText");
            popup.transform.position = worldPosition;
            popup.transform.localScale = Vector3.one * Settings.DocumentScale;
            popup.layer = LayerMask.NameToLayer("UI");

            var document = popup.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            var floatingText = popup.AddComponent<FloatingText>();
            floatingText.basePosition = worldPosition;
            floatingText.BuildLabel(document, text, color, fontSize > 0 ? fontSize : Settings.FontSize);
        }

        private void BuildLabel(UIDocument document, string text, Color color, int fontSize)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            // Start invisible so the label doesn't flash at full opacity before the first Update.
            root.style.opacity = 0f;

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityTextOutlineWidth = Settings.OutlineWidth;
            label.style.unityTextOutlineColor = Settings.OutlineColor;
            root.Add(label);
        }

        // Timing and movement are read from the settings every frame, so tweaking the
        // asset in play mode is reflected by popups immediately.
        private void Update()
        {
            age += Time.deltaTime;

            if (age < Settings.FadeInDuration)
            {
                // Decelerating rise into the hold position while fading in.
                float t = age / Settings.FadeInDuration;
                float eased = 1f - (1f - t) * (1f - t);
                SetOpacity(t);
                SetRise(Settings.FadeInRise * eased);
            }
            else if (age < Settings.FadeInDuration + Settings.HoldDuration)
            {
                SetOpacity(1f);
                SetRise(Settings.FadeInRise);
            }
            else if (age < Settings.FadeInDuration + Settings.HoldDuration + Settings.FadeOutDuration)
            {
                // Accelerating rise out of the hold position while fading out.
                float t = (age - Settings.FadeInDuration - Settings.HoldDuration) / Settings.FadeOutDuration;
                SetOpacity(1f - t);
                SetRise(Settings.FadeInRise + Settings.FadeOutRise * t * t);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetOpacity(float value)
        {
            root.style.opacity = value;
        }

        private void SetRise(float height)
        {
            transform.position = basePosition + Vector3.up * height;
        }
    }
}
