using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Runtime.Gameplay.Global
{
    /// <summary>Which of the two roads into the game the cursor is currently on.</summary>
    public enum HoverSource
    {
        /// <summary>Nothing under the cursor.</summary>
        None,

        /// <summary>A tile or a unit, picked out of the world by <see cref="Raycaster"/>.</summary>
        World,

        /// <summary>A UI Toolkit element - an item bar slot, a picker entry, a merge slot.</summary>
        Ui
    }

    /// <summary>
    /// The one owner of what the cursor is over. There are two roads into the game - the physics
    /// raycast for the board and UI Toolkit's own picking for the bar - and neither knows about the
    /// other, so before this both wrote to the same preview and the last one to run in a frame won.
    /// The symptom was <c>ItemManager</c> re-asserting its preview in <c>LateUpdate</c>, because the
    /// world's <c>SelectionNoHover</c> cleared it in the very frame the bar set it.
    ///
    /// A single piece of state with one owner rather than an event bus: the sources report here, and
    /// whoever draws something for a hover asks here what the cursor is on. The UI wins when both
    /// claim it, which is what the screen shows anyway - the bar is drawn in front of the board.
    ///
    /// It holds <b>what</b> is hovered, not merely whether: <see cref="Tooltip"/> is what
    /// <c>TooltipView</c> draws, and it is asked of the world source rather than stored, so a card
    /// follows the health it is showing while it is up.
    /// </summary>
    public class HoverTarget : MonoBehaviour
    {
        /// <summary>Raised when the cursor comes to rest on something else - or on nothing.</summary>
        public event Action Changed;

        /// <summary>Which road owns the cursor. <see cref="HoverSource.Ui"/> takes precedence.</summary>
        public HoverSource Source => UiHasCursor ? HoverSource.Ui
            : WorldAlive ? HoverSource.World
            : HoverSource.None;

        /// <summary>The item bar slot under the cursor, or -1 when the cursor is not on one.</summary>
        public int UiSlot => uiSlot;

        /// <summary>
        /// Whether the UI is holding the cursor. What a world-side reaction asks before clearing
        /// something the UI has just put up. True for anything labelled, not only a bar slot: a
        /// merge column stands over the board as surely as the bar does.
        /// </summary>
        public bool UiHasCursor => uiSlot >= 0 || !uiTooltip.IsEmpty;

        /// <summary>
        /// What to say about whatever the cursor is on. Asked of the world source every time rather
        /// than captured when the hover began, so the numbers on a card are the ones on the board.
        /// </summary>
        public TooltipContent Tooltip
        {
            get
            {
                if (!uiTooltip.IsEmpty)
                    return uiTooltip;

                return WorldAlive ? world.Describe() : TooltipContent.Empty;
            }
        }

        /// <summary>Where that tooltip is put. A world anchor is re-read for the same reason.</summary>
        public TooltipAnchor Anchor
        {
            get
            {
                if (!uiTooltip.IsEmpty)
                    return uiAnchor;

                return WorldAlive ? TooltipAnchor.World(world.TooltipPoint) : default;
            }
        }

        private int uiSlot = -1;
        private ITooltipSource world;

        private TooltipContent uiTooltip = TooltipContent.Empty;
        private TooltipAnchor uiAnchor;

        /// <summary>
        /// Whether the world source is still there. A plain null check is not enough: the reference
        /// is held as an interface, so C# compares it rather than Unity, and a destroyed object
        /// would still answer.
        /// </summary>
        private bool WorldAlive => world is Object o ? o != null : world != null;

        /// <summary>Said by the item bar's hover: a slot index, or -1 on leaving one.</summary>
        public void SetUiSlot(int slot)
        {
            if (uiSlot == slot)
                return;

            uiSlot = slot;
            Changed?.Invoke();
        }

        /// <summary>
        /// Said by any UI element that labels itself - a bar slot, a picker entry, a merge slot, the
        /// merge button. <see cref="TooltipContent.Empty"/> takes the label away again. Kept apart
        /// from <see cref="SetUiSlot"/> because not everything labelled is a slot of the bar, and
        /// what the bar's slot indices mean is the item manager's business alone.
        /// </summary>
        public void SetUiTooltip(TooltipContent content, TooltipAnchor anchor)
        {
            // The anchor is compared as well as the words: two slots can hold the same item - a pair
            // of identical draughts - and moving between them says the same thing somewhere else.
            bool same = TooltipContent.Same(uiTooltip, content) && SameAnchor(uiAnchor, anchor);

            uiTooltip = content;
            uiAnchor = anchor;

            if (!same)
                Changed?.Invoke();
        }

        private static bool SameAnchor(TooltipAnchor a, TooltipAnchor b)
        {
            return a.Space == b.Space && a.Side == b.Side
                   && a.PanelRect == b.PanelRect && a.WorldPoint == b.WorldPoint;
        }

        /// <summary>Said by the world selection: the tile or unit under the cursor, or null.</summary>
        public void SetWorld(ITooltipSource source)
        {
            if (ReferenceEquals(world, source))
                return;

            world = source;
            Changed?.Invoke();
        }
    }
}
