using System;
using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// Row of item slots at the bottom of the screen. A slot is selected by clicking it or by
    /// pressing its number key; selecting the already selected slot clears the selection.
    /// Resting the cursor on a slot opens its tooltip.
    /// The bar is a pure view: it owns which slot is armed and announces it, nothing more.
    /// </summary>
    public class ItemBar : MonoBehaviour
    {
        /// <summary>Raised with the selected slot index, or -1 when the selection was cleared.</summary>
        public event Action<int> SlotSelected;

        public const int NoSelection = -1;

        public int SelectedIndex { get; private set; } = NoSelection;

        /// <summary>Distance between the top edge of a slot and its tooltip, in panel pixels.</summary>
        private const float TooltipGap = 8f;

        [Tooltip("How many slots the bar shows. Only the first nine are reachable by number key.")]
        [SerializeField] private int slotCount = 6;

        [Tooltip("Seconds the cursor has to rest on a slot before its tooltip opens.")]
        [SerializeField] private float tooltipDelay = 0.5f;

        [Tooltip("Template instantiated once per slot.")]
        [SerializeField] private VisualTreeAsset slotTemplate;

        private readonly List<VisualElement> slots = new();
        private readonly List<string> slotTooltips = new();

        private InputHandler inputHandler;
        private VisualElement container;
        private VisualElement hudRoot;
        private Label tooltipLabel;
        private IVisualElementScheduledItem tooltipTimer;
        private int hoveredIndex = NoSelection;

        public void Setup(InputHandler handler)
        {
            inputHandler = handler;
            inputHandler.NumberKeyPressed += Select;
        }

        /// <summary>
        /// Selects the slot at <paramref name="index"/>, or clears the selection when that slot is
        /// already selected. Indices outside the bar are ignored, so the input handler can announce
        /// every number key without knowing how many slots exist.
        /// </summary>
        public void Select(int index)
        {
            if (index < 0 || index >= slots.Count)
                return;

            SelectedIndex = index == SelectedIndex ? NoSelection : index;

            for (int i = 0; i < slots.Count; i++)
                slots[i].EnableInClassList("slot--selected", i == SelectedIndex);

            SlotSelected?.Invoke(SelectedIndex);
        }

        public void ClearSelection()
        {
            if (SelectedIndex == NoSelection)
                return;

            Select(SelectedIndex);
        }

        /// <summary>
        /// Sets the text a slot shows on hover. Empty text turns the slot's tooltip off.
        /// </summary>
        public void SetSlotTooltip(int index, string text)
        {
            if (index < 0 || index >= slotTooltips.Count)
                return;

            slotTooltips[index] = text;

            // The open tooltip would otherwise keep showing the previous text.
            if (hoveredIndex == index)
                HideTooltip();
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
            tooltipLabel = root.Q<Label>("tooltip");

            slots.Clear();
            slotTooltips.Clear();
            container.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                int index = i;
                var slot = slotTemplate.Instantiate().Q("slot");

                // A focusable slot would keep keyboard focus after a click and swallow the number keys.
                slot.focusable = false;
                slot.Q<Label>("hotkey").text = (i + 1).ToString();
                slot.RegisterCallback<ClickEvent>(_ => Select(index));
                slot.RegisterCallback<PointerEnterEvent>(_ => ScheduleTooltip(index));
                slot.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());

                slots.Add(slot);
                // Placeholder until slots carry item data; replaced through SetSlotTooltip.
                slotTooltips.Add($"Slot {i + 1}");
                container.Add(slot);
            }
        }

        private void ScheduleTooltip(int index)
        {
            HideTooltip();

            if (string.IsNullOrEmpty(slotTooltips[index]))
                return;

            hoveredIndex = index;

            // Scheduled on the bar rather than on the tooltip: the timer has to keep running
            // while the tooltip itself is still hidden.
            tooltipTimer = container.schedule
                .Execute(() => ShowTooltip(index))
                .StartingIn((long)(tooltipDelay * 1000f));
        }

        private void ShowTooltip(int index)
        {
            tooltipLabel.text = slotTooltips[index];
            tooltipLabel.style.display = DisplayStyle.Flex;

            // Anchored to the top center of the slot; the USS translate does the centering and
            // lifts the tooltip above that point, so its size never has to be measured here.
            Rect slot = slots[index].worldBound;
            Vector2 anchor = hudRoot.WorldToLocal(new Vector2(slot.center.x, slot.yMin));

            tooltipLabel.style.left = anchor.x;
            tooltipLabel.style.top = anchor.y - TooltipGap;
        }

        private void HideTooltip()
        {
            tooltipTimer?.Pause();
            tooltipTimer = null;
            hoveredIndex = NoSelection;

            tooltipLabel.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (inputHandler == null)
                return;

            inputHandler.NumberKeyPressed -= Select;
        }
    }
}
