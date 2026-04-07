using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class OutlineManager : MonoBehaviour
    {
        [SerializeField] private Selector selector;

        private Unit lasSelectedUnit;

        private void Awake()
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(Selection selection)
        {
            if (selection.SelectedUnit == null)
                return;
                
            selection.SelectedUnit.Outline.Show();
            
            if(selection.SelectedUnit != lasSelectedUnit && lasSelectedUnit != null)
            {
                lasSelectedUnit.Outline.Hide();
                lasSelectedUnit = selection.SelectedUnit;
            }
        }
    }
}