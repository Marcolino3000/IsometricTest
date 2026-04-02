// using Runtime;
// using UnityEditor;
// using UnityEngine;
//
// // [CustomEditor(typeof(Object), true)]
// //jshdhf
// public class MyDataEditor : Editor
// {
//     [System.Serializable]
//     public sealed class ContextPropertyReference
//     {
//         
//         
//         [SerializeField] private Object targetObject;
//         [SerializeField] private string propertyPath;
//
//         public Object TargetObject => targetObject;
//         public string PropertyPath => propertyPath;
//         public bool IsValid => targetObject != null && !string.IsNullOrEmpty(propertyPath);
//
//         public ContextPropertyReference(Object targetObject, string propertyPath)
//         {
//             this.targetObject = targetObject;
//             this.propertyPath = propertyPath;
//         }
//
//         public SerializedProperty ResolveProperty(out SerializedObject serializedObject)
//         {
//             serializedObject = null;
//
//             if (!IsValid)
//             {
//                 return null;
//             }
//
//             serializedObject = new SerializedObject(targetObject);
//             return serializedObject.FindProperty(propertyPath);
//         }
//     }
//
//     public static ContextPropertyReference LastClickedPropertyReference { get; private set; }
//
//     private void OnEnable()
//     {
//         EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
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
//         DrawDefaultInspector();
//
//         // serializedObject.ApplyModifiedProperties();
//     }
//
//     private void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
//     {
//         // if (property.serializedObject.targetObject != target)
//         //     return;
//
//         ContextPropertyReference clickedReference = GetClickedPropertyReference(property);
//
//         if (clickedReference != null)
//         {
//             menu.AddSeparator("");
//             menu.AddItem(
//                 new GUIContent("Mirror/Capture Property Reference"),
//                 false,
//                 () =>
//                 {
//                     LastClickedPropertyReference = clickedReference;
//                     Debug.Log(
//                         $"Captured property reference: {clickedReference.TargetObject.name}.{clickedReference.PropertyPath}",
//                         clickedReference.TargetObject);
//                 });
//         }
//         
//
//         menu.AddItem(
//             new GUIContent("Add to Debug"),
//             false,
//             () =>
//             {
//                 Debug.Log($"Right-clicked property: {property.propertyPath}", target);
//             });
//     }
//
//     public static ContextPropertyReference GetClickedPropertyReference(SerializedProperty property)
//     {
//         if (property == null || property.serializedObject == null)
//         {
//             return null;
//         }
//
//         Object propertyOwner = property.serializedObject.targetObject;
//         if (propertyOwner == null || string.IsNullOrEmpty(property.propertyPath))
//         {
//             return null;
//         }
//
//         return new ContextPropertyReference(propertyOwner, property.propertyPath);
//     }
// }
