using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    public static partial class IM
    {
        /// <summary>Draws a text label.</summary>
        public static void Label(string text)
        {
            if (!ImContext.Prepare())
                return;
            var element = ImContext.NextElement<Label>(ImKind.Label, null, out _, out _);
            if (element.text != text)
                element.text = text;
        }

        /// <summary>Draws a bold section header.</summary>
        public static void Header(string text)
        {
            if (!ImContext.Prepare())
                return;
            var element = ImContext.NextElement<Label>(ImKind.Header, null, out _, out bool created);
            if (created)
                element.AddToClassList(ImStyles.Header);
            if (element.text != text)
                element.text = text;
        }

        /// <summary>Draws a button; returns true on the frame after it was clicked (like GUILayout.Button).</summary>
        public static bool Button(string text, string id = null)
        {
            if (!ImContext.Prepare())
                return false;
            var element = ImContext.NextElement<Button>(ImKind.Button, id, out var node, out bool created);
            var state = ImContext.State<ImClickState>(node);
            if (created)
                element.clicked += () => state.Clicked = true;
            if (element.text != text)
                element.text = text;
            if (!state.Clicked)
                return false;
            state.Clicked = false;
            return true;
        }

        /// <summary>Draws a checkbox and returns the (possibly user-edited) value.</summary>
        public static bool Toggle(string label, bool value, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<Toggle>(ImKind.Toggle, id, out var node, out bool created);
            SetLabel(element, label);
            return ImContext.SyncValue(element, node, value, created);
        }

        /// <summary>
        /// Draws a collapsible header and returns whether it is open; draw the dependent widgets
        /// conditionally, like EditorGUILayout.Foldout. The open state is remembered per label —
        /// pass <paramref name="id"/> when the label text is dynamic.
        /// </summary>
        public static bool Foldout(string label, bool defaultOpen = false, string id = null)
        {
            if (!ImContext.Prepare())
                return defaultOpen;
            var element = ImContext.NextElement<Foldout>(ImKind.Foldout, id ?? label, out _, out bool created);
            if (created)
                element.value = defaultOpen;
            if (element.text != label)
                element.text = label;
            return element.value;
        }

        /// <summary>Draws a progress bar for a value in [min, max] with an optional centered title.</summary>
        public static void ProgressBar(float value, string title = null, float min = 0f, float max = 1f)
        {
            if (!ImContext.Prepare())
                return;
            var element = ImContext.NextElement<ProgressBar>(ImKind.ProgressBar, null, out _, out _);
            if (element.lowValue != min)
                element.lowValue = min;
            if (element.highValue != max)
                element.highValue = max;
            if (element.value != value)
                element.value = value;
            title ??= string.Empty;
            if (element.title != title)
                element.title = title;
        }

        /// <summary>Draws a texture (e.g. a render target or debug texture). Pass width/height 0 for natural size.</summary>
        public static void Image(Texture texture, float width = 0f, float height = 0f)
        {
            if (!ImContext.Prepare())
                return;
            var element = ImContext.NextElement<Image>(ImKind.Image, null, out var node, out bool created);
            var state = ImContext.State<ImImageState>(node);
            if (created)
                element.scaleMode = ScaleMode.ScaleToFit;
            if (element.image != texture)
                element.image = texture;
            if (state.Width != width)
            {
                state.Width = width;
                element.style.width = width > 0f ? width : StyleKeyword.Null;
            }
            if (state.Height != height)
            {
                state.Height = height;
                element.style.height = height > 0f ? height : StyleKeyword.Null;
            }
        }

        /// <summary>Inserts fixed spacing along the current layout axis.</summary>
        public static void Space(float pixels = 6f)
        {
            if (!ImContext.Prepare())
                return;
            bool isRow = ImContext.CurrentContainer.IsRow;
            var element = ImContext.NextElement<VisualElement>(ImKind.Space, null, out var node, out bool created);
            if (created)
                element.AddToClassList(ImStyles.Space);
            var state = ImContext.State<ImFloatState>(node);
            if (created || state.Value != pixels)
            {
                state.Value = pixels;
                if (isRow)
                    element.style.width = pixels;
                else
                    element.style.height = pixels;
            }
        }

        /// <summary>Inserts a stretchy gap that pushes following widgets to the far side (GUILayout.FlexibleSpace).</summary>
        public static void FlexibleSpace()
        {
            if (!ImContext.Prepare())
                return;
            var element = ImContext.NextElement<VisualElement>(ImKind.FlexibleSpace, null, out _, out bool created);
            if (created)
                element.AddToClassList(ImStyles.FlexibleSpace);
        }

        /// <summary>Draws a thin separator line across the current layout axis.</summary>
        public static void Separator()
        {
            if (!ImContext.Prepare())
                return;
            bool isRow = ImContext.CurrentContainer.IsRow;
            var element = ImContext.NextElement<VisualElement>(ImKind.Separator, null, out _, out bool created);
            if (created)
            {
                element.AddToClassList(ImStyles.Separator);
                if (isRow)
                    element.AddToClassList(ImStyles.SeparatorVertical);
            }
        }

        static void SetLabel<T>(BaseField<T> field, string label)
        {
            if (field.label != label)
                field.label = label;
        }
    }

    internal sealed class ImImageState
    {
        public float Width;
        public float Height;
    }

    internal sealed class ImFloatState
    {
        public float Value;
    }
}
