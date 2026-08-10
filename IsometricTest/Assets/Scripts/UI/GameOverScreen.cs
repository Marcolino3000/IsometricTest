using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The card in the middle of a dimmed screen saying the match has been won or lost. It puts itself
    /// up when <see cref="MatchOutcomeWatcher"/> announces a verdict and takes itself down again when
    /// the verdict does - which is what an undo does, so stepping back past the deciding blow returns
    /// the board without anything having to dismiss the screen.
    ///
    /// Nothing dismisses it otherwise: the match is over, so there is nothing behind it to get back to
    /// but the history, and the hint line says which key that is. Built in code on a
    /// <see cref="UIDocument"/> of its own like <see cref="ItemPopup"/>, and click-through in the same
    /// way - every element ignores picking, so it takes no input away from the board underneath and the
    /// undo key reaches the history exactly as it does during play.
    ///
    /// A pure view: it is handed an outcome and a sentence, and knows nothing about what decided it.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        private const float FadeInDuration = 0.4f;

        private static readonly Color Dim = new(0f, 0f, 0f, 0.55f);
        private static readonly Color Background = new(0.07f, 0.07f, 0.086f);
        private static readonly Color Text = new(0.92f, 0.92f, 0.92f);
        private static readonly Color MutedText = new(1f, 1f, 1f, 0.5f);
        private static readonly Color WonAccent = new(0.98f, 0.82f, 0.29f);
        private static readonly Color LostAccent = new(0.89f, 0.31f, 0.27f);

        private VisualElement root;
        private VisualElement card;
        private Label title;
        private Label reason;
        private Label hint;

        private MatchOutcomeWatcher watcher;
        private bool showing;
        private float age;

        /// <summary>
        /// Creates the screen on a document of its own. <paramref name="panelSettings"/> is the HUD's,
        /// so it scales with the rest of the interface, and <paramref name="sortingOrder"/> puts it
        /// above everything it covers.
        /// </summary>
        public static GameOverScreen Create(PanelSettings panelSettings, float sortingOrder)
        {
            var host = new GameObject(nameof(GameOverScreen));

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var screen = host.AddComponent<GameOverScreen>();
            screen.Build(document);

            return screen;
        }

        /// <summary>
        /// Subscribes to the verdict. <paramref name="undoKey"/> is asked of the history rather than
        /// written out here, so the hint keeps naming the key that actually steps back.
        /// </summary>
        public void Setup(MatchOutcomeWatcher matchOutcomeWatcher, Key undoKey)
        {
            watcher = matchOutcomeWatcher;
            hint.text = $"{undoKey} takes the last action back";

            watcher.OutcomeChanged += HandleOutcomeChanged;

            // A verdict may already stand - the screen is set up after the board is.
            HandleOutcomeChanged(watcher.Result);
        }

        private void OnDestroy()
        {
            if (watcher != null)
                watcher.OutcomeChanged -= HandleOutcomeChanged;
        }

        private void HandleOutcomeChanged(MatchResult result)
        {
            if (result.IsOver)
                Show(result);
            else
                Hide();
        }

        private void Show(MatchResult result)
        {
            var won = result.Outcome == MatchOutcome.Victory;
            var accent = won ? WonAccent : LostAccent;

            title.text = won ? "Victory" : "Defeat";
            title.style.color = accent;
            SetBorderColor(card, accent);

            reason.text = result.Reason;
            reason.style.display = string.IsNullOrWhiteSpace(result.Reason)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            age = 0f;
            showing = true;

            // Starts transparent so the screen does not flash at full opacity before the first Update.
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            showing = false;

            if (root != null)
                root.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!showing || age >= FadeInDuration)
                return;

            age += Time.deltaTime;
            root.style.opacity = Mathf.Min(1f, age / FadeInDuration);
        }

        private void Build(UIDocument document)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = Dim;
            root.style.display = DisplayStyle.None;

            card = Element(root);
            card.style.minWidth = 340f;
            card.style.maxWidth = 520f;
            card.style.alignItems = Align.Center;
            card.style.paddingTop = 26f;
            card.style.paddingRight = 32f;
            card.style.paddingBottom = 26f;
            card.style.paddingLeft = 32f;
            card.style.backgroundColor = Background;
            SetBorder(card, 2f, 10f);

            title = TextLabel(card, 46f, Text);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 4f;

            reason = TextLabel(card, 17f, Text);
            reason.style.marginTop = 10f;

            hint = TextLabel(card, 13f, MutedText);
            hint.style.marginTop = 22f;
        }

        private static VisualElement Element(VisualElement parent)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(element);

            return element;
        }

        private static Label TextLabel(VisualElement parent, float fontSize, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(label);

            return label;
        }

        private static void SetBorder(VisualElement element, float width, float radius)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;

            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }
    }
}
