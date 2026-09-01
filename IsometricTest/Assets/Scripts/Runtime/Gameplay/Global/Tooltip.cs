using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.Gameplay.Global
{
    /// <summary>Which way a tooltip is placed from the thing it describes.</summary>
    public enum TooltipSide
    {
        Above,
        Left,
        Right
    }

    /// <summary>Which coordinates an anchor is given in - see <see cref="TooltipAnchor"/>.</summary>
    public enum TooltipSpace
    {
        /// <summary>A rectangle on the UI panel, i.e. the bounds of an element.</summary>
        Panel,

        /// <summary>A point on the board, converted through the camera when it is drawn.</summary>
        World
    }

    /// <summary>
    /// What a tooltip says, in the form the view draws it - a value pushed at a view that is told
    /// nothing about what produced it, the way <c>ItemOption</c>, <c>SlotLook</c> and
    /// <see cref="Capability"/> are. One shape for every kind of thing: an item slot, an empty slot,
    /// a unit, a tile, a lootbox.
    ///
    /// <see cref="Entries"/> are the label-and-detail rows under the numbers, and they are
    /// <see cref="Capability"/> because that struct already is the (symbol, label, one line) triple
    /// the badges and the unit card draw; a second struct with the same three fields would only need
    /// converting between.
    /// </summary>
    public readonly struct TooltipContent
    {
        /// <summary>Symbol drawn beside the title, or null.</summary>
        public readonly Sprite Icon;

        /// <summary>What the thing is called. Nothing is shown without one.</summary>
        public readonly string Title;

        /// <summary>What kind of thing it is - "Melee Weapon", "Lootbox", "Player".</summary>
        public readonly string Kind;

        public readonly string Description;

        /// <summary>What it amounts to in numbers, one short line each.</summary>
        public readonly IReadOnlyList<string> Stats;

        /// <summary>Named rows under the numbers - a unit's capabilities, a tile's traits.</summary>
        public readonly IReadOnlyList<Capability> Entries;

        /// <summary>Nothing to say, which is what turns a tooltip off.</summary>
        public static readonly TooltipContent Empty = default;

        public TooltipContent(string title, string kind = null, string description = null,
            IReadOnlyList<string> stats = null, IReadOnlyList<Capability> entries = null, Sprite icon = null)
        {
            Title = title;
            Kind = kind;
            Description = description;
            Stats = stats;
            Entries = entries;
            Icon = icon;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Title);

        /// <summary>
        /// This tooltip folded into one row of another - what a tile says about the box lying on it.
        /// The first number is taken as the detail, since that is the one worth reading in passing.
        /// </summary>
        public Capability AsEntry()
        {
            string detail = Stats != null && Stats.Count > 0 ? Stats[0] : Kind;

            return new Capability(Icon, Title, detail);
        }

        /// <summary>
        /// Whether two describe the same thing in the same words. Asked while a tooltip is up: the
        /// numbers on it are read off the live board, so it is rebuilt only once they have moved.
        /// </summary>
        public static bool Same(TooltipContent a, TooltipContent b)
        {
            if (a.Icon != b.Icon || a.Title != b.Title || a.Kind != b.Kind || a.Description != b.Description)
                return false;

            return SameLines(a.Stats, b.Stats) && SameEntries(a.Entries, b.Entries);
        }

        private static bool SameLines(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;

            if (countA != countB)
                return false;

            for (int i = 0; i < countA; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool SameEntries(IReadOnlyList<Capability> a, IReadOnlyList<Capability> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;

            if (countA != countB)
                return false;

            for (int i = 0; i < countA; i++)
            {
                if (a[i].Label != b[i].Label || a[i].Detail != b[i].Detail || a[i].Icon != b[i].Icon)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Where a tooltip is put and which way round. Whoever reports a hover knows the geometry, so
    /// the side is carried here rather than decided again by every view - which is what the item bar
    /// and the merge screen each used to do with a private enum of their own.
    ///
    /// A world anchor is a point rather than a rectangle and is converted through the camera when it
    /// is drawn, so the card follows a walking unit and a panning camera without being re-reported.
    /// </summary>
    public readonly struct TooltipAnchor
    {
        public readonly TooltipSpace Space;

        /// <summary>Bounds of the element, in panel coordinates. <see cref="TooltipSpace.Panel"/> only.</summary>
        public readonly Rect PanelRect;

        /// <summary>The point on the board the card hangs over. <see cref="TooltipSpace.World"/> only.</summary>
        public readonly Vector3 WorldPoint;

        public readonly TooltipSide Side;

        private TooltipAnchor(TooltipSpace space, Rect panelRect, Vector3 worldPoint, TooltipSide side)
        {
            Space = space;
            PanelRect = panelRect;
            WorldPoint = worldPoint;
            Side = side;
        }

        /// <summary>The bounds of a UI element, measured now - elements labelled this way don't move.</summary>
        public static TooltipAnchor Element(VisualElement element, TooltipSide side = TooltipSide.Above)
        {
            return element == null
                ? default
                : new TooltipAnchor(TooltipSpace.Panel, element.worldBound, Vector3.zero, side);
        }

        public static TooltipAnchor World(Vector3 point, TooltipSide side = TooltipSide.Above)
        {
            return new TooltipAnchor(TooltipSpace.World, Rect.zero, point, side);
        }
    }

    /// <summary>
    /// Something that can say what it is. Implemented by the things themselves - a unit, a tile, a
    /// lootbox - so nothing takes one apart from outside, exactly as an item builds its own
    /// <c>Stats</c>. A new kind of world object is tooltipped by implementing this and nothing else.
    ///
    /// UI elements have no object to implement it: the bar and the merge screen are pure views, so
    /// the owner of what they show pushes a <see cref="TooltipContent"/> at them instead.
    /// </summary>
    public interface ITooltipSource
    {
        /// <summary>
        /// What to say about this right now, or <see cref="TooltipContent.Empty"/> for nothing.
        /// Asked while the tooltip is up, so it must answer from the live state and may not assume
        /// the thing is still on the board.
        /// </summary>
        TooltipContent Describe();

        /// <summary>Where on the board the card hangs - the top of the thing being described.</summary>
        Vector3 TooltipPoint { get; }
    }
}
