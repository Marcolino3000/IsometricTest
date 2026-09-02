using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// A big line with a smaller one under it - how the game says something happened rather than
    /// what something is. The merge screen states the result of a gamble in it and the map states
    /// which zone was just entered; both are a headline and a sentence, and there will be more of
    /// them, so the look lives here rather than being built twice.
    ///
    /// A piece of a view rather than a view: it is a pair of lines put into whatever element the
    /// caller hands over, and who shows it, over what and for how long stays theirs. That is the
    /// whole split - the merge banner sits over a dimmed panel until the next merge, the zone
    /// announcement fades by itself over the board.
    ///
    /// <b>The headline is built one letter at a time</b>, words kept whole so a long line still
    /// wraps between them. That is what lets <see cref="Wave"/> lift each letter on its own; a
    /// <see cref="Label"/> is drawn as one block and could only be moved as one. A banner nobody
    /// waves is simply a still row of letters, which is what the merge screen shows.
    ///
    /// It picks nothing: an announcement is read, not pressed, and one over the board must not
    /// swallow a click meant for the tile behind it.
    /// </summary>
    public class Banner
    {
        /// <summary>The gold a good outcome is stated in - the game-over card's, so a result reads
        /// the same wherever the game announces one.</summary>
        public static readonly Color Accent = new(0.98f, 0.82f, 0.29f);

        /// <summary>The red the other kind is stated in.</summary>
        public static readonly Color Warning = new(0.89f, 0.31f, 0.27f);

        /// <summary>
        /// How far a letter rides, as a share of its own size, so the wave keeps its proportions
        /// whatever the headline is set in.
        /// </summary>
        private const float WaveHeight = 0.14f;

        /// <summary>How fast the wave runs, in radians a second.</summary>
        private const float WaveSpeed = 4.5f;

        /// <summary>
        /// How far one letter lags behind the one before it, in radians. Enough that a word is
        /// visibly a wave rather than a line bobbing as one, and not so much that neighbours fly
        /// apart.
        /// </summary>
        private const float WaveLag = 0.45f;

        /// <summary>The gap a space stands for, as a share of the letter size.</summary>
        private const float SpaceWidth = 0.28f;

        /// <summary>The element holding the two lines, so the caller can place or hide the pair.</summary>
        public VisualElement Root { get; private set; }

        private VisualElement headlineRow;
        private Label detail;

        // The headline's letters in reading order, spaces left out: what a wave lifts, and the
        // order is what carries it along the line.
        private readonly List<VisualElement> letters = new();

        private float headlineSize;

        /// <summary>
        /// Adds a banner to <paramref name="parent"/>. <paramref name="reservedHeight"/> above zero
        /// keeps the block that tall whether or not it says anything and hangs the lines from its
        /// bottom edge - what a panel underneath it needs, so a result appearing does not shove the
        /// panel down the screen. At zero the block is as tall as its text.
        /// </summary>
        public static Banner Create(VisualElement parent, float headlineSize = 46f, float detailSize = 17f,
            float reservedHeight = 0f)
        {
            var banner = new Banner { headlineSize = headlineSize };

            var block = new VisualElement { pickingMode = PickingMode.Ignore };
            block.style.alignItems = Align.Center;

            if (reservedHeight > 0f)
            {
                block.style.height = reservedHeight;
                block.style.justifyContent = Justify.FlexEnd;
            }

            banner.Root = block;

            // A row of words rather than a line of text: it wraps between them, and every letter
            // inside is its own element for the wave to lift.
            banner.headlineRow = new VisualElement { pickingMode = PickingMode.Ignore };
            banner.headlineRow.style.flexDirection = FlexDirection.Row;
            banner.headlineRow.style.flexWrap = Wrap.Wrap;
            banner.headlineRow.style.justifyContent = Justify.Center;
            banner.headlineRow.style.alignItems = Align.Center;
            block.Add(banner.headlineRow);

            banner.detail = Text(block, detailSize, CardStyle.Text);
            banner.detail.style.marginTop = 2f;

            parent?.Add(block);

            return banner;
        }

        /// <summary>
        /// What the two lines say. Both are finished strings - a banner knows no more about what
        /// happened than the colour it is told to state it in - and an empty one takes its line out
        /// of the layout rather than leaving a gap where it would have stood.
        /// </summary>
        public void Set(string headline, string detail, Color accent)
        {
            BuildHeadline(headline, accent);

            SetText(this.detail, detail);
        }

        /// <summary>
        /// Rides the headline's letters up and down, each a little behind the one before it, so the
        /// line reads as a wave running through it. <paramref name="time"/> is seconds - whatever
        /// the caller is already counting, since it only ever moves forward.
        ///
        /// Asked for rather than automatic: a banner is a piece of a view and has no frame of its
        /// own, so whoever owns the screen decides whether its news moves. Nothing here changes the
        /// layout - a letter is translated, not repositioned - so the wave costs no reflow.
        /// </summary>
        public void Wave(float time)
        {
            for (var i = 0; i < letters.Count; i++)
            {
                var offset = Mathf.Sin(time * WaveSpeed - i * WaveLag) * headlineSize * WaveHeight;

                letters[i].style.translate = new Translate(0f, offset);
            }
        }

        /// <summary>Says nothing, leaving whatever height the block reserves.</summary>
        public void Clear()
        {
            Set(null, null, Accent);
        }

        /// <summary>
        /// Lays the headline out as words of letters. Rebuilt on every line rather than reused: a
        /// headline is set once and read, so there is nothing to gain from keeping the elements of
        /// the one before it around.
        /// </summary>
        private void BuildHeadline(string text, Color accent)
        {
            headlineRow.Clear();
            letters.Clear();

            headlineRow.style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            if (string.IsNullOrWhiteSpace(text))
                return;

            VisualElement word = null;

            foreach (var character in text)
            {
                // A space is a gap between words, not a letter: it ends the word being built, so a
                // line too long for the screen wraps there and never inside a word.
                if (char.IsWhiteSpace(character))
                {
                    word = null;

                    var gap = new VisualElement { pickingMode = PickingMode.Ignore };
                    gap.style.width = headlineSize * SpaceWidth;
                    headlineRow.Add(gap);

                    continue;
                }

                if (word == null)
                {
                    word = new VisualElement { pickingMode = PickingMode.Ignore };
                    word.style.flexDirection = FlexDirection.Row;
                    word.style.flexShrink = 0f;
                    headlineRow.Add(word);
                }

                var letter = Text(word, headlineSize, accent);
                letter.text = character.ToString();
                letter.style.unityFontStyleAndWeight = FontStyle.Bold;

                letters.Add(letter);
            }
        }

        /// <summary>
        /// A line of the banner, or one letter of it. Outlined, since one of the two places it is
        /// drawn is the board itself, where a pale tile would otherwise swallow it.
        /// </summary>
        private static Label Text(VisualElement parent, float fontSize, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityTextOutlineWidth = Mathf.Max(1f, fontSize * 0.04f);
            label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = 0f;

            parent.Add(label);

            return label;
        }

        /// <summary>Fills a label, or takes it out of the layout when there is nothing to say.</summary>
        private static void SetText(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
