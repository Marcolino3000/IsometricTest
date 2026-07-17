using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    /// <summary>
    /// Immediate-mode UI on top of UI Toolkit. Call from any Update()/LateUpdate() — exactly like
    /// GUILayout, but rendered through a retained UI Toolkit panel that is created on first use.
    ///
    /// <code>
    /// void Update()
    /// {
    ///     using (IM.Window("Debug"))
    ///     {
    ///         IM.Label($"FPS: {1f / Time.smoothDeltaTime:0}");
    ///         if (IM.Button("Reset")) Reset();
    ///         speed = IM.Slider("Speed", speed, 0f, 10f);
    ///     }
    /// }
    /// </code>
    /// </summary>
    public static partial class IM
    {
        static float s_Scale = 1f;
        static float s_SortingOrder = 100f;

        /// <summary>UI scale of the overlay panel (1 = pixel-sized). Can be changed at runtime.</summary>
        public static float Scale
        {
            get => s_Scale;
            set
            {
                s_Scale = Mathf.Max(0.25f, value);
                ImHost.ApplyScale(s_Scale);
            }
        }

        /// <summary>Panel sorting order; defaults to 100 so the overlay sits above typical game UI.</summary>
        public static float SortingOrder
        {
            get => s_SortingOrder;
            set
            {
                s_SortingOrder = value;
                ImHost.ApplySortingOrder(value);
            }
        }

        /// <summary>Destroys all retained IM UI (window positions, foldout states, …). Rebuilt on the next IM.* call.</summary>
        public static void Clear() => ImContext.ClearAll();
    }

    enum ImScopeKind : byte
    {
        None = 0, // default(ImScope): disposing is a no-op (edit mode / inactive)
        Container = 1,
        Window = 2,
    }

    /// <summary>Scope handle returned by IM.Row/Box/Scroll/Window — dispose (via using) to close the group.</summary>
    public readonly struct ImScope : IDisposable
    {
        readonly ImScopeKind m_Kind;

        internal ImScope(ImScopeKind kind) => m_Kind = kind;

        public void Dispose()
        {
            switch (m_Kind)
            {
                case ImScopeKind.Container:
                    ImContext.Pop();
                    break;
                case ImScopeKind.Window:
                    ImContext.PopWindow();
                    break;
            }
        }
    }
}
