using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The card in the corner while a unit is hovered: its vitals, and one line per
    /// <see cref="Capability"/> saying what each of the badges over its head actually means. The
    /// badges answer "what is that thing" at a glance; this answers "with what numbers".
    ///
    /// Driven off <see cref="Selector.OnSelectionChanged"/> rather than by anything pushing at it,
    /// like the attack preview is - which is also what makes it go away on a deselection, a turn
    /// change or a unit dying under the cursor, without any of those paths knowing it exists. It
    /// appears and disappears with the hover: there is nothing to dismiss, unlike the find popup,
    /// because nothing was found.
    ///
    /// Built in code on a <see cref="UIDocument"/> of its own and click-through in the same way, so
    /// it never takes a click meant for the board underneath it.
    /// </summary>
    public class UnitCard : MonoBehaviour
    {
        private const float CardWidth = 320f;

        private Selector selector;
        private GameRules rules;

        private VisualElement root;
        private VisualElement capabilities;
        private Label title;
        private Label subtitle;
        private Label vitals;

        /// <summary>
        /// Creates the card on a document of its own. <paramref name="panelSettings"/> is the HUD's,
        /// so it scales with the rest of the interface.
        /// </summary>
        public static UnitCard Create(PanelSettings panelSettings, float sortingOrder)
        {
            var host = new GameObject(nameof(UnitCard));

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            var card = host.AddComponent<UnitCard>();
            card.Build(document);

            return card;
        }

        /// <summary>
        /// The rules go in live, like everywhere else, so the card can be switched off during play -
        /// it simply stops answering the hover.
        /// </summary>
        public UnitCard Setup(Selector selectorArg, GameRules gameRules)
        {
            selector = selectorArg;
            rules = gameRules;

            selector.OnSelectionChanged += HandleSelectionChanged;

            return this;
        }

        private void OnDestroy()
        {
            if (selector != null)
                selector.OnSelectionChanged -= HandleSelectionChanged;
        }

        // Fully qualified: UI Toolkit has a ChangeEvent of its own, and this file draws with it.
        private void HandleSelectionChanged(Runtime.Core.State.ChangeEvent<Selection> changeEvent)
        {
            if (rules == null || !rules.ShowUnitCard)
            {
                Hide();
                return;
            }

            switch (changeEvent.NewValue.Status)
            {
                case SelectionStatus.NoSelectionFriendlyHover:
                case SelectionStatus.NoSelectionEnemyHover:
                case SelectionStatus.SelectionFriendlyHover:
                case SelectionStatus.SelectionEnemyHover:
                    Show(changeEvent.NewValue.HoveredUnit);
                    break;

                default:
                    Hide();
                    break;
            }
        }

        private void Show(Unit unit)
        {
            // A hover can outlive the unit it points at - a removed unit is hidden rather than
            // destroyed, so the reference goes on looking perfectly valid.
            if (unit == null || !unit.IsAlive)
            {
                Hide();
                return;
            }

            var state = unit.CurrentState;

            title.text = unit.Blueprint != null ? unit.Blueprint.name : unit.name;
            subtitle.text = state.Team.ToString();
            vitals.text = $"Health {state.Health}/{unit.MaxHealth}    " +
                          $"AP {state.ActionPoints}/{unit.MaxActionPoints}    " +
                          // What it sees from where it stands, ground included - the number the fog
                          // is actually drawn from, not the one on the blueprint.
                          $"Sight {SightRules.GetSightRange(unit)}";

            capabilities.Clear();

            foreach (var capability in UnitRules.GetCapabilities(unit))
                AddCapability(capability);

            root.style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            if (root != null)
                root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// One capability: what it is called, and under it what it amounts to. The detail is dropped
        /// where it would only repeat the name, which is what a trait with no numbers of its own and
        /// no designer note comes to.
        /// </summary>
        private void AddCapability(Capability capability)
        {
            var entry = new VisualElement { pickingMode = PickingMode.Ignore };
            entry.style.marginTop = 8f;

            var name = TextLabel(entry, 15f, CardStyle.Text);
            name.text = capability.Label;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (!string.IsNullOrWhiteSpace(capability.Detail) && capability.Detail != capability.Label)
            {
                var detail = TextLabel(entry, 14f, CardStyle.StatText);
                detail.text = capability.Detail;
            }

            capabilities.Add(entry);
        }

        private void Build(UIDocument document)
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;
            root.style.alignItems = Align.FlexStart;
            root.style.justifyContent = Justify.FlexStart;
            root.style.display = DisplayStyle.None;

            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.width = CardWidth;
            card.style.marginTop = 20f;
            card.style.marginLeft = 20f;
            card.style.backgroundColor = CardStyle.Background;

            CardStyle.SetPadding(card, 14f, 16f);
            CardStyle.SetBorder(card, 2f, 8f);

            root.Add(card);

            title = TextLabel(card, 20f, CardStyle.Text);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            subtitle = TextLabel(card, 14f, CardStyle.MutedText);
            subtitle.style.marginTop = 2f;

            vitals = TextLabel(card, 15f, CardStyle.Text);
            vitals.style.marginTop = 8f;

            capabilities = new VisualElement { pickingMode = PickingMode.Ignore };
            capabilities.style.marginTop = 4f;
            card.Add(capabilities);
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
