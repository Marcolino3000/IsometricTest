using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Object), true)]
public class MyDataEditor : Editor
{
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
        if (property.serializedObject.targetObject != target)
            return;

        // Optional: only add the menu item for a specific field slkdfj
        // if (property.propertyPath == nameof(MyData.amount))
        // {
        //     menu.AddSeparator("");
        //     menu.AddItem(
        //         new GUIContent("Set Amount To 100"),
        //         false,
        //         () =>
        //         {
        //             property.serializedObject.Update();
        //
        //             SerializedProperty p =
        //                 property.serializedObject.FindProperty(property.propertyPath);
        //
        //             p.intValue = 100;
        //             property.serializedObject.ApplyModifiedProperties();
        //
        //             EditorUtility.SetDirty(target);
        //         });
        // }

        // Example for all fields
        menu.AddItem(
            new GUIContent("Log Property Path"),
            false,
            () =>
            {
                Debug.Log($"Right-clicked property: {property.propertyPath}", target);
            });
    }
}