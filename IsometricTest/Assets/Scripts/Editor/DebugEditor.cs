// using System.Linq;
// using Runtime.Debugger;
// using UnityEditor;
// using UnityEditor.UIElements;
// using UnityEngine;
//
// namespace Editor
// {
//     [CustomEditor(typeof(MonoBehaviour), true)]
//     public class DebugEditor : UnityEditor.Editor
//     {
//         [SerializeField] private DebugViewer viewer;
//         [SerializeField] private DebugViewWindow viewWindow;
//     private void OnEnable()
//     {
//         EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
//         Debug.Log("DebugEditor enabled");
//     }
//
//     private void OnDisable()
//     {
//         EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
//     }
//
//     public override void OnInspectorGUI()
//     {
//         // serializedObject.Update();
//
//         Debug.Log("DebugEditor: OnInspectorGUI");
//         DrawDefaultInspector();
//
//         // serializedObject.ApplyModifiedProperties();
//     }
//
//     private void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
//     {
//         Debug.Log("contextual property menu");
//         menu.AddItem(
//             new GUIContent("Add to Debug View"),
//             false,
//             () =>
//             {
//                 if (!EditorWindow.HasOpenInstances<DebugViewWindow>())
//                 {
//                     Debug.LogError("No instance of Debug View Window found.");
//                 }
//                 
//                 viewWindow = Resources.FindObjectsOfTypeAll<DebugViewWindow>().First();
//                 if (viewWindow == null)
//                 {
//                     Debug.LogError("Debug View Window is not open.");
//                     return;
//                 }
//                 
//                 PropertyField propertyField = new PropertyField(property);
//                 propertyField.Bind(property.serializedObject);
//                 
//                 viewWindow.AddElement(propertyField);
//             });
//     }
//     }
// }