using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ActionsPointsBar : MonoBehaviour
    {
        [SerializeField] private int maxElements;
        [SerializeField] private VisualTreeAsset blopTemplate;
    
        private VisualElement container;

        public void SetBlobAmount(int amount)
        {
            for (int i = 0; i < maxElements; i++)
            {
                container.ElementAt(i).ToggleInClassList("inactiveActionPointBlob");
            }
        }
    
        public void Setup(int maxBlobs)
        {
            maxElements = maxBlobs;
            container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");
        
            for (int i = 0; i < maxElements; i++)
            {
                VisualElement blop = blopTemplate.Instantiate().Q("blob");
                container.Add(blop);
            }
        }
    }
}