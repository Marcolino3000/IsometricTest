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

                activeBlobs.Add(activeBlob);
                previewInactiveBlobs.Add(previewInactiveBlob);
                inactiveBlobs.Add(inactiveBlob);

                container.Add(activeBlob);
                container.Add(previewInactiveBlob);
                container.Add(inactiveBlob);
            }
        }

        /// <summary>
        /// Shows, from left to right, <paramref name="activeAmount"/> active blobs followed by
        /// <paramref name="previewAmount"/> preview-inactive blobs; every remaining blob is shown as
        /// inactive. Used points are therefore replaced starting from the right side.
        /// </summary>
        public void SetBlobAmount(int activeAmount, int previewAmount = 0)
        {
            activeAmount = Mathf.Clamp(activeAmount, 0, maxElements);
            previewAmount = Mathf.Clamp(previewAmount, 0, maxElements - activeAmount);

            for (int i = 0; i < maxElements; i++)
            {
                bool isActive = i < activeAmount;
                bool isPreview = !isActive && i < activeAmount + previewAmount;

                Show(activeBlobs[i], isActive);
                Show(previewInactiveBlobs[i], isPreview);
                Show(inactiveBlobs[i], !isActive && !isPreview);
            }
        }

        private static void Show(VisualElement blob, bool visible)
        {
            blob.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
