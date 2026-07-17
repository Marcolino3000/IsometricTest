using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    public static partial class IM
    {
        /// <summary>
        /// Opens a draggable overlay window (GUILayout.Window equivalent). Windows are identified by
        /// title (or <paramref name="id"/>) and keep their position across frames.
        /// <code>using (IM.Window("Debug")) { IM.Label("hi"); }</code>
        /// </summary>
        public static ImScope Window(string title, string id = null)
        {
            if (!ImContext.Prepare())
                return default;
            ImContext.BeginWindow(title, id, null, 0f);
            return new ImScope(ImScopeKind.Window);
        }

        /// <summary>Opens a window with an initial position (applied once; the user can drag it afterwards) and optional fixed width.</summary>
        public static ImScope Window(string title, Vector2 initialPosition, float width = 0f, string id = null)
        {
            if (!ImContext.Prepare())
                return default;
            ImContext.BeginWindow(title, id, initialPosition, width);
            return new ImScope(ImScopeKind.Window);
        }

        /// <summary>Lays out children horizontally (GUILayout.BeginHorizontal equivalent).</summary>
        public static ImScope Row(string id = null)
            => OpenContainer(ImKind.Row, ImStyles.Row, isRow: true, id);

        /// <summary>Lays out children vertically inside a row (GUILayout.BeginVertical equivalent).</summary>
        public static ImScope Column(string id = null)
            => OpenContainer(ImKind.Column, ImStyles.Column, isRow: false, id);

        /// <summary>Indents children (handy under a Foldout).</summary>
        public static ImScope Indent(string id = null)
            => OpenContainer(ImKind.Indent, ImStyles.Indent, isRow: false, id);

        /// <summary>Draws a framed group with an optional bold title (GUILayout.Box-ish).</summary>
        public static ImScope Box(string title = null, string id = null)
        {
            if (!ImContext.Prepare())
                return default;
            var element = ImContext.NextElement<VisualElement>(ImKind.Box, id, out var node, out bool created);
            if (created)
                element.AddToClassList(ImStyles.Box);

            var state = ImContext.State<ImBoxState>(node);
            if (title != null)
            {
                if (state.Title == null)
                {
                    state.Title = new Label();
                    state.Title.AddToClassList(ImStyles.BoxTitle);
                    element.hierarchy.Insert(0, state.Title);
                }
                if (state.Title.text != title)
                    state.Title.text = title;
            }
            else if (state.Title != null)
            {
                state.Title.RemoveFromHierarchy();
                state.Title = null;
            }

            var container = ImContext.ChildContainer(node, state.Content ??= CreateBoxContent(element));
            ImContext.Push(container);
            return new ImScope(ImScopeKind.Container);
        }

        static VisualElement CreateBoxContent(VisualElement box)
        {
            var content = new VisualElement { name = "im-box-content" };
            box.hierarchy.Add(content);
            return content;
        }

        /// <summary>Opens a scroll view; scroll position is retained across frames.</summary>
        public static ImScope Scroll(float maxHeight = 0f, string id = null)
        {
            if (!ImContext.Prepare())
                return default;
            var element = ImContext.NextElement<ScrollView>(ImKind.Scroll, id, out var node, out bool created);
            if (created)
                element.AddToClassList(ImStyles.Scroll);

            var state = ImContext.State<ImFloatState>(node);
            if (created || state.Value != maxHeight)
            {
                state.Value = maxHeight;
                element.style.maxHeight = maxHeight > 0f ? maxHeight : StyleKeyword.Null;
            }

            var container = ImContext.ChildContainer(node, element.contentContainer);
            ImContext.Push(container);
            return new ImScope(ImScopeKind.Container);
        }

        static ImScope OpenContainer(ImKind kind, string className, bool isRow, string id)
        {
            if (!ImContext.Prepare())
                return default;
            var element = ImContext.NextElement<VisualElement>(kind, id, out var node, out bool created);
            if (created)
                element.AddToClassList(className);
            var container = ImContext.ChildContainer(node, element);
            container.IsRow = isRow;
            ImContext.Push(container);
            return new ImScope(ImScopeKind.Container);
        }
    }

    internal sealed class ImBoxState
    {
        public Label Title;
        public VisualElement Content;
    }
}
