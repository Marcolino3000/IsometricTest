using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DebugContextMenu
{
    static DebugContextMenu()
    {
        Debug.Log("Registering contextualPropertyMenu");
        EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
    }

    static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
    {
        Debug.Log(property.propertyPath);

        menu.AddItem(
            new GUIContent("Add to Debug View"),
            false,
            () => Debug.Log(property.propertyPath));
    }
}