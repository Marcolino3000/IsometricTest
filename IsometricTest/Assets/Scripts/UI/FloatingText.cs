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

        /// <summary>
        /// The damage number, and beside it whatever the strike had to say for itself - "Crit!" and
        /// anything else a trait added to <see cref="Runtime.Gameplay.Global.StrikeNotes"/>. One
        /// popup rather than two, so the word cannot drift away from the number it explains.
        /// </summary>
        public static void ShowDamage(int healthDelta, Vector3 unitPosition, PanelSettings panelSettings,
            string note = null)
        {
            Show(healthDelta.ToString(), Settings.DamageColor, unitPosition + Settings.SpawnOffset, panelSettings,
                note: note);
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
        /// <paramref name="note"/> is a smaller word set beside it, in its own colour.
        /// </summary>
        public static void Show(string text, Color color, Vector3 worldPosition, PanelSettings panelSettings,
            int fontSize = 0, string note = null)
        {
            var popup = new GameObject("FloatingText");
            popup.transform.position = worldPosition;
            popup.transform.localScale = Vector3.one * Settings.DocumentScale;
            popup.layer = LayerMask.NameToLayer("UI");

            var document = popup.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            var floatingText = popup.AddComponent<FloatingText>();
            floatingText.basePosition = worldPosition;
            floatingText.BuildLabel(document, text, color, fontSize > 0 ? fontSize : Settings.FontSize, note);
        }

        private void BuildLabel(UIDocument document, string text, Color color, int fontSize, string note)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            // Start invisible so the label doesn't flash at full opacity before the first Update.
            root.style.opacity = 0f;

            var label = new Label(Compose(text, note, fontSize)) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityTextOutlineWidth = Settings.OutlineWidth;
            label.style.unityTextOutlineColor = Settings.OutlineColor;
            root.Add(label);
        }

        /// <summary>
        /// The number, and after it what the strike had to say for itself, marked up in its own size
        /// and colour. One label rather than two elements in a row: the popup builds its document in
        /// code, so its root is sized by its content and two elements in it end up drawn over each
        /// other - and one line of text puts the note on the number's baseline for nothing.
        /// </summary>
        private static string Compose(string text, string note, int fontSize)
        {
            if (string.IsNullOrEmpty(note))
                return text;

            // Relative, so the note keeps its proportion whatever size the text it stands beside is.
            var scale = Mathf.RoundToInt(100f * Settings.StrikeNoteFontSize / Mathf.Max(1, fontSize));
            var colour = ColorUtility.ToHtmlStringRGB(Settings.StrikeNoteColor);

            return $"{text} <size={scale}%><color=#{colour}>{note}</color></size>";
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
