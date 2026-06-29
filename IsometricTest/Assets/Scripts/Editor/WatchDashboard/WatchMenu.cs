using UnityEditor;
using UnityEngine;

namespace Editor.WatchDashboard
{
    /// <summary>
    /// Entry points for sending a property to the Watch Dashboard.
    ///
    /// This project uses Odin Inspector, which draws all user MonoBehaviours and
    /// ScriptableObjects and suppresses Unity's per-property / header context menus
    /// (only built-in Unity types like Transform keep Unity's inspector). So the
    /// reliable hooks are Unity's own Hierarchy ("GameObject/…") and Project
    /// ("Assets/…") right-click menus, which Odin does not intercept. Each opens a
    /// picker listing the object's serialized properties.
    ///
    /// The per-property right-click is also kept; it works in non-Odin (Unity/IMGUI)
    /// inspectors such as Transform.
    /// </summary>
    [InitializeOnLoad]
    public static class WatchMenu
    {
        static WatchMenu()
        {
            EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        // 1) Per-property right-click — fires only in Unity-drawn (IMGUI) inspectors.
        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            Object target = property.serializedObject.targetObject;
            if (target == null)
            {
                return;
            }

            // The SerializedProperty is reused/invalidated after this callback returns,
            // so capture plain values now and let the menu items close over those.
            string path = property.propertyPath;
            string label = $"{target.name} · {property.displayName}";

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Watch in Dashboard"), false, () => Route(target, path, label));
        }

        // 2) Hierarchy right-click → pick a component + property (works regardless of Odin).
        private static double lastGameObjectInvoke;

        [MenuItem("GameObject/Watch in Dashboard…", false, 30)]
        private static void WatchGameObject(MenuCommand command)
        {
            // GameObject menu items fire once per selected object; only act once per click.
            if (EditorApplication.timeSinceStartup - lastGameObjectInvoke < 0.2)
            {
                return;
            }

            lastGameObjectInvoke = EditorApplication.timeSinceStartup;

            GameObject go = command.context as GameObject ?? Selection.activeGameObject;
            if (go == null)
            {
                return;
            }

            var menu = new GenericMenu();
            int total = 0;
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue; // missing script
                }

                total += AddPropertyItems(menu, component.GetType().Name, component);
            }

            if (total == 0)
            {
                menu.AddDisabledItem(new GUIContent("No serialized properties"));
            }

            menu.ShowAsContext();
        }

        // 3) Project right-click → pick a property of the selected asset (blueprints, traits, settings).
        [MenuItem("Assets/Watch in Dashboard…", false, 30)]
        private static void WatchAsset()
        {
            Object asset = Selection.activeObject;
            if (asset == null)
            {
                return;
            }

            var menu = new GenericMenu();
            int total = AddPropertyItems(menu, string.Empty, asset);
            if (total == 0)
            {
                menu.AddDisabledItem(new GUIContent("No serialized properties"));
            }

            menu.ShowAsContext();
        }

        [MenuItem("Assets/Watch in Dashboard…", true)]
        private static bool WatchAssetValidate()
        {
            return Selection.activeObject is ScriptableObject;
        }

        // ---- Shared ----

        // Adds a menu item per serialized property of <paramref name="target"/>, nested under
        // an optional <paramref name="prefix"/> submenu. Descends into serializable sub-objects
        // (so a unit's Current State exposes Health, Action Points, …) up to a small depth.
        private static int AddPropertyItems(GenericMenu menu, string prefix, Object target)
        {
            var serializedObject = new SerializedObject(target);
            const int maxDepth = 2;
            int added = 0;

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                int depth = Depth(iterator.propertyPath);
                bool isContainer = iterator.propertyType == SerializedPropertyType.Generic && !iterator.isArray;
                bool willDescend = isContainer && depth < maxDepth;
                enterChildren = willDescend;

                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                string path = iterator.propertyPath;
                string leaf = path.Replace(".Array.data[", "[").Replace('.', '/');
                string menuBase = string.IsNullOrEmpty(prefix) ? leaf : $"{prefix}/{leaf}";

                // A container we descend into becomes a submenu, so give the "watch the whole
                // block" option a distinct leaf name to avoid colliding with that submenu.
                string menuPath = willDescend ? $"{menuBase}/‹whole block›" : menuBase;

                string watchLabel = $"{target.name} · {iterator.displayName}";
                Object capturedTarget = target;
                string capturedPath = path;
                menu.AddItem(new GUIContent(menuPath), false, () => Route(capturedTarget, capturedPath, watchLabel));
                added++;
            }

            return added;
        }

        // A project asset can't store a reference to a scene/runtime object, so scene
        // objects are watched in the dashboard window's session list instead of a board.
        private static void Route(Object target, string path, string label)
        {
            if (EditorUtility.IsPersistent(target))
            {
                AddToBoard(WatchBoard.GetActiveOrCreate(), target, path, label);
            }
            else
            {
                WatchBoardWindow.AddSceneWatch(target, path, label);
            }
        }

        private static void AddToBoard(WatchBoard board, Object target, string path, string label)
        {
            if (board == null)
            {
                return;
            }

            Undo.RecordObject(board, "Watch Property");
            if (board.Add(target, path, label))
            {
                EditorUtility.SetDirty(board);
                AssetDatabase.SaveAssetIfDirty(board);
            }

            WatchBoard.SetActive(board);
            WatchBoardWindow.ShowAndFocus(board);
        }

        private static int Depth(string propertyPath)
        {
            int count = 0;
            foreach (char c in propertyPath)
            {
                if (c == '.')
                {
                    count++;
                }
            }

            return count;
        }
    }
}
