using System;
using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// One entry of a slot's picker — everything the bar needs to draw a choice, and nothing about
    /// what that choice is. The owner of the items builds these.
    /// </summary>
    public readonly struct ItemOption
    {
        public readonly Sprite Icon;

        /// <summary>What the card labelling this entry says - see <see cref="TooltipContent"/>.</summary>
        public readonly TooltipContent Tooltip;

        /// <summary>
        /// What to write under the icon where a view has room for it - the item's name. The bar has
        /// none and ignores it, drawing a tight row of symbols; the merge screen offers its columns
        /// with more space around them and prints it, so a choice there can be read without waiting
        /// out a hover.
        /// </summary>
        public readonly string Label;

        public ItemOption(Sprite icon, TooltipContent tooltip, string label = null)
        {
            Icon = icon;
            Tooltip = tooltip;
            Label = label;
        }
    }

    /// <summary>
    /// What a slot looks like whatever is in it - the category's own look, pushed by the owner of the
    /// items the way an <see cref="ItemOption"/> is. The bar never learns what a category is: it draws
    /// the <see cref="Ghost"/> while the slot holds nothing, wears the <see cref="Accent"/> either
    /// way, and leaves a gap in front of a slot that opens a group.
    /// </summary>
    public readonly struct SlotLook
    {
        /// <summary>Symbol shown, faded, while the slot is empty - what belongs in it.</summary>
        public readonly Sprite Ghost;

        /// <summary>Colour of the strip along the slot's bottom edge.</summary>
        public readonly Color Accent;

        /// <summary>Whether the row is broken in front of this slot.</summary>
        public readonly bool StartsGroup;

        public SlotLook(Sprite ghost, Color accent, bool startsGroup)
        {
            Ghost = ghost;
            Accent = accent;
            StartsGroup = startsGroup;
        }
    }

    /// <summary>
    /// Row of item slots at the bottom of the screen. A slot stands for a category and shows what is
    /// equipped of it; pressing its number key or clicking it asks the owner of the items for
    /// everything that fits, which is then offered in a column above the slot. Repeating the key
    /// walks that column and space confirms; with the mouse the entry is clicked directly.
    /// The bar is a pure view: it knows which slot is open and which entry is highlighted, nothing more.
    /// </summary>
    public class ItemBar : MonoBehaviour
    {
        /// <summary>Raised with the slot the player wants to see the fitting items of.</summary>
        public event Action<int> SlotActivated;

        /// <summary>Raised with the slot and the index of the entry that was confirmed.</summary>
        public event Action<int, int> OptionChosen;

        /// <summary>
        /// Raised with the slot the cursor rests on, and with <see cref="NoSelection"/> when it
        /// leaves one. Its own event rather than the tooltip's timer: what a slot would cost is shown
        /// the moment it is pointed at, while its label waits out the hover delay.
        /// </summary>
        public event Action<int> SlotHovered;

        /// <summary>How many slots the bar actually built. Zero until its Awake has run.</summary>
        public int SlotCount => slots.Count;

        /// <summary>Stands for "no slot" and "no entry" alike — both are indices into a list.</summary>
        private const int NoSelection = -1;

        /// <summary>Distance between the top edge of a slot and its picker, in panel pixels.</summary>
        private const float PickerGap = 8f;

        [Tooltip("How many slots the bar shows. Has to match the categories the owner of the items " +
                 "lays out - it warns when it does not. Only the first nine are reachable by number key.")]
        [SerializeField] private int slotCount = 6;

        [Tooltip("Template instantiated once per slot and once per picker entry.")]
        [SerializeField] private VisualTreeAsset slotTemplate;

        private readonly List<VisualElement> slots = new();
        private readonly List<VisualElement> slotIcons = new();
        private readonly List<VisualElement> slotAccents = new();
        private readonly List<TooltipContent> slotTooltips = new();

        /// <summary>
        /// What each slot holds, and what its category shows in place of what it does not hold. Kept
        /// apart because they are pushed apart - see <see cref="ApplyIcon"/>.
        /// </summary>
        private readonly List<Sprite> slotSymbols = new();
        private readonly List<Sprite> slotGhosts = new();

        private readonly List<VisualElement> options = new();
        private readonly List<TooltipContent> optionTooltips = new();

        private InputHandler inputHandler;

        // The one owner of what the cursor is over. The bar reports the label of whatever it is on -
        // a slot, an entry - and the tooltip view draws it; nothing here knows what a tooltip is
        // besides a value pushed at it, exactly as an icon is.
        private HoverTarget hoverTarget;

        private VisualElement container;
        private VisualElement hudRoot;
        private VisualElement picker;
        private int hoveredSlot = NoSelection;

        private int openSlot = NoSelection;
        private int highlightedOption = NoSelection;

        public void Setup(InputHandler handler, HoverTarget hover)
        {
            inputHandler = handler;
            hoverTarget = hover;
            inputHandler.NumberKeyPressed += HandleNumberKey;
            inputHandler.ConfirmPressed += ConfirmHighlighted;
            inputHandler.CancelPressed += ClosePicker;
        }

        /// <summary>
        /// Offers <paramref name="items"/> above <paramref name="slot"/> with
        /// <paramref name="highlighted"/> pre-selected — what the owner of the items answers a
        /// <see cref="SlotActivated"/> with. Nothing to offer means no picker, so a slot whose
        /// category the player owns nothing of simply does not react.
        /// </summary>
        public void OpenPicker(int slot, IReadOnlyList<ItemOption> items, int highlighted)
        {
            ClosePicker();

            if (slot < 0 || slot >= slots.Count || items == null || items.Count == 0)
                return;

            openSlot = slot;

            for (int i = 0; i < items.Count; i++)
                AddOption(items[i], i);

            // An entry outside the list (nothing equipped there) starts the walk at the first one.
            Highlight(highlighted >= 0 && highlighted < options.Count ? highlighted : 0);

            slots[slot].AddToClassList("slot--open");
            picker.style.display = DisplayStyle.Flex;

            PlacePicker(slot);
        }

        public void ClosePicker()
        {
            if (openSlot != NoSelection)
                slots[openSlot].RemoveFromClassList("slot--open");

            openSlot = NoSelection;
            highlightedOption = NoSelection;

            options.Clear();
            optionTooltips.Clear();

            if (picker == null)
                return;

            picker.Clear();
            picker.style.display = DisplayStyle.None;

            // The label goes back to the slot the cursor is on rather than away: closing a column is
            // routine - every refresh of the bar does it - and the slot under the cursor is still
            // being pointed at, so taking its label away would restart the delay for nothing.
            ReturnTooltipToSlot();
        }

        /// <summary>
        /// Sets what a slot says on hover. <see cref="TooltipContent.Empty"/> turns its label off.
        /// </summary>
        public void SetSlotTooltip(int index, TooltipContent content)
        {
            if (index < 0 || index >= slotTooltips.Count)
                return;

            slotTooltips[index] = content;

            // The cursor may be resting on the slot that has just changed hands, and it will not
            // enter it a second time - the open label would go on describing what was in it.
            if (hoveredSlot == index)
                ReportTooltip(slots[index], content, TooltipSide.Above);
        }

        /// <summary>
        /// Marks the slot holding what is actually in use. Several slots can show something at once,
        /// so the owner of the items says which one of them is the active one.
        /// </summary>
        public void SetSlotActive(int index, bool active)
        {
            if (index < 0 || index >= slots.Count)
                return;

            slots[index].EnableInClassList("slot--active", active);
        }

        /// <summary>
        /// Sets the symbol a slot shows. A null sprite falls back to the category's own symbol from
        /// <see cref="SetSlotLook"/>, so an empty slot still says what belongs in it.
        /// </summary>
        public void SetSlotIcon(int index, Sprite sprite)
        {
            if (index < 0 || index >= slotIcons.Count)
                return;

            slotSymbols[index] = sprite;

            ApplyIcon(index);
        }

        /// <summary>
        /// Sets what a slot looks like whatever it holds - see <see cref="SlotLook"/>.
        /// </summary>
        public void SetSlotLook(int index, SlotLook look)
        {
            if (index < 0 || index >= slots.Count)
                return;

            slotGhosts[index] = look.Ghost;
            slotAccents[index].style.backgroundColor = look.Accent;
            slots[index].EnableInClassList("slot--group-start", look.StartsGroup);

            ApplyIcon(index);
        }

        /// <summary>
        /// Draws what a slot holds, or its category's symbol faded out while it holds nothing. The two
        /// are pushed separately and in no fixed order, so which of them shows is decided here rather
        /// than by whichever arrived last.
        /// </summary>
        private void ApplyIcon(int index)
        {
            var symbol = slotSymbols[index];
            var empty = symbol == null;

            SetIcon(slotIcons[index], empty ? slotGhosts[index] : symbol);
            slotIcons[index].EnableInClassList("slot__icon--ghost", empty);
        }

        /// <summary>
        /// A number key opens its slot's picker; repeating the key of the open one walks its entries.
        /// Indices outside the bar are ignored, so the input handler can announce every number key
        /// without knowing how many slots exist.
        /// </summary>
        private void HandleNumberKey(int index)
        {
            if (index < 0 || index >= slots.Count)
                return;

            if (openSlot == index)
            {
                HighlightNext();
                return;
            }

            // Turning to another slot abandons the choice that was being made.
            ClosePicker();

            SlotActivated?.Invoke(index);
        }

        /// <summary>
        /// Clicking a slot opens its picker, clicking it again puts the picker away — the choice
        /// itself is made by clicking an entry, not the slot.
        /// </summary>
        private void HandleSlotClicked(int index)
        {
            bool wasOpen = openSlot == index;

            ClosePicker();

            if (wasOpen)
                return;

            SlotActivated?.Invoke(index);
        }

        private void ConfirmHighlighted()
        {
            if (openSlot == NoSelection)
                return;

            Choose(highlightedOption);
        }

        private void Choose(int option)
        {
            int slot = openSlot;

            ClosePicker();

            OptionChosen?.Invoke(slot, option);
        }

        private void HighlightNext()
        {
            Highlight((highlightedOption + 1) % options.Count);
        }

        private void Highlight(int index)
        {
            highlightedOption = index;

            for (int i = 0; i < options.Count; i++)
                options[i].EnableInClassList("slot--selected", i == highlightedOption);
        }

        private void AddOption(ItemOption item, int index)
        {
            var option = slotTemplate.Instantiate().Q("slot");

            // A focusable entry would keep keyboard focus after a click and swallow the number keys.
            option.focusable = false;
            option.AddToClassList("slot--option");

            // The number key belongs to the slot, not to the entries offered above it.
            option.Q<Label>("hotkey").style.display = DisplayStyle.None;
            SetIcon(option.Q<VisualElement>("icon"), item.Icon);

            option.RegisterCallback<ClickEvent>(_ => Choose(index));
            // Labelled from the side: the space above an entry is taken by the rest of the column.
            option.RegisterCallback<PointerEnterEvent>(_ =>
                ReportTooltip(option, optionTooltips[index], TooltipSide.Right));
            option.RegisterCallback<PointerLeaveEvent>(_ => ClearTooltip());

            options.Add(option);
            optionTooltips.Add(item.Tooltip);
            picker.Add(option);
        }

        /// <summary>
        /// Anchors the picker to the top center of its slot. The USS translate centers the column and
        /// lifts it above that point, so how tall the column turned out never has to be measured.
        /// </summary>
        private void PlacePicker(int slot)
        {
            Rect bounds = slots[slot].worldBound;
            Vector2 anchor = hudRoot.WorldToLocal(new Vector2(bounds.center.x, bounds.yMin));

            picker.style.left = anchor.x;
            picker.style.top = anchor.y - PickerGap;
        }

        private static void SetIcon(VisualElement icon, Sprite sprite)
        {
            icon.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : new StyleBackground(StyleKeyword.None);
        }

        // Built in Awake like the NextTurnButton caches its button: the UIDocument has already
        // created its tree by then, and Setup is left to do nothing but wire events.
        private void Awake()
        {
            BuildSlots();
        }

        private void BuildSlots()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            hudRoot = root.Q<VisualElement>("hudRoot");
            container = root.Q<VisualElement>("container");

            slots.Clear();
            slotIcons.Clear();
            slotAccents.Clear();
            slotSymbols.Clear();
            slotGhosts.Clear();
            slotTooltips.Clear();
            container.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                int index = i;
                var slot = slotTemplate.Instantiate().Q("slot");

                // A focusable slot would keep keyboard focus after a click and swallow the number keys.
                slot.focusable = false;
                slot.Q<Label>("hotkey").text = (i + 1).ToString();
                slot.RegisterCallback<ClickEvent>(_ => HandleSlotClicked(index));
                slot.RegisterCallback<PointerEnterEvent>(_ => HoverSlot(index, slot));
                slot.RegisterCallback<PointerLeaveEvent>(_ => LeaveSlot());

                slots.Add(slot);
                slotIcons.Add(slot.Q<VisualElement>("icon"));
                slotAccents.Add(slot.Q<VisualElement>("accent"));
                // All three are pushed by the ItemManager - the item the slot holds, what its
                // category shows in place of it, and the label. Empty means none of them.
                slotSymbols.Add(null);
                slotGhosts.Add(null);
                slotTooltips.Add(TooltipContent.Empty);
                container.Add(slot);
            }

            BuildPicker();
        }

        /// <summary>
        /// The picker hangs in the HUD root rather than in the slot row: it is positioned freely
        /// above whichever slot is open, the same way the tooltip is.
        /// </summary>
        private void BuildPicker()
        {
            picker = new VisualElement { name = "picker" };
            picker.AddToClassList("slot-picker");
            picker.style.display = DisplayStyle.None;

            hudRoot.Add(picker);
        }

        private void HoverSlot(int index, VisualElement slot)
        {
            hoveredSlot = index;

            // Said first, so the owner of the items has set which slot the cursor is on before the
            // report below wakes everything that reads the hover.
            SlotHovered?.Invoke(index);

            ReportTooltip(slot, slotTooltips[index], TooltipSide.Above);
        }

        private void LeaveSlot()
        {
            hoveredSlot = NoSelection;

            SlotHovered?.Invoke(NoSelection);

            ClearTooltip();
        }

        /// <summary>
        /// Hands the label of whatever the cursor is on to the one owner of the cursor. The delay
        /// before it opens, how it is placed and what it looks like are the tooltip view's - the bar
        /// says only what to say and where the thing being described is.
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
            if (hoveredSlot < 0 || hoveredSlot >= slots.Count)
                ClearTooltip();
            else
                ReportTooltip(slots[hoveredSlot], slotTooltips[hoveredSlot], TooltipSide.Above);
        }

        private void OnDestroy()
        {
            if (inputHandler == null)
                return;

            inputHandler.NumberKeyPressed -= HandleNumberKey;
            inputHandler.ConfirmPressed -= ConfirmHighlighted;
            inputHandler.CancelPressed -= ClosePicker;
        }
    }
}
