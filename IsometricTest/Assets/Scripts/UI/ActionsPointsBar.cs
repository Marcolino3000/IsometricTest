using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ActionsPointsBar : MonoBehaviour
    {
        [SerializeField] private int maxElements;
        [SerializeField] private VisualTreeAsset blopTemplate;

        private VisualElement container;

        // What makes a blob when the row is not this object's own - see SetupIn. Null leaves the
        // authored template, which is what a unit's own bar over its head is drawn with.
        private Func<string, VisualElement> blobFactory;

        private readonly List<VisualElement> activeBlobs = new();
        private readonly List<VisualElement> previewInactiveBlobs = new();
        private readonly List<VisualElement> inactiveBlobs = new();

        /// <summary>
        /// The faded look a previewed point falls back to when the caller names no icon for it.
        /// Read off the template rather than written here, so it stays authored in the UXML.
        /// </summary>
        private Color previewColor = new(0.75f, 0.68f, 0.47f);

        /// <summary>
        /// The radius that rounds a preview blob into a disc, captured from the template for the same
        /// reason the colour is. Dropped while an icon is shown - the background is clipped to it, so
        /// a square icon left in a circle would lose its corners.
        /// </summary>
        private StyleLength previewRadius = new(500f);

        /// <summary>
        /// Draws the row on this object's own world-space panel, over the unit's head.
        /// </summary>
        public void Setup(int maxBlobs)
        {
            container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");

            Build(maxBlobs);

            SetBlobAmount(maxElements);
        }

        /// <summary>
        /// Draws the row into <paramref name="row"/> instead, with <paramref name="blobs"/> making
        /// each of the three blobs a point is drawn with in place of the template — what the player's
        /// character does, since its points are shown in the HUD over the item slots rather than over
        /// its head (see <see cref="PlayerVitals"/>). Everything else stays here: which blob is lit,
        /// which is previewed and what icon it wears are the same call wherever the row hangs.
        /// </summary>
        public void SetupIn(VisualElement row, Func<string, VisualElement> blobs, int maxBlobs)
        {
            // Mounted elsewhere, so the panel this component sits on is taken down rather than left
            // hanging an empty frame over the unit. The component itself goes on answering — it is
            // called directly and never ticks.
            gameObject.SetActive(false);

            container = row;
            blobFactory = blobs;

            Build(maxBlobs);

            SetBlobAmount(maxElements);
        }

        /// <summary>
        /// Rebuilds the row for a new maximum. The blobs are built once at setup and
        /// <see cref="SetBlobAmount"/> clamps to how many there are, so an item that raises the unit's
        /// action points for good has to ask for another one - or the point it grants every turn would
        /// be spendable but never shown. How many are *lit* is left to the caller, which knows what is
        /// left of the turn.
        /// </summary>
        public void SetMaxBlobs(int maxBlobs)
        {
            if (container == null || maxBlobs == maxElements)
                return;

            Build(maxBlobs);
        }

        private void Build(int maxBlobs)
        {
            maxElements = maxBlobs;

            activeBlobs.Clear();
            previewInactiveBlobs.Clear();
            inactiveBlobs.Clear();
            container.Clear();

            for (int i = 0; i < maxElements; i++)
            {
                var activeBlob = Blob("active");
                var previewInactiveBlob = Blob("previewInactive");
                var inactiveBlob = Blob("inactive");

                if (i == 0)
                    CaptureDiscLook(previewInactiveBlob);

                activeBlobs.Add(activeBlob);
                previewInactiveBlobs.Add(previewInactiveBlob);
                inactiveBlobs.Add(inactiveBlob);

                container.Add(activeBlob);
                container.Add(previewInactiveBlob);
                container.Add(inactiveBlob);
            }
        }

        /// <summary>
        /// Shows, from left to right, <paramref name="activeAmount"/> active blobs followed by one
        /// preview blob per entry in <paramref name="previewIcons"/>; every remaining blob is shown as
        /// inactive. Used points are therefore replaced starting from the right side.
        ///
        /// A previewed point is drawn as the icon standing for the action about to spend it. The list
        /// is drawn left to right like everything else here; which end of it is spent first is the
        /// caller's business, since the bar is told sprites rather than actions on purpose - like the
        /// item bar, it draws what it is handed and knows nothing about what the picture means. An
        /// entry left null keeps the plain faded blob, which is what an action nobody drew an icon
        /// for falls back to.
        /// </summary>
        public void SetBlobAmount(int activeAmount, IReadOnlyList<Sprite> previewIcons = null)
        {
            activeAmount = Mathf.Clamp(activeAmount, 0, maxElements);

            int previewAmount = Mathf.Clamp(previewIcons?.Count ?? 0, 0, maxElements - activeAmount);

            for (int i = 0; i < maxElements; i++)
            {
                bool isActive = i < activeAmount;
                bool isPreview = !isActive && i < activeAmount + previewAmount;

                Show(activeBlobs[i], isActive);
                Show(previewInactiveBlobs[i], isPreview);
                Show(inactiveBlobs[i], !isActive && !isPreview);

                SetPreviewIcon(previewInactiveBlobs[i], isPreview ? previewIcons[i - activeAmount] : null);
            }
        }

        /// <summary>
        /// Draws a previewed point as its action's icon, or as the faded disc when there is none.
        /// The disc is dropped while an icon is shown: the icon *is* the blob, so nothing is left
        /// behind it to tint it.
        /// </summary>
        private void SetPreviewIcon(VisualElement blob, Sprite icon)
        {
            blob.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);

            blob.style.backgroundColor = icon != null ? Color.clear : previewColor;

            var radius = icon != null ? new StyleLength(0f) : previewRadius;

            blob.style.borderTopLeftRadius = radius;
            blob.style.borderTopRightRadius = radius;
            blob.style.borderBottomRightRadius = radius;
            blob.style.borderBottomLeftRadius = radius;

            // A blob is only square by accident of how the row divides up, so the cell is fitted
            // inside it rather than stretched to it.
            blob.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            blob.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            blob.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            blob.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        /// <summary>
        /// Remembers what a preview blob looks like while it is a disc, so the faded look stays
        /// authored in the UXML instead of being written twice. Anything the template does not state
        /// keeps the fallback, which is the same look spelled out.
        /// </summary>
        private void CaptureDiscLook(VisualElement blob)
        {
            if (blob.style.backgroundColor.keyword == StyleKeyword.Undefined)
                previewColor = blob.style.backgroundColor.value;

            if (blob.style.borderTopLeftRadius.keyword == StyleKeyword.Undefined)
                previewRadius = blob.style.borderTopLeftRadius;
        }

        /// <summary>One blob, from whichever of the two the row was set up with.</summary>
        private VisualElement Blob(string name) =>
            blobFactory != null ? blobFactory(name) : blopTemplate.Instantiate().Q(name);

        private static void Show(VisualElement blob, bool visible)
        {
            blob.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
