using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// One find, as the popup draws it — a symbol, some text and a list of lines, and nothing about
    /// what any of it means. The owner of the items builds these, the same way it builds an
    /// <see cref="ItemOption"/> for the bar.
    /// </summary>
    public readonly struct ItemCard
    {
        public readonly Sprite Icon;
        public readonly string Title;

        /// <summary>What kind of thing it is — "Melee Weapon", "Active Item".</summary>
        public readonly string Kind;

        /// <summary>Where it went, already phrased — "Slot 1".</summary>
        public readonly string Slot;

        public readonly string Description;

        /// <summary>What it does in numbers, one line each.</summary>
        public readonly IReadOnlyList<string> Stats;

        public ItemCard(Sprite icon, string title, string kind, string slot, string description,
            IReadOnlyList<string> stats)
        {
            Icon = icon;
            Title = title;
            Kind = kind;
            Slot = slot;
            Description = description;
            Stats = stats;
        }
    }

    /// <summary>
    /// Card in the middle of the screen announcing an item the player has just found for the first
    /// time. It fades in and then stays: a find is read, not glimpsed, so it waits for the player to
    /// click or press something and fades out on that. A second find while one is still up replaces
    /// it rather than queueing behind it, since the newer card is the one being looked for.
    ///
    /// Built in code on a <see cref="UIDocument"/> of its own, like the floating damage text is, so
    /// the Systems prefab needs no further scene object — the Initiator creates it and hands it the
    /// HUD's panel settings.
    ///
    /// <b>The press that puts it away does nothing else.</b> It swallows input on both roads into the
    /// game while it is up: as an <see cref="IInputBlocker"/> the <see cref="InputHandler"/> announces
    /// nothing, which covers the world raycast and every key, and the card itself takes the pointer
    /// (its contents stay click-through, the card as a whole is what picks), which covers the HUD
    /// lying under it on the same panel — a slot or the next-turn button would otherwise be pressed
    /// through it. Both stop the moment it is dismissed, so the fade-out is already out of the way.
    ///
    /// A pure view, like <see cref="ItemBar"/>: it is handed strings and a sprite and knows nothing
    /// about items, slots or what a stat line means.
    /// </summary>
    public class ItemPopup : MonoBehaviour, IInputBlocker
    {
        private const float FadeInDuration = 0.2f;
        private const float FadeOutDuration = 0.35f;

        // Shared with every other panel built in code - see CardStyle, which is where they live now.
        protected static readonly Color Background = CardStyle.Background;
        protected static readonly Color Border = CardStyle.Border;
        protected static readonly Color Text = CardStyle.Text;
        protected static readonly Color MutedText = CardStyle.MutedText;
        protected static readonly Color StatText = CardStyle.StatText;

        private VisualElement root;

        // Filled by BuildCard, which is where a variant lays them out differently; everything else
        // about a card - what goes in these, when it appears, when it goes away - is the same.
        protected VisualElement Icon;
        protected VisualElement Stats;
        protected Label Title;

        /// <summary>What it is and which slot it went to, on one line under the name.</summary>
        protected Label Kind;

        protected Label Description;

        private bool showing;
        private bool dismissed;

        /// <summary>Seconds into the phase the card is in - fading in and holding, or fading out.</summary>
        private float age;

        /// <summary>How wide the card is allowed to get.</summary>
        protected virtual float CardWidth => 460f;

        /// <summary>
        /// True while the card is up and waiting to be read: the press that dismisses it is the
        /// card's own and must reach nothing else. False again the moment it is, so the fade-out
        /// costs the player no click.
        /// </summary>
        public bool BlocksInput => showing && !dismissed;

        /// <summary>
        /// Creates a popup of layout <typeparamref name="T"/> on a document of its own.
        /// <paramref name="panelSettings"/> is the HUD's, so the card scales with the rest of the
        /// interface, and <paramref name="sortingOrder"/> puts it above whatever it may overlap.
        /// </summary>
        public static T Create<T>(PanelSettings panelSettings, float sortingOrder) where T : ItemPopup
        {
            var host = new GameObject(typeof(T).Name);

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var popup = host.AddComponent<T>();
            popup.Build(document);

            return popup;
        }

        /// <summary>Puts <paramref name="card"/> up, replacing whatever was showing.</summary>
        public void Show(ItemCard card)
        {
            Icon.style.backgroundImage = card.Icon != null
                ? new StyleBackground(card.Icon)
                : new StyleBackground(StyleKeyword.None);

            Title.text = card.Title;

            // What it is and where it went belong together: both answer "what have I got now".
            SetText(Kind, Join(card.Kind, card.Slot));
            SetText(Description, card.Description);

            Stats.Clear();

            if (card.Stats != null)
            {
                foreach (var line in card.Stats)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        StatLine(line);
                }
            }

            Stats.style.display = Stats.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            age = 0f;
            showing = true;
            dismissed = false;
            ApplyBlocking();

            // Starts transparent so the card does not flash at full opacity before the first Update.
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            showing = false;

            if (root == null)
                return;

            root.style.display = DisplayStyle.None;
            ApplyBlocking();
        }

        /// <summary>
        /// Puts the pointer in step with <see cref="BlocksInput"/>: the card picks for exactly as
        /// long as it swallows, so one answer covers the HUD under it and the game behind that.
        /// </summary>
        private void ApplyBlocking()
        {
            if (root != null)
                root.pickingMode = BlocksInput ? PickingMode.Position : PickingMode.Ignore;
        }

        private void Update()
        {
            if (!showing)
                return;

            age += Time.deltaTime;

            if (dismissed)
            {
                if (age < FadeOutDuration)
                    root.style.opacity = 1f - age / FadeOutDuration;
                else
                    Hide();

                return;
            }

            root.style.opacity = Mathf.Min(1f, age / FadeInDuration);

            // Not before the card is fully up: the button that opened it is still down in the frame
            // it appears, and would otherwise close it in the same breath.
            if (age < FadeInDuration || !AnyInput())
                return;

            dismissed = true;
            age = 0f;
            ApplyBlocking();
        }

        /// <summary>
        /// Anything the player can press. Read off the devices rather than through the
        /// <see cref="InputHandler"/>: the card answers to no binding in particular, so there is no
        /// action to add to the asset - the point is that whatever the player reaches for puts it away.
        /// </summary>
        private static bool AnyInput()
        {
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;

            return mouse != null && (mouse.leftButton.wasPressedThisFrame
                                     || mouse.rightButton.wasPressedThisFrame
                                     || mouse.middleButton.wasPressedThisFrame);
        }

        private void Build(UIDocument document)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.display = DisplayStyle.None;

            var card = Element(root);
            card.style.maxWidth = CardWidth;
            card.style.paddingTop = 16f;
            card.style.paddingRight = 20f;
            card.style.paddingBottom = 16f;
            card.style.paddingLeft = 20f;
            card.style.backgroundColor = Background;
            SetBorder(card, 2f, 8f);

            BuildCard(card);
        }

        /// <summary>
        /// Fills the card, which is the whole of what one layout does differently from another: it
        /// creates the symbol, the name, the subtitle, the description and the container the stat
        /// lines go into, and arranges them. Everything else - what goes in them, when the card
        /// appears and what puts it away - is the same whichever way they are arranged.
        /// </summary>
        protected virtual void BuildCard(VisualElement card)
        {
            // Symbol beside the name, description and numbers underneath: a wide card.
            var header = Element(card);
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            BuildIcon(header, 72f);

            var heading = Element(header);
            heading.style.flexGrow = 1f;
            heading.style.marginLeft = 14f;

            Title = TextLabel(heading, 24f, Text);
            Title.style.unityFontStyleAndWeight = FontStyle.Bold;

            Kind = TextLabel(heading, 15f, MutedText);
            Kind.style.marginTop = 2f;

            Description = TextLabel(card, 16f, Text);
            Description.style.marginTop = 12f;

            Stats = Element(card);
            Stats.style.marginTop = 10f;
        }

        /// <summary>Creates the symbol at <paramref name="size"/> square, scaled to fit it.</summary>
        protected void BuildIcon(VisualElement parent, float size)
        {
            Icon = Element(parent);
            Icon.style.width = size;
            Icon.style.height = size;
            Icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            Icon.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
        }

        /// <summary>Adds one line of numbers. Overridden where the lines sit differently.</summary>
        protected virtual void StatLine(string text)
        {
            TextLabel(Stats, 16f, StatText).text = text;
        }

        /// <summary>The two halves of the subtitle, or whichever of them there is.</summary>
        private static string Join(string kind, string slot)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return slot;

            return string.IsNullOrWhiteSpace(slot) ? kind : $"{kind}  ·  {slot}";
        }

        /// <summary>Fills a label, or takes it out of the layout when there is nothing to say.</summary>
        private static void SetText(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected static VisualElement Element(VisualElement parent)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(element);

            return element;
        }

        protected static Label TextLabel(VisualElement parent, float fontSize, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);

            return label;
        }

        private static void SetBorder(VisualElement element, float width, float radius)
        {
            CardStyle.SetBorder(element, width, radius);
        }
    }
}
