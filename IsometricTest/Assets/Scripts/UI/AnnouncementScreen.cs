using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The big line the game says over the board - which zone was just entered today, and whatever
    /// else is worth stating without stopping the match. It fades in, holds and fades out by itself:
    /// nothing is asked of the player, so nothing waits for an answer.
    ///
    /// Built in code on a <see cref="UIDocument"/> of its own like the find popup and the game-over
    /// card, and click-through in the same way the game-over card is: every element ignores picking
    /// and it is no <c>IInputBlocker</c>, so it takes nothing away from the board underneath. That is
    /// the whole difference from the find card, which is read and dismissed - this is glanced at
    /// while playing on.
    ///
    /// A pure view: it is handed two finished strings, and what happened is the caller's to know.
    /// The lines themselves are a <see cref="Banner"/>, shared with the merge screen's result - and
    /// this is the screen that waves them: the banner rides its letters only for whoever asks it
    /// to, frame by frame, so a merge result states itself while news off the map moves.
    ///
    /// A second announcement while one is still up replaces it, as a second find replaces the card
    /// showing the first: the newer news is the one worth reading.
    /// </summary>
    public class AnnouncementScreen : MonoBehaviour
    {
        private const float FadeInDuration = 0.35f;
        private const float HoldDuration = 2.2f;
        private const float FadeOutDuration = 0.9f;

        private const float HeadlineSize = 64f;
        private const float DetailSize = 22f;

        /// <summary>How far down the screen the lines hang - clear of the top HUD, well above the
        /// character, so the announcement never covers what it is being said about.</summary>
        private const float TopInsetPercent = 14f;

        private VisualElement root;
        private Banner banner;

        private bool showing;
        private float age;

        /// <summary>
        /// Creates the screen on a document of its own. <paramref name="panelSettings"/> is the
        /// HUD's, so it scales with the rest of the interface.
        /// </summary>
        public static AnnouncementScreen Create(PanelSettings panelSettings, float sortingOrder)
        {
            var host = new GameObject(nameof(AnnouncementScreen));

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var screen = host.AddComponent<AnnouncementScreen>();
            screen.Build(document);

            return screen;
        }

        /// <summary>
        /// Says <paramref name="headline"/> over the board, with <paramref name="detail"/> under it.
        /// Nothing at all for an empty headline, so a caller need not check what it was authored
        /// with.
        /// </summary>
        public void Show(string headline, string detail)
        {
            Show(headline, detail, Banner.Accent);
        }

        /// <summary>The same in a colour of the caller's choosing - see <see cref="Banner.Warning"/>.</summary>
        public void Show(string headline, string detail, Color accent)
        {
            if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(detail))
                return;

            banner.Set(headline, detail, accent);

            showing = true;
            age = 0f;

            root.style.display = DisplayStyle.Flex;
            root.style.opacity = 0f;
        }

        /// <summary>Takes an announcement off the screen at once - what a restart does.</summary>
        public void Hide()
        {
            showing = false;
            root.style.display = DisplayStyle.None;
        }

        private void Build(UIDocument document)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.FlexStart;
            root.style.paddingTop = new Length(TopInsetPercent, LengthUnit.Percent);
            root.style.display = DisplayStyle.None;

            banner = Banner.Create(root, HeadlineSize, DetailSize);
        }

        private void Update()
        {
            if (!showing)
                return;

            age += Time.deltaTime;

            // Every frame it is up, fading in or out included: the wave is what the line is doing,
            // not part of its arrival.
            banner.Wave(age);

            if (age < FadeInDuration)
            {
                root.style.opacity = age / FadeInDuration;
                return;
            }

            var held = age - FadeInDuration - HoldDuration;

            if (held <= 0f)
            {
                root.style.opacity = 1f;
                return;
            }

            if (held >= FadeOutDuration)
            {
                Hide();
                return;
            }

            root.style.opacity = 1f - held / FadeOutDuration;
        }
    }
}
