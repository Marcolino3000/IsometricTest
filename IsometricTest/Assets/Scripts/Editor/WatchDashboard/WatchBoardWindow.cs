using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.WatchDashboard
{
    /// <summary>
    /// The central overview. Lists every watched property as a live, two-way bound
    /// field: edits here change the original value, and external changes (inspector
    /// edits, runtime) show up here in real time via UI Toolkit's serialized-property
    /// binding.
    ///
    /// Watches come from two stores. Asset properties live in the persistent
    /// <see cref="WatchBoard"/> asset. Scene / play-mode objects cannot be referenced
    /// from a project asset, so they are kept in this window's own serialized list,
    /// which survives domain reloads for the life of the editor session (the object
    /// itself still vanishes when play mode ends).
    /// </summary>
    public class WatchBoardWindow : EditorWindow
    {
        [SerializeField] private WatchBoard board;
        [SerializeField] private List<WatchBoard.Entry> sceneEntries = new();

        private ObjectField boardField;
        private ScrollView list;
        private Label countLabel;

        // One SerializedObject per target, reused across that target's entries.
        private readonly Dictionary<Object, SerializedObject> serializedCache = new();

        [MenuItem("Tools/Watch Dashboard")]
        public static void Open()
        {
            WatchBoardWindow wnd = GetWindow<WatchBoardWindow>();
            wnd.titleContent = new GUIContent("Watch Dashboard");
            wnd.minSize = new Vector2(280, 180);
        }

        public static void ShowAndFocus(WatchBoard targetBoard)
        {
            WatchBoardWindow wnd = GetWindow<WatchBoardWindow>();
            wnd.titleContent = new GUIContent("Watch Dashboard");
            if (targetBoard != null)
            {
                wnd.board = targetBoard;
            }

            wnd.Rebuild();
            wnd.Focus();
        }

        public static void AddSceneWatch(Object target, string path, string label)
        {
            WatchBoardWindow wnd = GetWindow<WatchBoardWindow>();
            wnd.titleContent = new GUIContent("Watch Dashboard");
            wnd.AddSceneEntry(target, path, label);
            wnd.Focus();
        }

        private void OnEnable()
        {
            if (board == null)
            {
                board = WatchBoard.GetActive();
            }

            Undo.undoRedoPerformed += Rebuild;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Rebuild;
        }

        public void CreateGUI()
        {
            BuildChrome();
            Rebuild();
        }

        private void AddSceneEntry(Object target, string path, string label)
        {
            if (target == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            foreach (WatchBoard.Entry e in sceneEntries)
            {
                if (e.Target == target && e.PropertyPath == path)
                {
                    Rebuild();
                    return;
                }
            }

            sceneEntries.Add(new WatchBoard.Entry { Target = target, PropertyPath = path, Label = label });
            Rebuild();
        }

        private void BuildChrome()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            var toolbar = new Toolbar();

            boardField = new ObjectField
            {
                objectType = typeof(WatchBoard),
                allowSceneObjects = false,
                value = board
            };
            boardField.style.flexGrow = 1;
            boardField.RegisterValueChangedCallback(evt =>
            {
                board = evt.newValue as WatchBoard;
                if (board != null)
                {
                    WatchBoard.SetActive(board);
                }

                Rebuild();
            });
            toolbar.Add(boardField);

            toolbar.Add(new ToolbarButton(Rebuild) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(RemoveInvalid) { text = "Clean", tooltip = "Remove entries whose object or property no longer exists" });
            toolbar.Add(new ToolbarButton(ClearAll) { text = "Clear" });
            root.Add(toolbar);

            countLabel = new Label();
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel.style.marginLeft = 6;
            countLabel.style.marginTop = 4;
            countLabel.style.marginBottom = 2;
            root.Add(countLabel);

            list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            root.Add(list);
        }

        private void Rebuild()
        {
            if (list == null)
            {
                return; // CreateGUI hasn't run yet; it will call Rebuild itself.
            }

            list.Clear();
            serializedCache.Clear();
            boardField?.SetValueWithoutNotify(board);

            int assetCount = board != null ? board.Entries.Count : 0;
            int sceneCount = sceneEntries.Count;
            countLabel.text = $"{assetCount + sceneCount} watched" + (sceneCount > 0 ? $"  ·  {sceneCount} live" : string.Empty);

            if (assetCount == 0 && sceneCount == 0)
            {
                list.Add(Help("Nothing watched yet.\nRight-click any property in an inspector and choose “Watch in Dashboard”.\n\nAsset properties are saved to a Watch Board; scene / play-mode objects are watched for this session."));
                if (board == null)
                {
                    var create = new Button(() =>
                    {
                        board = WatchBoard.GetActiveOrCreate();
                        Rebuild();
                    }) { text = "Create Watch Board" };
                    create.style.marginLeft = 10;
                    create.style.marginTop = 4;
                    create.style.maxWidth = 160;
                    list.Add(create);
                }

                return;
            }

            if (assetCount > 0)
            {
                list.Add(SectionHeader($"Assets — {board.name}"));
                foreach (WatchBoard.Entry entry in board.Entries)
                {
                    WatchBoard.Entry captured = entry;
                    list.Add(BuildRow(captured, () => RemoveAssetEntry(captured)));
                }
            }

            if (sceneCount > 0)
            {
                list.Add(SectionHeader("Scene / play mode — this session"));
                foreach (WatchBoard.Entry entry in sceneEntries)
                {
                    WatchBoard.Entry captured = entry;
                    list.Add(BuildRow(captured, () => RemoveSceneEntry(captured)));
                }
            }
        }

        private VisualElement BuildRow(WatchBoard.Entry entry, System.Action onRemove)
        {
            var card = new VisualElement();
            card.style.marginLeft = card.style.marginRight = 6;
            card.style.marginTop = card.style.marginBottom = 2;
            card.style.paddingTop = card.style.paddingBottom = 4;
            card.style.paddingLeft = card.style.paddingRight = 6;
            card.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            if (entry.Target == null)
            {
                Label missing = Warning($"⚠ Missing object  ({entry.Label})");
                missing.style.flexGrow = 1;
                header.Add(missing);
                header.Add(RemoveButton(onRemove));
                card.Add(header);
                return card;
            }

            var nameBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(entry.Target);
                Selection.activeObject = entry.Target;
            }) { text = entry.Target.name, tooltip = "Select / ping source object" };
            nameBtn.style.flexGrow = 1;
            nameBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
            nameBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameBtn.style.backgroundColor = Color.clear;
            nameBtn.style.borderTopWidth = nameBtn.style.borderBottomWidth = 0;
            nameBtn.style.borderLeftWidth = nameBtn.style.borderRightWidth = 0;
            nameBtn.style.paddingLeft = 0;
            header.Add(nameBtn);
            header.Add(RemoveButton(onRemove));
            card.Add(header);

            SerializedObject so = GetSerializedObject(entry.Target);
            SerializedProperty sp = so.FindProperty(entry.PropertyPath);
            if (sp == null)
            {
                card.Add(Warning($"property “{entry.PropertyPath}” not found"));
                return card;
            }

            var field = new PropertyField(sp, sp.displayName);
            field.BindProperty(sp);
            field.style.marginTop = 2;
            card.Add(field);

            return card;
        }

        private Button RemoveButton(System.Action onRemove)
        {
            var btn = new Button(onRemove) { text = "✕", tooltip = "Stop watching" };
            btn.style.width = 22;
            return btn;
        }

        private void RemoveAssetEntry(WatchBoard.Entry entry)
        {
            if (board == null)
            {
                return;
            }

            Undo.RecordObject(board, "Remove Watch");
            board.Remove(entry);
            Save();
            Rebuild();
        }

        private void RemoveSceneEntry(WatchBoard.Entry entry)
        {
            sceneEntries.Remove(entry);
            Rebuild();
        }

        private void RemoveInvalid()
        {
            if (board != null)
            {
                Undo.RecordObject(board, "Remove Invalid Watches");
                board.RemoveInvalid();
                Save();
            }

            sceneEntries.RemoveAll(e => !e.IsValid);
            Rebuild();
        }

        private void ClearAll()
        {
            int total = (board != null ? board.Entries.Count : 0) + sceneEntries.Count;
            if (total == 0)
            {
                return;
            }

            bool ok = EditorUtility.DisplayDialog("Clear Watch Dashboard",
                "Remove all watched properties (asset board + scene watches)?", "Clear", "Cancel");
            if (!ok)
            {
                return;
            }

            if (board != null)
            {
                Undo.RecordObject(board, "Clear Watches");
                board.Clear();
                Save();
            }

            sceneEntries.Clear();
            Rebuild();
        }

        private SerializedObject GetSerializedObject(Object target)
        {
            if (!serializedCache.TryGetValue(target, out SerializedObject so) || so == null)
            {
                so = new SerializedObject(target);
                serializedCache[target] = so;
            }

            return so;
        }

        private void Save()
        {
            if (board == null)
            {
                return;
            }

            EditorUtility.SetDirty(board);
            AssetDatabase.SaveAssetIfDirty(board);
        }

        private static Label SectionHeader(string text)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.fontSize = 11;
            l.style.color = new Color(0.6f, 0.7f, 0.85f);
            l.style.marginLeft = 6;
            l.style.marginTop = 8;
            l.style.marginBottom = 2;
            return l;
        }

        private static Label Help(string text)
        {
            var l = new Label(text) { style = { whiteSpace = WhiteSpace.Normal } };
            l.style.marginLeft = l.style.marginRight = 10;
            l.style.marginTop = 10;
            l.style.color = new Color(0.7f, 0.7f, 0.7f);
            return l;
        }

        private static Label Warning(string text)
        {
            var l = new Label(text) { style = { whiteSpace = WhiteSpace.Normal } };
            l.style.color = new Color(1f, 0.6f, 0.3f);
            return l;
        }
    }
}
