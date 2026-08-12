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

        public void SetBlobAmount(int amount)
        {
            currentElements = amount;
            for (int i = 0; i < maxElements; i++)
            {
                container.ElementAt(i).style.visibility = i < amount ? Visibility.Visible : Visibility.Hidden;
            }
        }
    
        public void Setup(int maxBlobs)
        {
            container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");

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
                VisualElement blop = blopTemplate.Instantiate().Q("blob");
                container.Add(blop);
            }
        }
    }
}
