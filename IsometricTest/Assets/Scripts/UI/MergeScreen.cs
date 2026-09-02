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

        /// <summary>
        /// How an outcome is coloured - the gold and the red the game-over card states a won and a
        /// lost match in, so a result reads the same wherever the game announces one. Taken from
        /// <see cref="Banner"/>, which is also what draws them here.
        /// </summary>
        private static readonly Color SucceededAccent = Banner.Accent;
        private static readonly Color FailedAccent = Banner.Warning;

        /// <summary>
        /// The banner is as tall as this whether or not it says anything, so the card below it
        /// stands where it stood and a merge does not shove the panel down the screen.
        /// </summary>
        private const float OutcomeHeight = 92f;

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

        // How the last merge went, in the same two lines the map announces a new zone in - see
        // Banner. Only what it says is this screen's; the look of a headline over a sentence is not.
        private Banner outcome;

        private Button mergeButton;

        private readonly VisualElement[] slots = new VisualElement[SideCount];
        private readonly VisualElement[] slotIcons = new VisualElement[SideCount];
        private readonly Label[] slotCaptions = new Label[SideCount];

        /// <summary>What each slot says on hover. Kept because it changes under a resting cursor -
        /// an item put in the slot, or one an undo took back out of it.</summary>
        private readonly TooltipContent[] slotTooltips = { TooltipContent.Empty, TooltipContent.Empty };

        private readonly List<VisualElement> options = new();

        // The one owner of what the cursor is over. This screen reports the label of whatever it is
        // on and the tooltip view draws it - the same dialogue the item bar has, and the reason the
        // two windows can no longer label things differently.
        private HoverTarget hoverTarget;

        /// <summary>The slot the cursor rests on, or <see cref="NoSelection"/>.</summary>
        private int hoveredSide = NoSelection;

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

        public MergeScreen Setup(HoverTarget hover)
        {
            hoverTarget = hover;

            return this;
        }

        /// <summary>
        /// Puts one side's slot up: the symbol, the name written under it, and the full description
        /// on hover. An empty slot falls back to saying what it is for.
        /// </summary>
        public void SetSlot(int side, Sprite icon, string label, TooltipContent tooltip)
        {
            if (side < 0 || side >= SideCount)
                return;

            slotIcons[side].style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);

            bool named = !string.IsNullOrWhiteSpace(label);

            slotCaptions[side].text = named ? label : SideCaptions[side];
            slotCaptions[side].style.color = named ? CardStyle.Text : CardStyle.MutedText;
            slotTooltips[side] = tooltip;

            // An open label would otherwise go on describing what the slot held a moment ago.
            if (hoveredSide == side)
                ReportTooltip(slots[side], tooltip, SideOf(side));
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

        /// <summary>
        /// What the banner says, or nothing at all for an empty <paramref name="headline"/>. The
        /// two strings are finished, like every other one pushed here; <paramref name="succeeded"/>
        /// is only which of the two colours the word wears - the view knows no more about a merge
        /// than that it went one way or the other.
        /// </summary>
        public void SetOutcome(string headline, string detail, bool succeeded)
        {
            outcome.Set(headline, detail, succeeded ? SucceededAccent : FailedAccent);
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

            // The label goes back to the slot the cursor is on rather than away: a column is closed
            // on every refresh of the panel, and the slot under it is still being pointed at.
            ReturnTooltipToSlot();

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

            // The panel goes up under a cursor that may be resting on an item slot, and an element
            // covered without the pointer moving reports no leave - its label would linger over this.
            ClearTooltip();

            Opened?.Invoke();
        }

        public void Close()
        {
            ClosePicker();

            IsOpen = false;
            hoveredSide = NoSelection;

            // The panel goes out from under the cursor, so no element will report leaving it.
            ClearTooltip();

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

            // Labelled outwards from the card, on the side of the slot the column belongs to: the
            // space above an entry is taken by the rest of the column, as it is on the item bar.
            TooltipContent content = item.Tooltip;
            TooltipSide side = SideOf(openSide);
            option.RegisterCallback<PointerEnterEvent>(_ => ReportTooltip(option, content, side));
            option.RegisterCallback<PointerLeaveEvent>(_ => ClearTooltip());

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

            // From above, like an item slot of the bar it stands beside - there is nothing over it
            // but the map, and the screen edges leave no room to either side.
            var label = new TooltipContent(Heading, description: Subheading);
            button.RegisterCallback<PointerEnterEvent>(_ => ReportTooltip(button, label, TooltipSide.Above));
            button.RegisterCallback<PointerLeaveEvent>(_ => ClearTooltip());

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

            // A click on the dimmed ground *around* the card puts things away, an open column first
            // and then the panel - the same order escape does it in. Only around it: the card is
            // pickable and stops its own clicks here, so the empty space inside the panel is not
            // empty space. The click is spent on this and reaches nothing else - the press it began
            // with landed while this was still blocking, and the InputHandler announces a left click
            // on the press where UI Toolkit reports one on the release, so the world never hears it.
            overlay.RegisterCallback<ClickEvent>(_ => DismissOutside());

            root.Add(overlay);

            // Before the card, so it stands over the dimmed ground above the panel rather than
            // inside it; after it in the file only because the card is what it announces about.
            BuildOutcome();
            BuildCard();
            BuildPicker();
        }

        /// <summary>
        /// How the last merge went, said over the dimmed ground above the card rather than on the
        /// line inside it: a merge is a gamble and its result is the loudest thing on the screen,
        /// while the card goes on being the bench - it says what this pair is worth and what is
        /// missing, which is a different question from what the last one turned out to be.
        ///
        /// One word and a sentence, like the game-over card: the word carries the outcome and its
        /// colour, the line under it what actually changed hands.
        /// </summary>
        private void BuildOutcome()
        {
            // The height is reserved whether or not anything is said, which is what keeps the card
            // below standing where it stood: the two lines hang from the bottom of that block, so
            // the taller of them grows away from the card rather than pushing at it.
            outcome = Banner.Create(overlay, reservedHeight: OutcomeHeight);
            outcome.Root.style.marginBottom = 10f;
        }

        private void BuildCard()
        {
            // Pickable, which is what makes the bare parts of the card part of the card: an element
            // that ignores the pointer is not the target of a click on it, so one landing between
            // the heading and a slot would reach the overlay behind and be read as a click beside
            // the panel. Its contents stay as they are - a label ignores the pointer and bubbles up
            // to here, a slot or a button is the target and answers for itself.
            var card = new VisualElement { pickingMode = PickingMode.Position };
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
            slot.RegisterCallback<PointerEnterEvent>(_ => HoverSlot(side, slot));
            slot.RegisterCallback<PointerLeaveEvent>(_ => LeaveSlot());

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

        private void HoverSlot(int side, VisualElement slot)
        {
            hoveredSide = side;

            ReportTooltip(slot, slotTooltips[side], SideOf(side));
        }

        private void LeaveSlot()
        {
            hoveredSide = NoSelection;

            ClearTooltip();
        }

        /// <summary>
        /// Which way a thing belonging to one of the two slots is labelled - outwards, away from the
        /// middle of the card, so the label never covers the other slot it is being weighed against.
        /// </summary>
        private static TooltipSide SideOf(int side)
        {
            return side == LeftSide ? TooltipSide.Left : TooltipSide.Right;
        }

        /// <summary>
        /// Hands the label of whatever the cursor is on to the one owner of the cursor, exactly as
        /// the item bar does. How long it waits, where it goes and what it looks like are the
        /// tooltip view's - which is what makes the two windows label things the same way.
        /// </summary>
        private void ReportTooltip(VisualElement anchor, TooltipContent content, TooltipSide side)
        {
            hoverTarget?.SetUiTooltip(content, TooltipAnchor.Element(anchor, side));
        }

        private void ClearTooltip()
        {
            hoverTarget?.SetUiTooltip(TooltipContent.Empty, default);
        }

        /// <summary>Labels the slot the cursor rests on, or nothing while it rests on none.</summary>
        private void ReturnTooltipToSlot()
        {
            if (hoveredSide == NoSelection || slots[hoveredSide] == null)
                ClearTooltip();
            else
                ReportTooltip(slots[hoveredSide], slotTooltips[hoveredSide], SideOf(hoveredSide));
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
