using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The find card as a column, everything centered: the name and what it is on top, the symbol
    /// under them and larger, then the description and the numbers. The other way of arranging the
    /// same card - see <see cref="ItemPopup"/>, which is the wide one and holds everything else a
    /// card does: when it appears, what puts it away, and what goes in it.
    ///
    /// A find is a moment rather than a line of a list, and this layout gives the thing found the
    /// middle of the card; the wide one reads faster. Which one is used is the Initiator's to say.
    /// </summary>
    public class VerticalItemPopup : ItemPopup
    {
        /// <summary>Narrower than the wide card: a column reads badly across a long line.</summary>
        protected override float CardWidth => 380f;

        protected override void BuildCard(VisualElement card)
        {
            card.style.alignItems = Align.Center;
            card.style.paddingTop = 20f;
            card.style.paddingBottom = 20f;

            Title = TextLabel(card, 26f, Text);
            Title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Title.style.unityTextAlign = TextAnchor.MiddleCenter;

            Kind = TextLabel(card, 15f, MutedText);
            Kind.style.marginTop = 2f;
            Kind.style.unityTextAlign = TextAnchor.MiddleCenter;

            BuildIcon(card, 144f);
            Icon.style.marginTop = 16f;

            Description = TextLabel(card, 16f, Text);
            Description.style.marginTop = 16f;
            Description.style.unityTextAlign = TextAnchor.MiddleCenter;

            Stats = Element(card);
            Stats.style.marginTop = 12f;
            Stats.style.alignItems = Align.Center;
        }

        /// <summary>
        /// Centered like the rest. The lines are laid out one under another either way, so only how
        /// they sit in the column differs.
        /// </summary>
        protected override void StatLine(string text)
        {
            var label = TextLabel(Stats, 16f, StatText);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.text = text;
        }
    }
}
