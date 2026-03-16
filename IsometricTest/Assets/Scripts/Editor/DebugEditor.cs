using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using Object = System.Object;

namespace Runtime
{
    [CustomEditor(typeof(Object), true)]
    public class DebugEditor : Editor
    {
        [SerializeField] private DebugViewer viewer;
        [SerializeField] private DebugViewWindow viewWindow;
    private void OnEnable()
    {
        EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
    }

    private void OnDisable()
    {
        EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
    }

    public override void OnInspectorGUI()
    {
        // serializedObject.Update();
     
        DrawDefaultInspector();

        // serializedObject.ApplyModifiedProperties();
    }

    private void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
    {
        menu.AddItem(
            new GUIContent("Add to Debug View"),
            false,
            () =>
            {
                if (!EditorWindow.HasOpenInstances<DebugViewWindow>())
                {
                    Debug.LogError("No instance of Debug View Window found.");
                }
                
                viewWindow = Resources.FindObjectsOfTypeAll<DebugViewWindow>().First();
                if (viewWindow == null)
                {
                    Debug.LogError("Debug View Window is not open.");
                    return;
                }
                
                PropertyField propertyField = new PropertyField(property);
                propertyField.Bind(property.serializedObject);
                
                viewWindow.AddElement(propertyField);
            });
    }
    }
}