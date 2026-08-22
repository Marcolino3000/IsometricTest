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

        public void Setup(int maxBlobs)
        {
            container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");

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
                var activeBlob = blopTemplate.Instantiate().Q("active");
                var previewInactiveBlob = blopTemplate.Instantiate().Q("previewInactive");
                var inactiveBlob = blopTemplate.Instantiate().Q("inactive");

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
        /// A previewed point is drawn as the icon standing for the action about to spend it, in the
        /// order the plan spends them, so a walk up to a target reads as steps followed by the strike.
        /// The bar is told sprites rather than actions on purpose - like the item bar, it draws what
        /// it is handed and knows nothing about what the picture means. An entry left null keeps the
        /// plain faded blob, which is what an action nobody drew an icon for falls back to.
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

        private static void Show(VisualElement blob, bool visible)
        {
            blob.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
