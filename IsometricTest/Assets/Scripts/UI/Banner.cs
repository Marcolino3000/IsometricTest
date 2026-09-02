using System.Collections.Generic;
using Runtime.Gameplay.Global;
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
    /// <b>The word a line ends on gets a wave of its own</b>, which is what the two
    /// <see cref="WaveSettings"/> handed to <see cref="Wave"/> are for: the sentence carries the
    /// news and the last word is the news, so it moves faster and starts its wave over at its own
    /// first letter.
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

        /// <summary>The gap a space stands for, as a share of the letter size.</summary>
        private const float SpaceWidth = 0.28f;

        /// <summary>The element holding the two lines, so the caller can place or hide the pair.</summary>
        public VisualElement Root { get; private set; }

        private VisualElement headlineRow;
        private Label detail;

        // The headline's letters in reading order, spaces left out: what a wave lifts, and the
        // order is what carries it along the line.
        private readonly List<VisualElement> letters = new();

        // Where the last word starts in that list. The word a line ends on is the thing being
        // said - the horde is *enraged* - so it is given a wave of its own rather than being the
        // tail of the sentence's.
        private int lastWordStart;

        private float headlineSize;

        // The face both lines are set in, or null for the interface's own. Handed in rather than
        // decided here: what a banner is set in belongs to whoever is saying something - the
        // announcement is stated in a display face, the merge result stays in the interface's.
        private Font font;

        /// <summary>
        /// Adds a banner to <paramref name="parent"/>. <paramref name="reservedHeight"/> above zero
        /// keeps the block that tall whether or not it says anything and hangs the lines from its
        /// bottom edge - what a panel underneath it needs, so a result appearing does not shove the
        /// panel down the screen. At zero the block is as tall as its text.
        /// <paramref name="font"/> is the face both lines are set in; unset leaves them in the
        /// interface's own.
        /// </summary>
        public static Banner Create(VisualElement parent, float headlineSize = 46f, float detailSize = 17f,
            float reservedHeight = 0f, Font font = null)
        {
            var banner = new Banner { headlineSize = headlineSize, font = font };

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

            banner.detail = Text(block, detailSize, CardStyle.Text, font);
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
        /// The last word is given <paramref name="lastWord"/> instead of <paramref name="text"/>,
        /// and its wave starts over at its own first letter: that word is what the line is about,
        /// so it moves on its own terms rather than as the far end of the sentence's wave. Pass the
        /// same settings twice for one wave through the whole line.
        ///
        /// Asked for rather than automatic: a banner is a piece of a view and has no frame of its
        /// own, so whoever owns the screen decides whether its news moves - and the settings come
        /// in per call, so they can be tuned while it is on screen. Nothing here changes the layout
        /// - a letter is translated, not repositioned - so the wave costs no reflow.
        /// </summary>
        public void Wave(float time, WaveSettings text, WaveSettings lastWord)
        {
            for (var i = 0; i < letters.Count; i++)
            {
                var tail = i >= lastWordStart;
                var wave = (tail ? lastWord : text) ?? text;

                if (wave == null)
                    continue;

                // Counted from the word's own start, so the last word's wave reads as one running
                // through that word rather than as whatever phase the sentence had reached.
                var place = tail ? i - lastWordStart : i;
                var offset = Mathf.Sin(time * wave.Speed - place * wave.Lag) * headlineSize * wave.Height;

                letters[i].style.translate = new Translate(0f, offset);
            }
        }

        /// <summary>Says nothing, leaving whatever height the block reserves.</summary>
        public void Clear()
        {
            Set(null, null, Accent);
        }

        /// <summary>Puts every letter back on the line - what a banner that has stopped waving shows.</summary>
        public void Still()
        {
            foreach (var letter in letters)
                letter.style.translate = new Translate(0f, 0f);
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
            lastWordStart = 0;

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

                    // Every word claims it; the one that still holds it at the end is the last.
                    lastWordStart = letters.Count;
                }

                var letter = Text(word, headlineSize, accent, font);
                letter.text = character.ToString();
                letter.style.unityFontStyleAndWeight = FontStyle.Bold;

                letters.Add(letter);
            }
        }

        /// <summary>
        /// A line of the banner, or one letter of it. Outlined, since one of the two places it is
        /// drawn is the board itself, where a pale tile would otherwise swallow it.
        /// </summary>
        private static Label Text(VisualElement parent, float fontSize, Color color, Font font = null)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;

            // Unity's own null check - an unassigned asset reference is not the CLR's null. Written
            // as a FontDefinition rather than through unityFont: the two set the same style, and
            // this is the one the current text engine reads.
            if (font != null)
                label.style.unityFontDefinition = FontDefinition.FromFont(font);

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
