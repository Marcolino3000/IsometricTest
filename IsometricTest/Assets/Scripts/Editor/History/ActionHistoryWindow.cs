using Runtime.Gameplay.History;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.History
{
    /// <summary>
    /// Every action taken since the match started, in order. Clicking one restores the state the game
    /// was in when that action was taken, undoing or redoing everything in between - the same jump the
    /// Ctrl/Cmd+Z and Ctrl/Cmd+Y shortcuts make one step at a time.
    ///
    /// The window owns no state: it mirrors the running <see cref="ActionHistory"/> and polls its
    /// version, which also gets it through domain reloads and play mode changes without re-wiring.
    /// </summary>
    public class ActionHistoryWindow : EditorWindow
    {
        private static readonly Color CurrentRowColor = new(0.23f, 0.47f, 0.85f, 0.45f);
        private static readonly Color TurnRowColor = new(1f, 1f, 1f, 0.07f);

        private ScrollView list;
        private Label status;
        private Button undoButton;
        private Button redoButton;
        private Button latestButton;
        private Button resumeAiButton;

        // What the list currently shows, so polling can skip rebuilding when nothing changed.
        private ActionHistory shownHistory;
        private int shownVersion = -1;

        private ActionHistory history;

        /// <summary>
        /// The history of the running match, looked up the way the other editor tools here find scene
        /// objects. Cached: the reference only goes stale on play mode changes and domain reloads,
        /// where it reads as null and is looked up again.
        /// </summary>
        private ActionHistory History
        {
            get
            {
                if (history == null && EditorApplication.isPlaying)
                    history = FindFirstObjectByType<ActionHistory>();

                return history;
            }
        }

        [MenuItem("Tools/Action History")]
        public static void Open()
        {
            var window = GetWindow<ActionHistoryWindow>();
            window.titleContent = new GUIContent("Action History");
            window.minSize = new Vector2(260, 180);
        }

        public void CreateGUI()
        {
            BuildChrome();
            Rebuild();

            rootVisualElement.schedule.Execute(Sync).Every(200);
        }

        // The runtime shortcuts only fire while the Game view has focus, which it is not while you are
        // clicking around in here. Same keys, bound to this window, and rebindable under Edit > Shortcuts.
        [Shortcut("Action History/Undo", typeof(ActionHistoryWindow), KeyCode.PageUp)]
        private static void UndoShortcut(ShortcutArguments args)
        {
            (args.context as ActionHistoryWindow)?.Run(history => history.Undo());
        }

        [Shortcut("Action History/Redo", typeof(ActionHistoryWindow), KeyCode.PageDown)]
        private static void RedoShortcut(ShortcutArguments args)
        {
            (args.context as ActionHistoryWindow)?.Run(history => history.Redo());
        }

        private void BuildChrome()
        {
            var root = rootVisualElement;
            root.Clear();

            var toolbar = new Toolbar();

            undoButton = new ToolbarButton(() => Run(history => history.Undo()))
                { text = "Undo", tooltip = "Step one action back (Page Up)" };
            redoButton = new ToolbarButton(() => Run(history => history.Redo()))
                { text = "Redo", tooltip = "Step one action forward (Page Down)" };
            latestButton = new ToolbarButton(() => Run(history => history.GoToLatest()))
                { text = "Latest", tooltip = "Jump back to the most recent state" };
            resumeAiButton = new ToolbarButton(() => Run(history => history.ResumeAi()))
                { text = "Resume AI", tooltip = "Let the AI play out the current turn - restoring a snapshot cancels a turn in progress" };

            toolbar.Add(undoButton);
            toolbar.Add(redoButton);
            toolbar.Add(latestButton);
            toolbar.Add(resumeAiButton);
            root.Add(toolbar);

            status = new Label();
            status.style.marginLeft = 6;
            status.style.marginTop = 4;
            status.style.marginBottom = 2;
            status.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(status);

            var hint = new Label("Click an action to return to the state it was taken in. \">\" marks where the game is. " +
                                 "Page Up / Page Down step, here or in the Game view.");
            hint.style.marginLeft = 6;
            hint.style.marginBottom = 4;
            hint.style.opacity = 0.6f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            root.Add(hint);

            list = new ScrollView();
            list.style.flexGrow = 1;
            root.Add(list);
        }

        private void Run(System.Action<ActionHistory> command)
        {
            var history = History;
            if (history == null)
                return;

            command(history);
            Rebuild();
        }

        /// <summary>Rebuilds only when the history, its contents or the position in it changed.</summary>
        private void Sync()
        {
            var current = History;

            // ReferenceEquals, not ==: leaving play mode destroys the history, and Unity's == would
            // report the destroyed object as equal to null and leave a stale list on screen.
            var version = current != null ? current.Version : -1;

            if (ReferenceEquals(current, shownHistory) && version == shownVersion)
                return;

            Rebuild();
        }

        private void Rebuild()
        {
            if (list == null)
                return;

            list.Clear();

            var history = History;
            shownHistory = history;
            shownVersion = history != null ? history.Version : -1;

            undoButton.SetEnabled(history != null && history.CanUndo);
            redoButton.SetEnabled(history != null && history.CanRedo);
            latestButton.SetEnabled(history != null && history.CanRedo);
            resumeAiButton.SetEnabled(history != null);

            if (history == null)
            {
                status.text = "Not playing";
                list.Add(new Label("Enter play mode to record actions.") { style = { marginLeft = 6, marginTop = 6 } });
                return;
            }

            var entries = history.Entries;
            status.text = $"{entries.Count} action{(entries.Count == 1 ? "" : "s")}  -  at {history.Cursor}/{entries.Count}";

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                list.Add(BuildRow(history, i, (i + 1).ToString(), entry.Label, entry.Detail,
                    entry.Kind == ActionKind.TurnChange));
            }

            // Last stop: everything applied. Clicking any action above rewinds to just before it, so
            // without this row the newest state would only be reachable through the Latest button.
            list.Add(BuildRow(history, entries.Count, string.Empty, "Latest state", string.Empty, emphasize: true));
        }

        /// <summary>
        /// One row of the list. <paramref name="targetCursor"/> is the position it restores - for an
        /// action row, the moment just before that action was taken.
        /// </summary>
        private VisualElement BuildRow(ActionHistory history, int targetCursor, string number, string label,
            string detail, bool emphasize)
        {
            // Where the game currently sits: everything above has happened, this row and everything
            // below has not (yet).
            var isCurrent = history.Cursor == targetCursor;
            var isUndone = targetCursor > history.Cursor;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            if (isCurrent)
                row.style.backgroundColor = CurrentRowColor;
            else if (emphasize)
                row.style.backgroundColor = TurnRowColor;

            if (isUndone)
                row.style.opacity = 0.45f;

            var marker = new Label(isCurrent ? ">" : string.Empty);
            marker.style.width = 14;
            marker.style.flexShrink = 0;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(marker);

            var numberLabel = new Label(number);
            numberLabel.style.width = 26;
            numberLabel.style.flexShrink = 0;
            numberLabel.style.opacity = 0.6f;
            row.Add(numberLabel);

            var text = new Label(label);
            text.style.flexGrow = 1;
            text.style.overflow = Overflow.Hidden;
            if (emphasize)
                text.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(text);

            if (!string.IsNullOrEmpty(detail))
            {
                var detailLabel = new Label(detail);
                detailLabel.style.opacity = 0.6f;
                detailLabel.style.flexShrink = 0;
                detailLabel.style.marginLeft = 8;
                row.Add(detailLabel);
            }

            row.tooltip = string.IsNullOrEmpty(number)
                ? "Jump back to the most recent state"
                : $"Go to the state the game was in when \"{label}\" was taken";

            row.RegisterCallback<MouseDownEvent>(_ => Run(target => target.GoTo(targetCursor)));
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = isCurrent ? CurrentRowColor :
                    emphasize ? TurnRowColor : Color.clear);

            return row;
        }
    }
}
