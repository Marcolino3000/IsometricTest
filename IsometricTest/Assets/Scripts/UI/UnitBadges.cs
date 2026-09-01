using Actions;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The row of badges over a unit's head: one per <see cref="Capability"/>, drawn as the asset's
    /// symbol where there is one and as its name where there is not, so a trait works - and reads -
    /// before anybody has drawn an icon for it.
    ///
    /// Built in code on a world-space <see cref="UIDocument"/> of its own like the floating damage
    /// text, so the unit prefab needs no further object and the row can be as long as the unit's
    /// gear makes it. It borrows the health bar's panel settings, which is what makes it scale and
    /// sort with the bars, and it is a child of the unit, which is what puts it under
    /// <see cref="Unit.SetRevealed"/> - an enemy in the fog gives nothing away.
    ///
    /// A pure view like <see cref="ItemBar"/>: it is handed capabilities and knows nothing about
    /// weapons, traits or action points.
    /// </summary>
    public class UnitBadges : MonoBehaviour
    {
        /// <summary>Matches the floating text's, so a badge and a damage number are drawn at one size.</summary>
        private const float DocumentScale = 0.2f;

        /// <summary>Clear of the action point bar, which is the lowest of the two bars.</summary>
        private static readonly Vector3 Offset = new(0f, 0.62f, 0f);

        private const float IconSize = 56f;
        private const int FontSize = 34;

        private Unit unit;
        private VisualElement root;

        /// <summary>
        /// Hangs a badge row on <paramref name="unit"/>. <paramref name="panelSettings"/> is the
        /// health bar's, so the badges live on the same world-space panel the bars do.
        /// </summary>
        public static UnitBadges Create(Unit unit, PanelSettings panelSettings)
        {
            if (unit == null || panelSettings == null)
                return null;

            var host = new GameObject(nameof(UnitBadges));
            host.transform.SetParent(unit.transform, false);
            host.transform.localPosition = Offset;
            host.transform.localScale = Vector3.one * DocumentScale;
            host.layer = LayerMask.NameToLayer("UI");

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            var badges = host.AddComponent<UnitBadges>();
            badges.Build(unit, document);

            return badges;
        }

        /// <summary>Rebuilds the row from what the unit can do right now.</summary>
        public void Refresh()
        {
            if (unit == null || root == null)
                return;

            root.Clear();

            foreach (var capability in UnitRules.GetCapabilities(unit))
                root.Add(BuildBadge(capability));
        }

        private void Build(Unit owner, UIDocument document)
        {
            unit = owner;

            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            // Two things change what a unit can do and they now both say so: what it carries, and
            // the ground under it - the tile is in here because what a weapon reaches depends on
            // where it is standing, which is the one capability that moves with nothing picked up.
            unit.CurrentState.LoadoutChanged += Refresh;
            unit.CurrentState.PositionChanged += HandlePositionChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            if (unit == null || unit.CurrentState == null)
                return;

            unit.CurrentState.LoadoutChanged -= Refresh;
            unit.CurrentState.PositionChanged -= HandlePositionChanged;
        }

        private void HandlePositionChanged(Runtime.Core.State.ChangeEvent<Tile> changeEvent) => Refresh();

        private static VisualElement BuildBadge(Capability capability)
        {
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.marginLeft = 4f;
            badge.style.marginRight = 4f;
            badge.style.backgroundColor = CardStyle.OverlayBackground;

            CardStyle.SetPadding(badge, 4f, 8f);
            CardStyle.SetBorder(badge, 2f, 8f);

            if (capability.Icon != null)
                badge.Add(BuildIcon(capability.Icon));
            else
                badge.Add(BuildLabel(capability.Label));

            return badge;
        }

        private static VisualElement BuildIcon(Sprite sprite)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = IconSize;
            icon.style.height = IconSize;
            icon.style.backgroundImage = new StyleBackground(sprite);
            icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            icon.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));

            return icon;
        }

        private static Label BuildLabel(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = FontSize;
            label.style.color = CardStyle.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            return label;
        }
    }
}
