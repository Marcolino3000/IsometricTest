using System;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>Which of the two roads into the game the cursor is currently on.</summary>
    public enum HoverSource
    {
        /// <summary>Nothing under the cursor.</summary>
        None,

        /// <summary>A tile or a unit, picked out of the world by <see cref="Raycaster"/>.</summary>
        World,

        /// <summary>A UI Toolkit element - today an item bar slot.</summary>
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
    /// </summary>
    public class HoverTarget : MonoBehaviour
    {
        /// <summary>Raised when the cursor comes to rest on something else - or on nothing.</summary>
        public event Action Changed;

        /// <summary>Which road owns the cursor. <see cref="HoverSource.Ui"/> takes precedence.</summary>
        public HoverSource Source => uiSlot >= 0 ? HoverSource.Ui
            : worldHovered ? HoverSource.World
            : HoverSource.None;

        /// <summary>The item bar slot under the cursor, or -1 when the cursor is not on one.</summary>
        public int UiSlot => uiSlot;

        /// <summary>
        /// Whether the UI is holding the cursor. What a world-side reaction asks before clearing
        /// something the UI has just put up.
        /// </summary>
        public bool UiHasCursor => uiSlot >= 0;

        private int uiSlot = -1;
        private bool worldHovered;

        /// <summary>Said by the item bar's hover: a slot index, or -1 on leaving one.</summary>
        public void SetUiSlot(int slot)
        {
            if (uiSlot == slot)
                return;

            uiSlot = slot;
            Changed?.Invoke();
        }

        /// <summary>Said by the world selection: whether a tile or unit is under the cursor.</summary>
        public void SetWorldHover(bool hovered)
        {
            if (worldHovered == hovered)
                return;

            worldHovered = hovered;
            Changed?.Invoke();
        }
    }
}
