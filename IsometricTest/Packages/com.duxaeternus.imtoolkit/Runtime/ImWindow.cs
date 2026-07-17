using UnityEngine;
using UnityEngine.UIElements;

namespace ImToolkit
{
    /// <summary>Retained chrome for one IM.Window: root panel, draggable title bar, and the reconciled content container.</summary>
    internal sealed class ImWindowRecord
    {
        public VisualElement Root;
        public Label Title;
        public ImContainer Content;
        public float Width;

        public static ImWindowRecord Create(string title, Vector2 position)
        {
            var root = new VisualElement { name = "im-window" };
            root.AddToClassList(ImStyles.Window);
            root.style.position = Position.Absolute;
            root.style.left = position.x;
            root.style.top = position.y;
            root.RegisterCallback<PointerDownEvent>(evt => ((VisualElement)evt.currentTarget).BringToFront(),
                TrickleDown.TrickleDown);

            var titleBar = new VisualElement { name = "im-titlebar" };
            titleBar.AddToClassList(ImStyles.TitleBar);
            var titleLabel = new Label(title);
            titleLabel.AddToClassList(ImStyles.Title);
            titleBar.Add(titleLabel);
            titleBar.AddManipulator(new ImWindowDragger(root));
            root.Add(titleBar);

            var content = new VisualElement { name = "im-content" };
            content.AddToClassList(ImStyles.Content);
            root.Add(content);

            return new ImWindowRecord
            {
                Root = root,
                Title = titleLabel,
                Content = new ImContainer { Element = content },
            };
        }
    }

    /// <summary>Drags a window by its title bar, clamped so the title bar stays reachable.</summary>
    internal sealed class ImWindowDragger : PointerManipulator
    {
        readonly VisualElement m_Window;
        Vector3 m_PointerStart;
        Vector2 m_WindowStart;
        bool m_Active;

        public ImWindowDragger(VisualElement window)
        {
            m_Window = window;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;
            m_PointerStart = evt.position;
            m_WindowStart = new Vector2(m_Window.resolvedStyle.left, m_Window.resolvedStyle.top);
            m_Active = true;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_Active || !target.HasPointerCapture(evt.pointerId))
                return;

            Vector3 delta = evt.position - m_PointerStart;
            float x = m_WindowStart.x + delta.x;
            float y = m_WindowStart.y + delta.y;

            var parent = m_Window.parent;
            if (parent != null && parent.resolvedStyle.width > 0f)
            {
                x = Mathf.Clamp(x, 40f - m_Window.resolvedStyle.width, parent.resolvedStyle.width - 40f);
                y = Mathf.Clamp(y, 0f, parent.resolvedStyle.height - 28f);
            }

            m_Window.style.left = x;
            m_Window.style.top = y;
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!m_Active || !CanStopManipulation(evt))
                return;
            m_Active = false;
            target.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnCaptureOut(PointerCaptureOutEvent evt) => m_Active = false;
    }

    /// <summary>USS class names used by the package stylesheet.</summary>
    internal static class ImStyles
    {
        public const string Root = "im-root";
        public const string Main = "im-main";
        public const string Window = "im-window";
        public const string TitleBar = "im-window__titlebar";
        public const string Title = "im-window__title";
        public const string Content = "im-window__content";
        public const string Header = "im-header";
        public const string Row = "im-row";
        public const string Column = "im-col";
        public const string Box = "im-box";
        public const string BoxTitle = "im-box__title";
        public const string Scroll = "im-scroll";
        public const string Indent = "im-indent";
        public const string Space = "im-space";
        public const string FlexibleSpace = "im-flex";
        public const string Separator = "im-separator";
        public const string SeparatorVertical = "im-separator--vertical";
    }
}
