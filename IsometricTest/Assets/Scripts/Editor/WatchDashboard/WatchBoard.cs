using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor.WatchDashboard
{
    /// <summary>
    /// Persistent storage for the Watch Dashboard. Each entry points at a property
    /// (target object + serialized property path) that was picked from an inspector
    /// context menu. Because it is an asset it survives recompiles, play-mode toggles
    /// and editor restarts, so the dashboard always shows the same watched values.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Debug/Watch Board", fileName = "WatchBoard")]
    public class WatchBoard : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public UnityEngine.Object Target;
            public string PropertyPath;
            public string Label;

            public bool IsValid => Target != null && !string.IsNullOrEmpty(PropertyPath);
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public bool Contains(UnityEngine.Object target, string propertyPath)
        {
            foreach (Entry e in entries)
            {
                if (e.Target == target && e.PropertyPath == propertyPath)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Add(UnityEngine.Object target, string propertyPath, string label)
        {
            if (target == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            if (Contains(target, propertyPath))
            {
                return false;
            }

            entries.Add(new Entry { Target = target, PropertyPath = propertyPath, Label = label });
            return true;
        }

        public void Remove(Entry entry) => entries.Remove(entry);

        public void Clear() => entries.Clear();

        public void RemoveInvalid() => entries.RemoveAll(e => !e.IsValid);

        // ---- Asset lookup / active-board tracking (editor tooling only) ----

        private const string ActiveBoardPrefKey = "WatchDashboard.ActiveBoardGuid";
        private const string DefaultFolder = "Assets/Watch Dashboard";
        private const string DefaultAssetName = "WatchBoard.asset";

        public static List<WatchBoard> FindAll()
        {
            var result = new List<WatchBoard>();
            foreach (string guid in AssetDatabase.FindAssets("t:WatchBoard"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WatchBoard board = AssetDatabase.LoadAssetAtPath<WatchBoard>(path);
                if (board != null)
                {
                    result.Add(board);
                }
            }

            return result;
        }

        /// <summary>The remembered board, or the first one found, or null if none exist yet.</summary>
        public static WatchBoard GetActive()
        {
            string guid = EditorPrefs.GetString(ActiveBoardPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    WatchBoard board = AssetDatabase.LoadAssetAtPath<WatchBoard>(path);
                    if (board != null)
                    {
                        return board;
                    }
                }
            }

            List<WatchBoard> all = FindAll();
            return all.Count > 0 ? all[0] : null;
        }

        public static WatchBoard GetActiveOrCreate()
        {
            WatchBoard board = GetActive();
            if (board != null)
            {
                SetActive(board);
                return board;
            }

            return CreateBoard(DefaultFolder, DefaultAssetName);
        }

        public static void SetActive(WatchBoard board)
        {
            if (board == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(board);
            EditorPrefs.SetString(ActiveBoardPrefKey, AssetDatabase.AssetPathToGUID(path));
        }

        public static WatchBoard CreateBoard(string folder, string assetName)
        {
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}");
            WatchBoard board = CreateInstance<WatchBoard>();
            AssetDatabase.CreateAsset(board, path);
            AssetDatabase.SaveAssets();
            SetActive(board);
            return board;
        }

        public static WatchBoard CreateBoardInteractive()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Watch Board", "WatchBoard", "asset",
                "Choose where to save the new Watch Board");

            if (string.IsNullOrEmpty(path))
            {
                return GetActiveOrCreate();
            }

            WatchBoard board = CreateInstance<WatchBoard>();
            AssetDatabase.CreateAsset(board, path);
            AssetDatabase.SaveAssets();
            SetActive(board);
            return board;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent))
            {
                parent = "Assets";
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
