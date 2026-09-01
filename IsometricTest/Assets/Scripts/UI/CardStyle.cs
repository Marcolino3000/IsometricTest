using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// What a panel built in code looks like - the colours and the border every one of them uses.
    /// Here rather than on any one of them because there are now three: the find popup, the tooltip
    /// card everything hovered is labelled with, and the badges over a unit's head. They are drawn on different
    /// panels and appear for different reasons, but a player reading two of them at once should not
    /// see two different interfaces.
    /// </summary>
    public static class CardStyle
    {
        /// <summary>Opaque: a card is read, and the map moving underneath it is only in the way.</summary>
        public static readonly Color Background = new(0.07f, 0.07f, 0.086f);

        /// <summary>The same, let through a little - for panels that sit over the board itself.</summary>
        public static readonly Color OverlayBackground = new(0.07f, 0.07f, 0.086f, 0.85f);

        public static readonly Color Border = new(1f, 1f, 1f, 0.35f);
        public static readonly Color Text = new(0.92f, 0.92f, 0.92f);
        public static readonly Color MutedText = new(1f, 1f, 1f, 0.6f);
        public static readonly Color StatText = new(0.91f, 0.89f, 0.34f);

        public static void SetBorder(VisualElement element, float width, float radius)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;

            element.style.borderTopColor = Border;
            element.style.borderRightColor = Border;
            element.style.borderBottomColor = Border;
            element.style.borderLeftColor = Border;

            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        public static void SetPadding(VisualElement element, float vertical, float horizontal)
        {
            element.style.paddingTop = vertical;
            element.style.paddingBottom = vertical;
            element.style.paddingLeft = horizontal;
            element.style.paddingRight = horizontal;
        }
    }
}
