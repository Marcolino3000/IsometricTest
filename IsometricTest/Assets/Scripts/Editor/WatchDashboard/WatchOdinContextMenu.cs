using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor.WatchDashboard
{
    /// <summary>
    /// Adds "Watch in Dashboard" to the right-click context menu of every property in
    /// Odin-drawn inspectors.
    ///
    /// This project's Odin draws all user MonoBehaviours/ScriptableObjects with its own
    /// property system, which bypasses Unity's <c>EditorApplication.contextualPropertyMenu</c>.
    /// Odin instead builds a property's context menu from the drawers in its chain that
    /// implement <see cref="IDefinesGenericMenuItems"/>. This is a generic value drawer, so
    /// Odin applies it to every property; it draws nothing itself (delegates via
    /// <c>CallNextDrawer</c>) and only contributes the menu item.
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    public class WatchOdinContextMenuDrawer<T> : OdinValueDrawer<T>, IDefinesGenericMenuItems
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            // Transparent wrapper — let the real drawer(s) render the value.
            CallNextDrawer(label);
        }

        public void PopulateGenericMenu(InspectorProperty property, GenericMenu genericMenu)
        {
            SerializedObject serializedObject = property.Tree.UnitySerializedObject;
            string unityPath = property.UnityPropertyPath;

            // Only Unity-serialized properties can be mirrored via SerializedProperty bindings;
            // Odin-only / non-serialized members have no Unity path and are skipped.
            if (serializedObject == null || string.IsNullOrEmpty(unityPath))
            {
                return;
            }

            UnityEngine.Object target = serializedObject.targetObject;
            if (target == null)
            {
                return;
            }

            string label = $"{target.name} · {property.NiceName}";
            genericMenu.AddItem(new GUIContent("Watch in Dashboard"), false,
                () => WatchMenu.Route(target, unityPath, label));
        }
    }
}
