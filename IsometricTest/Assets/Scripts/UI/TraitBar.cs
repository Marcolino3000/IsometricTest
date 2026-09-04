using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The row of trait symbols above the character's health, in the HUD - what it is carrying and
    /// what has been put on it, read alongside the board rather than off the badges over its head.
    ///
    /// A piece of a view like <see cref="PlayerVitals"/>: it is put into whatever element it is
    /// handed and decides nothing about where that element sits. It draws the traits alone - the
    /// weapon's name is in the item bar under it, and saying it twice would be the widest thing
    /// here - but the traits it draws include the ones the drawn weapon grants, which is what a
    /// weapon is worth beyond its numbers. Which of the two a symbol came from is on its card.
    ///
    /// <b>Unlike the rest of the HUD it picks.</b> A symbol has to be hovered to be worth anything -
    /// a trait is a picture until something says what it does - so each one takes the pointer and
    /// reports itself to <see cref="HoverTarget"/>, exactly as an item slot does. The row between
    /// them does not, so a click that falls in a gap still reaches the board.
    ///
    /// It asks the trait what to say (<c>Trait.Describe</c>) rather than assembling a card out of
    /// its fields, the way a tile asks the lootbox lying on it - so a trait added later is labelled
    /// without this class being touched.
    /// </summary>
    public class TraitBar
    {
        /// <summary>The row itself, for the caller to place. Empty until a unit is shown.</summary>
        public VisualElement Root { get; }

        private readonly HoverTarget hoverTarget;
        private readonly PlayerVitalsSettings settings;

        // What each drawn symbol says, by the element that draws it - held so a hover can answer
        // without the trait being looked up again, and so a Refresh under a resting cursor can say
        // it afresh rather than leaving the card of a trait that has since worn off.
        private readonly List<(VisualElement Element, TooltipContent Content)> drawn = new();

        private Unit unit;
        private VisualElement hovered;

        public TraitBar(HoverTarget hoverTargetArg, PlayerVitalsSettings settingsArg)
        {
            hoverTarget = hoverTargetArg;
            settings = settingsArg;

            Root = new VisualElement { name = "traitRow", pickingMode = PickingMode.Ignore };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.alignItems = Align.Center;
            Root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Draws the traits of <paramref name="shown"/> from now on, and follows them: what a unit
        /// carries changes while the match runs - a status put on, a passive worn, an undo taking
        /// either back - and <c>UnitState.LoadoutChanged</c> is raised for all of them.
        /// </summary>
        public void Show(Unit shown)
        {
            if (unit == shown)
                return;

            Unsubscribe();

            unit = shown;

            if (unit != null)
                unit.CurrentState.LoadoutChanged += Refresh;

            Refresh();
        }

        /// <summary>Restyles what is drawn - answered when the settings asset is edited.</summary>
        public void ApplyLook()
        {
            foreach (var (element, _) in drawn)
                StyleSymbol(element);
        }

        /// <summary>Rebuilds the row from what the unit carries right now.</summary>
        private void Refresh()
        {
            // The cursor may be resting on a symbol that is about to be rebuilt - or on one for a
            // status that has just worn off. Its card goes with it; a pointer still over the row
            // will enter the new element and say the new thing.
            ClearTooltip();

            Root.Clear();
            drawn.Clear();

            if (unit != null && unit.IsAlive)
            {
                foreach (var carried in UnitRules.GetTraits(unit.CurrentState))
                    Add(carried);
            }

            // A row with nothing in it would be an empty frame above the health - the character
            // carrying nothing is not worth a line of its own.
            Root.style.display = drawn.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Add(TraitCount carried)
        {
            var trait = carried.Trait;
            var content = trait.Describe(carried.Count, carried.FromWeapon);

            // The symbol where one is authored, the name where none is - the badge row's rule, so a
            // trait reads before anybody has drawn a picture for it.
            var symbol = trait.Icon != null ? Symbol(trait.Icon) : Name(content.Title);

            StyleSymbol(symbol);

            symbol.RegisterCallback<PointerEnterEvent>(_ => Hover(symbol, content));
            symbol.RegisterCallback<PointerLeaveEvent>(_ => Leave(symbol));

            // How many of it, where that is more than one - the same mark the badges over the
            // unit's head carry, and Capability.Title is where it is worded.
            if (carried.Count > 1)
                symbol.Add(Count(carried.Count));

            drawn.Add((symbol, content));
            Root.Add(symbol);
        }

        private VisualElement Symbol(Sprite sprite)
        {
            var element = new VisualElement { name = "traitSymbol" };

            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            element.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));

            return element;
        }

        private VisualElement Name(string text)
        {
            var element = new VisualElement { name = "traitSymbol" };
            element.style.width = StyleKeyword.Auto;

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = settings.TraitIconSize * 0.4f;
            label.style.color = CardStyle.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            element.Add(label);

            return element;
        }

        /// <summary>
        /// The count, in the corner of the symbol it belongs to rather than beside it: the row is
        /// read across, so a number standing between two symbols would read as belonging to either.
        /// </summary>
        private Label Count(int count)
        {
            var label = new Label($"×{count}") { pickingMode = PickingMode.Ignore };

            label.style.position = Position.Absolute;
            label.style.right = -2f;
            label.style.bottom = -4f;
            label.style.fontSize = settings.TraitIconSize * 0.42f;
            label.style.color = CardStyle.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            return label;
        }

        private void StyleSymbol(VisualElement element)
        {
            element.style.height = settings.TraitIconSize;
            element.style.marginLeft = settings.BlobGap;
            element.style.marginRight = settings.BlobGap;
            element.style.justifyContent = Justify.Center;
            element.style.alignItems = Align.Center;

            // A picture is worth hovering, so it takes the pointer - the one thing in this block
            // that does.
            element.pickingMode = PickingMode.Position;

            if (element.style.width.keyword != StyleKeyword.Auto)
                element.style.width = settings.TraitIconSize;
        }

        private void Hover(VisualElement symbol, TooltipContent content)
        {
            hovered = symbol;

            hoverTarget?.SetUiTooltip(content, TooltipAnchor.Element(symbol, TooltipSide.Above));
        }

        private void Leave(VisualElement symbol)
        {
            // Only the one that has the card takes it away: leaving an element that was rebuilt out
            // from under the cursor must not clear the card the new one has just put up.
            if (hovered != symbol)
                return;

            ClearTooltip();
        }

        private void ClearTooltip()
        {
            if (hovered == null)
                return;

            hovered = null;
            hoverTarget?.SetUiTooltip(TooltipContent.Empty, default);
        }

        private void Unsubscribe()
        {
            if (unit != null && unit.CurrentState != null)
                unit.CurrentState.LoadoutChanged -= Refresh;
        }
    }
}
