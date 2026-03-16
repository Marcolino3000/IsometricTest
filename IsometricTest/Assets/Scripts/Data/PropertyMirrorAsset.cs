// using UnityEngine;
//
// namespace Data
// {
//     [CreateAssetMenu(menuName = "Data/Debug/Property Mirror Asset")]
//     public class PropertyMirrorAsset : ScriptableObject
//     {
//         [SerializeField] private Object sourceObject;
//         [SerializeField] private string sourcePropertyPath;
//
//         public Object SourceObject => sourceObject;
//         public string SourcePropertyPath => sourcePropertyPath;
//     }
// }
//
// #if UNITY_EDITOR
// namespace Data
// {
//     [UnityEditor.CustomEditor(typeof(PropertyMirrorAsset))]
//     public class PropertyMirrorAssetEditor : UnityEditor.Editor
//     {
//         private UnityEditor.SerializedProperty sourceObjectProperty;
//         private UnityEditor.SerializedProperty sourcePropertyPathProperty;
//
//         private void OnEnable()
//         {
//             sourceObjectProperty = serializedObject.FindProperty("sourceObject");
//             sourcePropertyPathProperty = serializedObject.FindProperty("sourcePropertyPath");
//         }
//
//         public override void OnInspectorGUI()
//         {
//             serializedObject.Update();
//
//             UnityEditor.EditorGUILayout.HelpBox(
//                 "Right-click any property in an inspector and choose Mirror/Capture Property Reference, then click Use Last Clicked Property here.",
//                 UnityEditor.MessageType.Info);
//
//             if (UnityEngine.GUILayout.Button("Use Last Clicked Property"))
//             {
//                 MyDataEditor.ContextPropertyReference clicked = MyDataEditor.LastClickedPropertyReference;
//
//                 if (clicked != null && clicked.IsValid)
//                 {
//                     sourceObjectProperty.objectReferenceValue = clicked.TargetObject;
//                     sourcePropertyPathProperty.stringValue = clicked.PropertyPath;
//                 }
//                 else
//                 {
//                     UnityEngine.Debug.LogWarning("No captured property reference found. Capture one from a property context menu first.");
//                 }
//             }
//
//             UnityEditor.EditorGUI.BeginDisabledGroup(true);
//             UnityEditor.EditorGUILayout.PropertyField(sourceObjectProperty);
//             UnityEditor.EditorGUILayout.PropertyField(sourcePropertyPathProperty);
//             UnityEditor.EditorGUI.EndDisabledGroup();
//
//             UnityEngine.Object source = sourceObjectProperty.objectReferenceValue;
//             string path = sourcePropertyPathProperty.stringValue;
//
//             if (source == null || string.IsNullOrEmpty(path))
//             {
//                 UnityEditor.EditorGUILayout.HelpBox("No source property selected yet.", UnityEditor.MessageType.Warning);
//             }
//             else
//             {
//                 UnityEditor.SerializedObject sourceSerializedObject = new UnityEditor.SerializedObject(source);
//                 sourceSerializedObject.Update();
//                 UnityEditor.SerializedProperty mirroredProperty = sourceSerializedObject.FindProperty(path);
//
//                 if (mirroredProperty == null)
//                 {
//                     UnityEditor.EditorGUILayout.HelpBox(
//                         $"Property not found: {path}",
//                         UnityEditor.MessageType.Error);
//                 }
//                 else
//                 {
//                     UnityEditor.EditorGUILayout.Space();
//                     UnityEditor.EditorGUILayout.LabelField("Mirrored Source Property", UnityEditor.EditorStyles.boldLabel);
//
//                     UnityEditor.EditorGUI.BeginChangeCheck();
//                     UnityEditor.EditorGUILayout.PropertyField(mirroredProperty, true);
//                     if (UnityEditor.EditorGUI.EndChangeCheck())
//                     {
//                         sourceSerializedObject.ApplyModifiedProperties();
//                         UnityEditor.EditorUtility.SetDirty(source);
//                     }
//                 }
//             }
//
//             serializedObject.ApplyModifiedProperties();
//         }
//     }
// }
// #endif
