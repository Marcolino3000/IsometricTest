using System;
using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The workbench where two items are made into one: a button in the bottom left corner, and the
    /// panel it opens. Two slots - a weapon on the left, a weapon or a passive item on the right -
    /// and a chance, printed under them, that the traits of the right one carry over to the left.
    ///
    /// A pure view, like <see cref="ItemBar"/>, and it holds the same two pieces of state the bar
    /// does: which slot is open and which entry of its column is highlighted. It is handed
    /// <see cref="ItemOption"/>s, sprites and finished strings, and it reports which slot was
    /// activated and which entry was picked; that there are items behind any of it, what may go in
    /// which slot and where the percentage comes from are all the
    /// <see cref="Runtime.Gameplay.Items.ItemManager"/>'s to know.
    ///
    /// Built in code on a <see cref="UIDocument"/> of its own, like <see cref="ItemPopup"/>, so the
    /// Systems prefab needs no further scene object. It shares the HUD's panel settings, which is
    /// also what makes a click on it a click on the HUD to the <c>Raycaster</c> - documents on one
    /// panel are picked together - so pressing the corner button never also raycasts into the world.
    ///
    /// While it is open it swallows input as an <see cref="IInputBlocker"/>: a modal the player
    /// opened deliberately must not also be moving units behind itself.
    /// </summary>
    public class MergeScreen : MonoBehaviour, IInputBlocker
    {
        /// <summary>The two sides, which are what every call here is indexed by.</summary>
        public const int LeftSide = 0;
        public const int RightSide = 1;

        private const int SideCount = 2;

        /// <summary>Stands for "no slot" and "no entry" alike - both are indices into a list.</summary>
        private const int NoSelection = -1;

        private const float SlotSize = 84f;
        private const float PickerGap = 10f;

        /// <summary>
        /// What an item slot of the bar looks like - <c>.slot</c> in <c>itemBar.uss</c>. The corner
        /// button wears it rather than the card's own frame: it stands on the HUD beside that row
        /// and is pressed the same way, so it should read as one of them and not as a piece of the
        /// panel it opens. Repeated here because the bar's look is authored in USS and this document
        /// is built in code; keep the two in step if the row is ever resized.
        /// </summary>
        private const float BarSlotSize = 96f;
        private const float BarSlotBorder = 3f;
        private const float BarSlotRadius = 6f;
        private static readonly Color BarSlotBackground = new(0.157f, 0.157f, 0.188f, 0.9f);

        private const string Heading = "Merge Two Items";
        private const string Subheading = "The traits of the right item are added to the left item.";

        /// <summary>
        /// What each slot is for, written under it while it is empty - which of the two survives the
        /// merge. Replaced by the name of whatever is put in it, so the panel can be read without
        /// hovering anything.
        /// </summary>
        private static readonly string[] SideCaptions = { "Improved", "Consumed" };

        /// <summary>Raised with the side the player wants to see the fitting items of.</summary>
        public event Action<int> SlotActivated;

        /// <summary>Raised with the side and the index of the entry that was chosen.</summary>
        public event Action<int, int> OptionChosen;

        /// <summary>Raised when the merge button is pressed.</summary>
        public event Action MergeRequested;

        /// <summary>Raised when the panel goes up, so the owner of the items can fill it.</summary>
        public event Action Opened;

        private VisualElement root;
        private VisualElement overlay;
        private VisualElement picker;
        private Label chance;
        private Label notice;
        private Button mergeButton;

        private readonly VisualElement[] slots = new VisualElement[SideCount];
        private readonly VisualElement[] slotIcons = new VisualElement[SideCount];
        private readonly Label[] slotCaptions = new Label[SideCount];

        private readonly List<VisualElement> options = new();

        private int openSide = NoSelection;
        private int highlightedOption = NoSelection;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// True while the panel is up. Nothing below it is announced then, so the click that picks
        /// an item out of a column does not also send a unit walking across the map.
        /// </summary>
        public bool BlocksInput => IsOpen;

        /// <summary>
        /// Creates the screen on a document of its own. <paramref name="panelSettings"/> is the
        /// HUD's, so it scales with the rest of the interface and is picked together with it, and
        /// <paramref name="buttonIcon"/> is the symbol the corner button wears - authored in the
        /// action icon table like every other symbol standing for a kind of action.
        /// </summary>
        public static MergeScreen Create(PanelSettings panelSettings, float sortingOrder, Sprite buttonIcon)
        {
            var host = new GameObject(nameof(MergeScreen));

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var screen = host.AddComponent<MergeScreen>();
            screen.Build(document, buttonIcon);

            return screen;
        }

        /// <summary>
        /// Puts one side's slot up: the symbol, the name written under it, and the full description
        /// on hover. An empty slot falls back to saying what it is for.
        /// </summary>
        public void SetSlot(int side, Sprite icon, string label, string tooltip)
        {
            if (side < 0 || side >= SideCount)
                return;

            slotIcons[side].style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);

            bool named = !string.IsNullOrWhiteSpace(label);

            slotCaptions[side].text = named ? label : SideCaptions[side];
            slotCaptions[side].style.color = named ? CardStyle.Text : CardStyle.MutedText;
            slots[side].tooltip = tooltip ?? string.Empty;
        }

        /// <summary>The odds, already phrased. Empty takes the line out of the layout.</summary>
        public void SetChance(string text)
        {
            SetText(chance, text);
        }

        /// <summary>
        /// The line under the odds: why the pair cannot be merged while it cannot, and how the last
        /// merge went once it can.
        /// </summary>
        public void SetNotice(string text)
        {
            SetText(notice, text);
        }

        public void SetMergeEnabled(bool enabled)
        {
            mergeButton.SetEnabled(enabled);
            mergeButton.style.opacity = enabled ? 1f : 0.45f;
        }

        /// <summary>
        /// Offers <paramref name="items"/> above one of the slots with <paramref name="highlighted"/>
        /// pre-selected - what the owner of the items answers a <see cref="SlotActivated"/> with.
        /// </summary>
        public void OpenPicker(int side, IReadOnlyList<ItemOption> items, int highlighted)
        {
            ClosePicker();

            if (side < 0 || side >= SideCount || items == null || items.Count == 0)
                return;

            openSide = side;

            for (int i = 0; i < items.Count; i++)
                AddOption(items[i], i);

            // An entry outside the list (nothing chosen yet) starts the walk at the first one.
            Highlight(highlighted >= 0 && highlighted < options.Count ? highlighted : 0);

            picker.style.display = DisplayStyle.Flex;

            // Measured once the column has been laid out, since it is anchored to the slot it belongs
            // to and the panel may still be settling in the frame it opens.
            picker.schedule.Execute(() => PlacePicker(side));
        }

        public void ClosePicker()
        {
            openSide = NoSelection;
            highlightedOption = NoSelection;

            options.Clear();

            if (picker == null)
                return;

            picker.Clear();
            picker.style.display = DisplayStyle.None;
        }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            overlay.pickingMode = PickingMode.Position;

            Opened?.Invoke();
        }

        public void Close()
        {
            ClosePicker();

            IsOpen = false;

            if (overlay == null)
                return;

            overlay.style.display = DisplayStyle.None;
            overlay.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// What a click beside the card does: takes back the last thing that was opened. A column
        /// standing open is a choice being made, so that goes first and the panel stays.
        /// </summary>
        private void DismissOutside()
        {
            if (openSide != NoSelection)
                ClosePicker();
            else
                Close();
        }

        private void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>
        /// Escape closes it - the column first, then the panel. Read straight off the keyboard rather
        /// than through the <see cref="InputHandler"/>, because the panel is blocking it: nothing
        /// below is announced while a modal is up, which is the point of blocking and would swallow
        /// the very key meant to take the modal away.
        /// </summary>
        private void Update()
        {
            if (!IsOpen)
                return;

            var keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            DismissOutside();
        }

        private void Choose(int option)
        {
            int side = openSide;

            ClosePicker();

            OptionChosen?.Invoke(side, option);
        }

        private void Highlight(int index)
        {
            highlightedOption = index;

            for (int i = 0; i < options.Count; i++)
                SetHighlighted(options[i], i == highlightedOption);
        }

        /// <summary>
        /// One entry of a column: the symbol, and the name beside it. Named rather than left as a
        /// bare icon like the bar's entries are, because the two sides offer different things out of
        /// one inventory and which is which has to be readable at a glance.
        /// </summary>
        private void AddOption(ItemOption item, int index)
        {
            var option = new VisualElement();
            option.style.flexDirection = FlexDirection.Row;
            option.style.alignItems = Align.Center;
            option.style.marginTop = 4f;
            option.style.minWidth = 200f;
            option.style.backgroundColor = CardStyle.Background;
            CardStyle.SetBorder(option, 2f, 6f);
            option.pickingMode = PickingMode.Position;
            option.tooltip = item.Tooltip ?? string.Empty;

            var frame = Frame(option, 52f);
            frame.pickingMode = PickingMode.Ignore;
            frame.style.flexShrink = 0f;

            var icon = Icon(frame);
            icon.style.backgroundImage = item.Icon != null
                ? new StyleBackground(item.Icon)
                : new StyleBackground(StyleKeyword.None);

            var label = Text(option, 14f, CardStyle.Text);
            label.text = item.Label ?? string.Empty;
            label.style.flexGrow = 1f;
            label.style.marginLeft = 8f;
            label.style.marginRight = 10f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;

            option.RegisterCallback<ClickEvent>(evt =>
            {
                // The overlay behind would otherwise take the same click and close the column again.
                evt.StopPropagation();
                Choose(index);
            });

            options.Add(option);
            picker.Add(option);
        }

        /// <summary>
        /// Anchors the column to the top centre of its slot, the way the item bar's picker is: the
        /// translate centres it and lifts it above that point, so how tall it turned out is never
        /// measured.
        /// </summary>
        private void PlacePicker(int side)
        {
            Rect bounds = slots[side].worldBound;
            Vector2 anchor = overlay.WorldToLocal(new Vector2(bounds.center.x, bounds.yMin));

            picker.style.left = anchor.x;
            picker.style.top = anchor.y - PickerGap;
        }

        private void Build(UIDocument document, Sprite buttonIcon)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;

            BuildCornerButton(buttonIcon);
            BuildOverlay();
        }

        /// <summary>
        /// The button that opens the workbench, in the bottom left corner - the opposite corner from
        /// the next-turn button, and clear of the item bar in the middle.
        /// </summary>
        private void BuildCornerButton(Sprite icon)
        {
            var button = Frame(root, BarSlotSize);
            button.style.position = Position.Absolute;
            button.style.left = 16f;
            button.style.bottom = 16f;
            button.style.backgroundColor = BarSlotBackground;
            CardStyle.SetBorder(button, BarSlotBorder, BarSlotRadius);
            button.pickingMode = PickingMode.Position;
            button.tooltip = Heading;

            var symbol = Icon(button);
            symbol.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);

            button.RegisterCallback<ClickEvent>(_ => Toggle());
        }

        private void BuildOverlay()
        {
            overlay = new VisualElement { name = "mergeOverlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.top = 0f;
            overlay.style.right = 0f;
            overlay.style.bottom = 0f;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            // Dimmed rather than opaque: the board is not in the way here, and seeing it keeps the
            // panel reading as something opened over the game rather than as a screen of its own.
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            overlay.style.display = DisplayStyle.None;

            // A click that lands beside the card puts things away, an open column first and then the
            // panel - the same order escape does it in, and the same thing clicking off a tooltip
            // does. The click is spent on that and reaches nothing else: the press it began with
            // landed while this was still blocking, and the InputHandler announces a left click on
            // the press while UI Toolkit reports one on the release, so the world never hears it.
            overlay.RegisterCallback<ClickEvent>(_ => DismissOutside());

            root.Add(overlay);

            BuildCard();
            BuildPicker();
        }

        private void BuildCard()
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.alignItems = Align.Center;
            card.style.maxWidth = 460f;
            CardStyle.SetPadding(card, 22f, 26f);
            card.style.backgroundColor = CardStyle.Background;
            CardStyle.SetBorder(card, 2f, 8f);

            // The card swallows its own clicks: one that lands on it is not one that lands beside it.
            card.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            var title = Text(card, 24f, CardStyle.Text);
            title.text = Heading;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            // A little smaller and quieter: it says what the two slots do, not what the panel is.
            var subtitle = Text(card, 14f, CardStyle.MutedText);
            subtitle.text = Subheading;
            subtitle.style.marginTop = 4f;

            BuildSlots(card);

            chance = Text(card, 19f, CardStyle.StatText);
            chance.style.marginTop = 18f;

            notice = Text(card, 14f, CardStyle.MutedText);
            notice.style.marginTop = 6f;

            BuildButtons(card);

            overlay.Add(card);
        }

        private void BuildSlots(VisualElement card)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 20f;

            // Right to left, because that is the way the traits travel - the arrow between the two
            // slots is the same sentence the subtitle is.
            BuildSlot(row, LeftSide);

            var arrow = Text(row, 26f, CardStyle.MutedText);
            arrow.text = "←";
            arrow.style.marginLeft = 14f;
            arrow.style.marginRight = 14f;

            BuildSlot(row, RightSide);

            card.Add(row);
        }

        private void BuildSlot(VisualElement row, int side)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.alignItems = Align.Center;

            var slot = Frame(column, SlotSize);
            slot.pickingMode = PickingMode.Position;
            slot.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                ActivateSlot(side);
            });

            slots[side] = slot;
            slotIcons[side] = Icon(slot);

            var caption = Text(column, 13f, CardStyle.MutedText);
            caption.text = SideCaptions[side];
            caption.style.marginTop = 6f;
            // Held to the slot's width, so a long name wraps under it instead of pushing the two
            // slots and the arrow between them apart.
            caption.style.width = SlotSize + 24f;

            slotCaptions[side] = caption;

            row.Add(column);
        }

        /// <summary>
        /// Clicking a slot opens its column, clicking it again puts the column away - the choice
        /// itself is made by clicking an entry, exactly as on the item bar.
        /// </summary>
        private void ActivateSlot(int side)
        {
            bool wasOpen = openSide == side;

            ClosePicker();

            if (wasOpen)
                return;

            SlotActivated?.Invoke(side);
        }

        private void BuildButtons(VisualElement card)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 20f;

            mergeButton = TextButton(row, "Merge", () => MergeRequested?.Invoke());
            TextButton(row, "Close", Close).style.marginLeft = 10f;

            card.Add(row);
        }

        private void BuildPicker()
        {
            // In the overlay rather than in the card: it is positioned freely above whichever slot is
            // open, so it must not be laid out by the card's own column.
            picker = new VisualElement { name = "mergePicker", pickingMode = PickingMode.Ignore };
            picker.style.position = Position.Absolute;
            picker.style.flexDirection = FlexDirection.ColumnReverse;
            picker.style.alignItems = Align.Center;
            picker.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-100f));
            picker.style.display = DisplayStyle.None;

            overlay.Add(picker);
        }

        /// <summary>A bordered square - a slot, a picker entry or the corner button.</summary>
        private static VisualElement Frame(VisualElement parent, float size)
        {
            var frame = new VisualElement();
            frame.style.width = size;
            frame.style.height = size;
            frame.style.alignItems = Align.Center;
            frame.style.justifyContent = Justify.Center;
            frame.style.backgroundColor = CardStyle.OverlayBackground;
            CardStyle.SetBorder(frame, 2f, 6f);

            parent.Add(frame);

            return frame;
        }

        /// <summary>The symbol inside a frame, inset a little and scaled to fit.</summary>
        private static VisualElement Icon(VisualElement parent)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.flexGrow = 1f;
            icon.style.width = Length.Percent(100f);
            icon.style.marginTop = 6f;
            icon.style.marginRight = 6f;
            icon.style.marginBottom = 6f;
            icon.style.marginLeft = 6f;
            icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            icon.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));

            parent.Add(icon);

            return icon;
        }

        private static void SetHighlighted(VisualElement element, bool highlighted)
        {
            var border = highlighted ? CardStyle.StatText : CardStyle.Border;

            element.style.borderTopColor = border;
            element.style.borderRightColor = border;
            element.style.borderBottomColor = border;
            element.style.borderLeftColor = border;
        }

        private static Label Text(VisualElement parent, float fontSize, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            parent.Add(label);

            return label;
        }

        private static Button TextButton(VisualElement parent, string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.fontSize = 16f;
            button.style.color = CardStyle.Text;
            button.style.backgroundColor = CardStyle.OverlayBackground;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            CardStyle.SetPadding(button, 8f, 22f);
            CardStyle.SetBorder(button, 2f, 6f);

            parent.Add(button);

            return button;
        }

        /// <summary>Fills a label, or takes it out of the layout when there is nothing to say.</summary>
        private static void SetText(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
