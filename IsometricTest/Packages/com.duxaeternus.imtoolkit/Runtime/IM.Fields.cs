using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    public static partial class IM
    {
        /// <summary>Draws a float slider with a numeric input box; returns the (possibly user-edited) value.</summary>
        public static float Slider(string label, float value, float min, float max, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<Slider>(ImKind.Slider, id, out var node, out bool created);
            if (created)
                element.showInputField = true;
            SetLabel(element, label);
            if (element.lowValue != min)
                element.lowValue = min;
            if (element.highValue != max)
                element.highValue = max;
            return ImContext.SyncValue(element, node, value, created);
        }

        /// <summary>Draws an integer slider with a numeric input box; returns the (possibly user-edited) value.</summary>
        public static int Slider(string label, int value, int min, int max, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<SliderInt>(ImKind.SliderInt, id, out var node, out bool created);
            if (created)
                element.showInputField = true;
            SetLabel(element, label);
            if (element.lowValue != min)
                element.lowValue = min;
            if (element.highValue != max)
                element.highValue = max;
            return ImContext.SyncValue(element, node, value, created);
        }

        /// <summary>Draws a text field; returns the (possibly user-edited) value.</summary>
        public static string TextField(string label, string value, bool multiline = false, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<TextField>(ImKind.TextField, id, out var node, out bool created);
            if (created)
                element.multiline = multiline;
            SetLabel(element, label);
            return ImContext.SyncValue(element, node, value ?? string.Empty, created);
        }

        /// <summary>Draws a float input field; returns the (possibly user-edited) value.</summary>
        public static float FloatField(string label, float value, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<FloatField>(ImKind.FloatField, id, out var node, out bool created);
            SetLabel(element, label);
            return ImContext.SyncValue(element, node, value, created);
        }

        /// <summary>Draws an integer input field; returns the (possibly user-edited) value.</summary>
        public static int IntField(string label, int value, string id = null)
        {
            if (!ImContext.Prepare())
                return value;
            var element = ImContext.NextElement<IntegerField>(ImKind.IntField, id, out var node, out bool created);
            SetLabel(element, label);
            return ImContext.SyncValue(element, node, value, created);
        }

        /// <summary>
        /// Draws a dropdown over <paramref name="options"/>; returns the selected index.
        /// Reuse the same list instance across frames to avoid rebuilding choices.
        /// </summary>
        public static int Dropdown(string label, int index, List<string> options, string id = null)
        {
            if (!ImContext.Prepare())
                return index;
            return DropdownCore(ImKind.Dropdown, label, index, options, id);
        }

        /// <summary>Draws a dropdown over all values of an enum; returns the selected value.</summary>
        public static T EnumPopup<T>(string label, T value, string id = null) where T : struct, Enum
        {
            if (!ImContext.Prepare())
                return value;
            int index = DropdownCore(ImKind.EnumPopup, label, ImEnumCache<T>.IndexOf(value), ImEnumCache<T>.Names, id);
            var values = ImEnumCache<T>.Values;
            return values.Length == 0 ? value : values[Mathf.Clamp(index, 0, values.Length - 1)];
        }

        static int DropdownCore(ImKind kind, string label, int index, List<string> options, string id)
        {
            var element = ImContext.NextElement<DropdownField>(kind, id, out var node, out bool created);
            var state = ImContext.State<ImDropdownState>(node);
            SetLabel(element, label);

            if (options == null || options.Count == 0)
                return index;

            if (!ReferenceEquals(state.Options, options))
            {
                state.Options = options;
                element.choices = options;
            }

            int clamped = Mathf.Clamp(index, 0, options.Count - 1);
            if (created)
            {
                element.SetValueWithoutNotify(options[clamped]);
                state.LastPushed = clamped;
                element.RegisterValueChangedCallback(_ =>
                {
                    state.UserChanged = true;
                    state.UserIndex = element.index;
                });
                return clamped;
            }

            if (state.UserChanged)
            {
                state.UserChanged = false;
                state.LastPushed = state.UserIndex;
                return state.UserIndex;
            }

            if (clamped != state.LastPushed)
            {
                element.SetValueWithoutNotify(options[clamped]);
                state.LastPushed = clamped;
            }
            return clamped;
        }
    }

    internal sealed class ImDropdownState
    {
        public List<string> Options;
        public int LastPushed = -1;
        public bool UserChanged;
        public int UserIndex;
    }

    internal static class ImEnumCache<T> where T : struct, Enum
    {
        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
        public static readonly List<string> Names = new List<string>(Enum.GetNames(typeof(T)));

        public static int IndexOf(T value)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(Values[i], value))
                    return i;
            }
            return 0;
        }
    }
}
