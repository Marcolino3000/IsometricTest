using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private int maxElements;
        [SerializeField] private int currentElements;
        [SerializeField] private VisualTreeAsset blopTemplate;
    
        private VisualElement container;

        // What makes a blob when the row is not this object's own - see SetupIn. Null leaves the
        // authored template, which is what a unit's own bar over its head is drawn with.
        private Func<string, VisualElement> blobFactory;

        public void SetBlobAmount(int amount)
        {
            currentElements = amount;
            for (int i = 0; i < maxElements; i++)
            {
                container.ElementAt(i).style.visibility = i < amount ? Visibility.Visible : Visibility.Hidden;
            }
        }
    
        /// <summary>
        /// Draws the row on this object's own world-space panel, over the unit's head.
        /// </summary>
        public void Setup(int maxBlobs)
        {
            container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");

            Build(maxBlobs);
        }

        /// <summary>
        /// Draws the row into <paramref name="row"/> instead, with <paramref name="blobs"/> making
        /// each blob in place of the template — what the player's character does, since its numbers
        /// are shown in the HUD over the item slots rather than over its head (see
        /// <see cref="PlayerVitals"/>). Which blob is lit stays here either way: the two rows differ
        /// in where they are mounted and how big a blob is drawn, and in nothing else.
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
        }

        /// <summary>
        /// Rebuilds the row for a new maximum. The blobs are built once at setup, so an item that
        /// raises the unit's maximum health has to ask for more of them - and an undo of it for fewer.
        /// How many are *shown* is not set here: the caller knows the health, and this is called
        /// while it is being restored.
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
            container.Clear();

            for (int i = 0; i < maxElements; i++)
            {
                VisualElement blop = Blob("blob");
                container.Add(blop);
            }
        }

        /// <summary>One blob, from whichever of the two the row was set up with.</summary>
        private VisualElement Blob(string name) =>
            blobFactory != null ? blobFactory(name) : blopTemplate.Instantiate().Q(name);
    }
}
