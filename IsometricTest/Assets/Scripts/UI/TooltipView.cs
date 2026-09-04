using System.Collections.Generic;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The one card that labels things - an item slot, an empty slot, a picker entry, a merge slot,
    /// a unit, a tile, the box lying on it. Before this the item bar and the merge screen each had a
    /// tooltip of their own and the board had none but the unit card in the corner, so three windows
    /// labelled things three ways.
    ///
    /// A pure view, like the bar it replaced the tooltip of: it asks <see cref="HoverTarget"/> what
    /// the cursor is on and draws the <see cref="TooltipContent"/> it gets back. What produced that
    /// content - an item, a unit, a tile - it is never told. It is also the unit card: a hovered unit
    /// describes itself with the same vitals and capability rows the corner card used to draw, so
    /// there is one card and one look rather than two.
    ///
    /// Built in code on a <see cref="UIDocument"/> of its own like <see cref="ItemPopup"/>, sorting
    /// above every other card - it labels the merge screen, which is itself a modal over everything
    /// else. It blocks nothing and picks nothing: <c>Raycaster</c> picks every document on the HUD's
    /// panel together, so a pickable tooltip under the cursor would cancel the very hover that put it
    /// there, and the card would flicker on and off. Nothing has to hide it while a modal is up
    /// either - the find popup covers the screen and takes the pointer, so no hover survives it.
    /// </summary>
    public class TooltipView : MonoBehaviour
    {
        /// <summary>How close a card may come to the edge of the screen, in panel pixels.</summary>
        private const float ScreenMargin = 8f;

        private const float IconSize = 48f;

        private HoverTarget hoverTarget;
        private TooltipSettings settings;

        private VisualElement root;
        private VisualElement card;
        private VisualElement header;
        private VisualElement icon;
        private Label title;
        private Label kind;
        private Label description;
        private VisualElement stats;
        private VisualElement entries;

        private IVisualElementScheduledItem timer;
        private TooltipContent shown = TooltipContent.Empty;
        private bool visible;
        private float nextRefresh;
        private Vector2 placedAt = new(float.NaN, float.NaN);
        private Camera cam;

        /// <summary>
        /// Creates the view on a document of its own. <paramref name="panelSettings"/> is the HUD's,
        /// so it scales with the rest of the interface.
        /// </summary>
        public static TooltipView Create(PanelSettings panelSettings, float sortingOrder)
        {
            var host = new GameObject(nameof(TooltipView));

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var view = host.AddComponent<TooltipView>();
            view.Build(document);

            return view;
        }

        public TooltipView Setup(HoverTarget hover)
        {
            hoverTarget = hover;
            hoverTarget.Changed += HandleHoverChanged;

            return this;
        }

        private void OnDestroy()
        {
            if (hoverTarget != null)
                hoverTarget.Changed -= HandleHoverChanged;
        }

        private void OnEnable()
        {
            cam = Camera.main;
            settings = TooltipSettings.Load();
        }

        /// <summary>
        /// The cursor has come to rest on something else. A card already up moves straight to the new
        /// thing - the delay is there to keep a card from appearing while the cursor is only passing
        /// over, and by now it plainly is not.
        /// </summary>
        private void HandleHoverChanged()
        {
            var content = hoverTarget.Tooltip;

            if (content.IsEmpty)
            {
                Hide();
                return;
            }

            if (visible)
            {
                Draw(content);
                return;
            }

            float delay = hoverTarget.Source == HoverSource.Ui ? settings.UiDelay : settings.WorldDelay;

            // One scheduled item, restarted, rather than a new one per hover: a paused item is kept
            // by the panel's scheduler, and the cursor crosses a great many things in a match.
            timer ??= root.schedule.Execute(Open);
            timer.Pause();
            timer.ExecuteLater((long)(delay * 1000f));
        }

        private void Open()
        {
            var content = hoverTarget.Tooltip;

            if (content.IsEmpty)
                return;

            visible = true;
            card.style.display = DisplayStyle.Flex;

            Draw(content);
        }

        private void Hide()
        {
            timer?.Pause();
            visible = false;
            shown = TooltipContent.Empty;

            if (card != null)
                card.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Keeps the card on the thing it describes: the numbers are re-read at a slow interval, so a
        /// unit losing health under the cursor shows it, and the position is followed every frame, so
        /// the card rides a walking unit and a panning camera.
        /// </summary>
        private void Update()
        {
            if (!visible)
                return;

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + settings.RefreshInterval;

                var content = hoverTarget.Tooltip;

                if (content.IsEmpty)
                {
                    Hide();
                    return;
                }

                if (!TooltipContent.Same(content, shown))
                    Draw(content);
            }

            Place();
        }

        /// <summary>
        /// Fills the card. Hidden until it has been placed once, so a rebuilt card is never seen for
        /// a frame at the position the previous one stood in.
        /// </summary>
        private void Draw(TooltipContent content)
        {
            shown = content;
            nextRefresh = Time.unscaledTime + settings.RefreshInterval;
            placedAt = new Vector2(float.NaN, float.NaN);

            card.style.maxWidth = settings.MaxWidth;
            card.style.visibility = Visibility.Hidden;

            bool hasIcon = content.Icon != null;
            icon.style.backgroundImage = hasIcon
                ? new StyleBackground(content.Icon)
                : new StyleBackground(StyleKeyword.None);
            icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;

            title.text = content.Title;
            SetText(kind, content.Kind);
            SetText(description, content.Description);

            stats.Clear();

            if (content.Stats != null)
            {
                foreach (var line in content.Stats)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var label = TextLabel(stats, 15f, CardStyle.StatText);
                    label.text = line;
                }
            }

            stats.style.display = stats.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            entries.Clear();

            if (content.Entries != null)
            {
                foreach (var entry in content.Entries)
                    AddEntry(entry);
            }

            entries.style.display = entries.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// One named row: what it is called, and under it what it amounts to. The detail is dropped
        /// where it would only repeat the name, which is what a trait with no numbers of its own and
        /// no designer note comes to.
        /// </summary>
        private void AddEntry(Capability entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Label))
                return;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.marginTop = 8f;

            var label = TextLabel(row, 15f, CardStyle.Text);
            label.text = entry.Title;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (!string.IsNullOrWhiteSpace(entry.Detail) && entry.Detail != entry.Label)
            {
                var detail = TextLabel(row, 14f, CardStyle.StatText);
                detail.text = entry.Detail;
            }

            entries.Add(row);
        }

        /// <summary>
        /// Puts the card beside what it describes and holds it on the screen. Measured rather than
        /// shifted by a percentage translate, because the card has to be clamped into the panel and
        /// that cannot be done without knowing how big it turned out.
        /// </summary>
        private void Place()
        {
            float width = card.resolvedStyle.width;
            float height = card.resolvedStyle.height;

            // Not laid out yet - the frame it was filled in.
            if (float.IsNaN(width) || width <= 0f || float.IsNaN(height) || height <= 0f)
                return;

            if (!TryResolveAnchor(out Rect anchor, out TooltipSide side))
                return;

            float gap = settings.Gap;

            Vector2 position = side switch
            {
                TooltipSide.Left => new Vector2(anchor.xMin - gap - width, anchor.center.y - height * 0.5f),
                TooltipSide.Right => new Vector2(anchor.xMax + gap, anchor.center.y - height * 0.5f),
                _ => new Vector2(anchor.center.x - width * 0.5f, anchor.yMin - gap - height)
            };

            Rect bounds = root.contentRect;

            if (bounds.width > 0f && bounds.height > 0f)
            {
                position.x = Mathf.Clamp(position.x, ScreenMargin,
                    Mathf.Max(ScreenMargin, bounds.width - width - ScreenMargin));
                position.y = Mathf.Clamp(position.y, ScreenMargin,
                    Mathf.Max(ScreenMargin, bounds.height - height - ScreenMargin));
            }

            if (Mathf.Approximately(position.x, placedAt.x) && Mathf.Approximately(position.y, placedAt.y))
                return;

            placedAt = position;
            card.style.left = position.x;
            card.style.top = position.y;
            card.style.visibility = Visibility.Visible;
        }

        /// <summary>
        /// The anchor in this document's own coordinates. A panel anchor is the element's bounds; a
        /// world one is a point on the board, converted through the camera here rather than by
        /// whoever reported it - a card over a unit has to follow it and the view.
        /// </summary>
        private bool TryResolveAnchor(out Rect rect, out TooltipSide side)
        {
            TooltipAnchor anchor = hoverTarget.Anchor;
            side = anchor.Side;

            if (anchor.Space == TooltipSpace.Panel)
            {
                Vector2 min = root.WorldToLocal(anchor.PanelRect.min);
                Vector2 max = root.WorldToLocal(anchor.PanelRect.max);

                rect = new Rect(min, max - min);

                return rect.width > 0f || rect.height > 0f;
            }

            rect = default;

            if (cam == null)
                cam = Camera.main;

            if (cam == null || root.panel == null)
                return false;

            Vector2 panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, anchor.WorldPoint, cam);
            Vector2 local = root.WorldToLocal(panelPoint);

            rect = new Rect(local, Vector2.zero);

            return true;
        }

        private void Build(UIDocument document)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;

            card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.position = Position.Absolute;
            card.style.display = DisplayStyle.None;
            card.style.backgroundColor = CardStyle.Background;
            CardStyle.SetPadding(card, 14f, 16f);
            CardStyle.SetBorder(card, 2f, 8f);

            root.Add(card);

            BuildHeader();

            description = TextLabel(card, 15f, CardStyle.Text);
            description.style.marginTop = 8f;

            stats = Column(card, 8f);
            entries = Column(card, 4f);
        }

        /// <summary>The symbol beside the name, as the find popup draws it, and the two lines of title.</summary>
        private void BuildHeader()
        {
            header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = IconSize;
            icon.style.height = IconSize;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 12f;
            icon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            icon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            icon.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            header.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.style.flexShrink = 1f;

            title = TextLabel(text, 20f, CardStyle.Text);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            kind = TextLabel(text, 14f, CardStyle.MutedText);
            kind.style.marginTop = 2f;

            header.Add(text);
            card.Add(header);
        }

        private static VisualElement Column(VisualElement parent, float marginTop)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.marginTop = marginTop;
            column.style.display = DisplayStyle.None;

            parent.Add(column);

            return column;
        }

        /// <summary>Empty text takes the line out of the layout rather than leaving a gap.</summary>
        private static void SetText(Label label, string text)
        {
            bool has = !string.IsNullOrWhiteSpace(text);

            label.text = has ? text : string.Empty;
            label.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Label TextLabel(VisualElement parent, float fontSize, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);

            return label;
        }
    }
}
