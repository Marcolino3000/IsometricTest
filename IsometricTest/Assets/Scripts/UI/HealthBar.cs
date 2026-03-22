using UnityEngine;
using UnityEngine.UIElements;

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
        maxElements = maxBlobs;
        container = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("container");
        
        for (int i = 0; i < maxElements; i++)
        {
            VisualElement blop = blopTemplate.Instantiate().Q("blob");
            container.Add(blop);
        }
    }
}
