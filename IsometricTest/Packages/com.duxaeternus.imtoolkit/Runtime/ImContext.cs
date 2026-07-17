using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    /// <summary>Widget kinds. Part of the reconciliation key, so two different kinds never reuse each other's elements.</summary>
    internal enum ImKind
    {
        Label,
        Header,
        Button,
        Toggle,
        Slider,
        SliderInt,
        TextField,
        FloatField,
        IntField,
        Dropdown,
        EnumPopup,
        ProgressBar,
        Image,
        Space,
        FlexibleSpace,
        Separator,
        Foldout,
        Row,
        Column,
        Box,
        Scroll,
        Indent,
    }

    /// <summary>One retained widget slot inside a container. Persists across frames as long as the same call keeps happening.</summary>
    internal sealed class ImNode
    {
        public long Key;
        public VisualElement Element;
        public ImContainer Children; // only for container widgets (Row/Box/Scroll/…)
        public object State;         // widget-specific state (click flags, value sync, …)
    }

    /// <summary>A VisualElement whose children are managed by the reconciler, plus the per-frame cursor.</summary>
    internal sealed class ImContainer
    {
        public VisualElement Element;
        public readonly List<ImNode> Nodes = new List<ImNode>();
        public int Cursor;
        public int FrameTouched = int.MinValue;
        public bool IsRow;
        public bool RootVisible = true; // only meaningful for root containers (main panel, window contents)
    }

    /// <summary>Sync state for value fields (Slider, Toggle, TextField, …).</summary>
    internal sealed class ImValueState<T>
    {
        public T LastPushed;
        public bool UserChanged;
        public T UserValue;
    }

    internal sealed class ImClickState
    {
        public bool Clicked;
    }

    /// <summary>
    /// The immediate-mode core: matches this frame's IM.* calls against the retained element tree,
    /// creating/reusing/removing elements so the tree always mirrors the calls.
    /// </summary>
    internal static class ImContext
    {
        /// <summary>Frame source, swappable so edit-mode tests can advance frames manually.</summary>
        internal static Func<int> FrameSource = () => Time.frameCount;

        static int s_Frame = int.MinValue;
        static readonly List<ImContainer> s_Stack = new List<ImContainer>();
        static readonly Dictionary<string, ImWindowRecord> s_Windows = new Dictionary<string, ImWindowRecord>();
        static ImContainer s_Main;
        static VisualElement s_Layer;
        static bool s_TestMode;
        static bool s_WarnedEditMode;

        internal static VisualElement Layer => s_Layer;
        internal static bool TestMode => s_TestMode;

        // ---- lifecycle -----------------------------------------------------

        /// <summary>Called by every IM.* entry point. Returns false when calls should no-op (edit mode).</summary>
        internal static bool Prepare()
        {
            if (!s_TestMode)
            {
                if (!Application.isPlaying)
                {
                    if (!s_WarnedEditMode)
                    {
                        s_WarnedEditMode = true;
                        Debug.LogWarning("[IM Toolkit] IM.* calls only render in play mode; call ignored.");
                    }
                    return false;
                }
                ImHost.Ensure();
            }

            if (s_Layer == null)
                return false;

            int frame = FrameSource();
            if (frame != s_Frame)
            {
                s_Frame = frame;
                s_Stack.Clear(); // also recovers from unbalanced scopes (exceptions in user code)
            }
            return true;
        }

        internal static void SetLayer(VisualElement layer) => s_Layer = layer;

        /// <summary>Runs once per frame after all Update/LateUpdate scripts: prunes root containers and hides anything not drawn this frame.</summary>
        internal static void EndFrame()
        {
            if (!s_TestMode && !Application.isPlaying)
                return;
            if (s_Layer == null)
                return;

            int frame = FrameSource();
            s_Stack.Clear();

            if (s_Main != null)
                FinalizeRoot(s_Main, s_Main.Element, frame);
            foreach (var kv in s_Windows)
                FinalizeRoot(kv.Value.Content, kv.Value.Root, frame);
        }

        static void FinalizeRoot(ImContainer container, VisualElement visual, int frame)
        {
            bool touched = container.FrameTouched == frame;
            if (touched != container.RootVisible)
            {
                container.RootVisible = touched;
                visual.style.display = touched ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (touched)
                Prune(container);
        }

        /// <summary>Destroys all retained UI (windows, positions, state). Everything is rebuilt on the next IM.* call.</summary>
        internal static void ClearAll()
        {
            foreach (var kv in s_Windows)
                kv.Value.Root.RemoveFromHierarchy();
            s_Windows.Clear();
            s_Main?.Element.RemoveFromHierarchy();
            s_Main = null;
            s_Stack.Clear();
        }

        internal static void ResetStatics()
        {
            s_Frame = int.MinValue;
            s_Stack.Clear();
            s_Windows.Clear();
            s_Main = null;
            s_Layer = null;
            s_TestMode = false;
            s_WarnedEditMode = false;
            FrameSource = () => Time.frameCount;
        }

        // ---- containers ----------------------------------------------------

        internal static ImContainer CurrentContainer
        {
            get
            {
                if (s_Stack.Count > 0)
                    return s_Stack[s_Stack.Count - 1];
                return MainPanel();
            }
        }

        static ImContainer MainPanel()
        {
            if (s_Main == null)
            {
                var panel = new VisualElement { name = "im-main" };
                panel.AddToClassList(ImStyles.Main);
                s_Layer.Add(panel);
                s_Main = new ImContainer { Element = panel };
            }
            Touch(s_Main);
            return s_Main;
        }

        internal static void Touch(ImContainer container)
        {
            if (container.FrameTouched != s_Frame)
            {
                container.FrameTouched = s_Frame;
                container.Cursor = 0;
            }
        }

        internal static void Push(ImContainer container) => s_Stack.Add(container);

        /// <summary>Pop a scoped container (Row/Box/Scroll/…): prune whatever this frame did not redraw, then pop.</summary>
        internal static void Pop()
        {
            if (s_Stack.Count == 0)
                return;
            Prune(s_Stack[s_Stack.Count - 1]);
            s_Stack.RemoveAt(s_Stack.Count - 1);
        }

        /// <summary>Pop a window scope. No prune here — the same window may be appended to again this frame; EndFrame prunes.</summary>
        internal static void PopWindow()
        {
            if (s_Stack.Count == 0)
                return;
            s_Stack.RemoveAt(s_Stack.Count - 1);
        }

        static void Prune(ImContainer container)
        {
            var nodes = container.Nodes;
            if (container.Cursor >= nodes.Count)
                return;
            for (int i = container.Cursor; i < nodes.Count; i++)
                nodes[i].Element.RemoveFromHierarchy();
            nodes.RemoveRange(container.Cursor, nodes.Count - container.Cursor);
        }

        /// <summary>Get or create the child container for a container widget node.</summary>
        internal static ImContainer ChildContainer(ImNode node, VisualElement contentElement)
        {
            node.Children ??= new ImContainer { Element = contentElement };
            Touch(node.Children);
            return node.Children;
        }

        // ---- reconciliation ------------------------------------------------

        static long MakeKey(ImKind kind, string id)
            => ((long)kind << 32) | (uint)(id?.GetHashCode() ?? 0);

        /// <summary>
        /// Advance the cursor in the current container and return the element for this call —
        /// reusing the retained one when the key matches, creating a fresh one otherwise.
        /// Skipped nodes (widgets that stopped being drawn) are removed on the fly.
        /// </summary>
        internal static T NextElement<T>(ImKind kind, string id, out ImNode node, out bool created)
            where T : VisualElement, new()
        {
            var container = CurrentContainer;
            long key = MakeKey(kind, id);
            var nodes = container.Nodes;

            if (container.Cursor < nodes.Count)
            {
                if (nodes[container.Cursor].Key == key)
                {
                    node = nodes[container.Cursor++];
                    created = false;
                    return (T)node.Element;
                }

                // Scan ahead: if the matching node exists further on, the calls before it were
                // removed this frame — drop them and resync instead of recreating everything.
                for (int j = container.Cursor + 1; j < nodes.Count; j++)
                {
                    if (nodes[j].Key != key)
                        continue;
                    for (int k = container.Cursor; k < j; k++)
                        nodes[k].Element.RemoveFromHierarchy();
                    nodes.RemoveRange(container.Cursor, j - container.Cursor);
                    node = nodes[container.Cursor++];
                    created = false;
                    return (T)node.Element;
                }
            }

            var element = new T();
            node = new ImNode { Key = key, Element = element };
            nodes.Insert(container.Cursor, node);
            container.Element.Insert(container.Cursor, element);
            container.Cursor++;
            created = true;
            return element;
        }

        internal static TState State<TState>(ImNode node) where TState : class, new()
            => (TState)(node.State ??= new TState());

        /// <summary>
        /// Two-way value sync for BaseField widgets: user edits win for one frame,
        /// otherwise a changed caller value is pushed into the field without notification.
        /// </summary>
        internal static T SyncValue<T>(BaseField<T> field, ImNode node, T value, bool created)
        {
            var state = State<ImValueState<T>>(node);
            if (created)
            {
                field.SetValueWithoutNotify(value);
                state.LastPushed = value;
                field.RegisterValueChangedCallback(evt =>
                {
                    state.UserChanged = true;
                    state.UserValue = evt.newValue;
                });
                return value;
            }

            if (state.UserChanged)
            {
                state.UserChanged = false;
                state.LastPushed = state.UserValue;
                return state.UserValue;
            }

            if (!EqualityComparer<T>.Default.Equals(value, state.LastPushed))
            {
                field.SetValueWithoutNotify(value);
                state.LastPushed = value;
            }
            return value;
        }

        // ---- windows ---------------------------------------------------------

        internal static IReadOnlyDictionary<string, ImWindowRecord> Windows => s_Windows;

        internal static void BeginWindow(string title, string id, Vector2? initialPosition, float width)
        {
            id ??= title;
            if (!s_Windows.TryGetValue(id, out var window))
            {
                var position = initialPosition ?? new Vector2(24f + 32f * s_Windows.Count, 24f + 32f * s_Windows.Count);
                window = ImWindowRecord.Create(title, position);
                s_Layer.Add(window.Root);
                s_Windows.Add(id, window);
            }

            if (window.Title.text != title)
                window.Title.text = title;
            if (width > 0f && window.Width != width)
            {
                window.Width = width;
                window.Root.style.width = width;
            }

            Touch(window.Content);
            Push(window.Content);
        }

        // ---- test support ----------------------------------------------------

        internal static void TestSetup(VisualElement layer)
        {
            ResetStatics();
            s_TestMode = true;
            s_Layer = layer;
        }

        internal static ImContainer TestMainContainer => s_Main;
    }
}
