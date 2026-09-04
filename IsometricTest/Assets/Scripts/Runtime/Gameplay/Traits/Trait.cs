using System;
using Runtime.Gameplay.Global;
using UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Runtime.Gameplay.Traits
{
    public abstract class Trait : ScriptableObject
    {
        [Tooltip("Designer-facing note describing what this trait does. Purely informational.")]
        [TextArea] public string Description;

        [Header("Symbol")]
        [Tooltip("Sheet the symbol is cut from - one of those in Resources/Visuals/Icons. Optional: " +
                 "a trait with none is badged with its name instead, so nothing has to be drawn " +
                 "before a trait works.")]
        [SerializeField] private Texture2D iconSheet;

        [Tooltip("Cell on the sheet, counted from the top left corner and starting at one - the " +
                 "numbers a sprite sheet viewer prints, the same convention the action and slot " +
                 "icon tables are authored in.")]
        [SerializeField] private Vector2Int iconCell = Vector2Int.one;

        [Tooltip("Edge length of one cell in pixels. The sheet is taken to be an even grid of them.")]
        [Min(1)] [SerializeField] private int iconCellSize = 16;

        [Tooltip("A symbol authored as a sprite of its own, which wins over the sheet above. Only " +
                 "needed for art that is not on one of the sheets.")]
        [FormerlySerializedAs("Icon")]
        [SerializeField] private Sprite icon;

        // Cut on first use and kept, since the badge row and the HUD both ask for it and every
        // Refresh would otherwise be a fresh sprite. Dropped in OnDisable rather than leaked one per
        // domain reload, exactly as ActionIconSet does with the cells it cuts.
        [NonSerialized] private Sprite cutIcon;

        /// <summary>
        /// The symbol drawn for this trait - the sprite authored on it, or the cell it names on a
        /// sheet, or null where it names neither, which is what leaves it badged with its name.
        ///
        /// <b>Where the cell is authored is the point.</b> It sits on the trait rather than in a
        /// table keyed by trait, for the reason <c>Item.FoundIn</c> names its own lootbox tier: a
        /// second list of which trait is which picture is a second place to keep in step, and it
        /// drifts the first time a trait is added and only half wired up.
        /// </summary>
        public Sprite Icon
        {
            get
            {
                if (icon != null)
                    return icon;

                // A status is applied as a copy of its asset, and a copy per affliction would cut a
                // sprite per affliction. The asset it came from cuts one and every copy shows it.
                var owner = IconOwner;

                if (owner != this)
                    return owner.Icon;

                if (cutIcon == null)
                    cutIcon = IconSheet.Cut(iconSheet, iconCellSize, iconCell, this, "badge symbol");

                return cutIcon;
            }
        }

        /// <summary>
        /// Whose symbol this trait shows. Itself for everything authored as an asset; overridden by
        /// anything that is a runtime copy of one - see <see cref="StatusTrait"/>.
        /// </summary>
        protected virtual Trait IconOwner => this;

        /// <summary>
        /// What kind of thing the card calls this - what stands under its name. Overridden where a
        /// trait is worn rather than carried.
        /// </summary>
        protected virtual string KindName => "Trait";

        protected virtual void OnDisable()
        {
            IconSheet.Release(cutIcon);
            cutIcon = null;
        }

        /// <summary>
        /// The card shown while the cursor rests on this trait's symbol: its name, what kind of
        /// thing it is, the designer's note and the one line of numbers <see cref="Summary"/> is.
        ///
        /// A trait describing itself, the way <c>Item.Describe</c> does - so a view drawing traits
        /// is handed content and told nothing about traits, and a new trait is labelled the moment
        /// it exists. <paramref name="count"/> is how many of it the unit carries and
        /// <paramref name="fromWeapon"/> whether it comes off the weapon in hand rather than off the
        /// unit - which is what makes it go away again when the other weapon is drawn, and the one
        /// thing about a trait that is not the trait's own to know; see <c>UnitRules.GetTraits</c>.
        /// </summary>
        public TooltipContent Describe(int count = 1, bool fromWeapon = false)
        {
            var summary = Summary;

            // Summary falls back to the note where a trait has no numbers, and to the name where it
            // has neither - so a line equal to either of those would only be said twice.
            var stats = string.IsNullOrWhiteSpace(summary) || summary == name || summary == Description
                ? null
                : new[] { summary };

            return new TooltipContent(
                count > 1 ? $"{name} ×{count}" : name,
                fromWeapon ? "Weapon Trait" : KindName,
                Description,
                stats,
                icon: Icon);
        }

        /// <summary>
        /// One short line saying what this trait does, in numbers - what a passive item carrying it
        /// reports when it is found and what the card labelling a unit or a tile prints under the
        /// trait's name. A trait with numbers of its own builds it from them; the default falls back
        /// to the authored note, so a trait that has none still reads as something.
        ///
        /// <b>The stat, then the number</b> - "Defense +3", "Move cost -1" - the shape a tile's own
        /// numbers are printed in, and never a sentence: the name above it already says what the
        /// trait is, so this line is only what it is worth. Anything that has to be said in words
        /// belongs in <see cref="Description"/>, which is the fallback rather than the line.
        /// </summary>
        public virtual string Summary => string.IsNullOrWhiteSpace(Description) ? name : Description;

        /// <summary>A number as a summary prints it, with its sign - shared so every line matches.</summary>
        protected static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

        public virtual int ModifyOutgoingDamage(int damage, CombatContext context) => damage;

        public virtual int ModifyIncomingDamage(int damage, CombatContext context) => damage;

        public virtual int ModifyAttackRange(int range, RangeContext context) => range;

        /// <summary>
        /// How far the carrier sees from <see cref="SightContext.Tile"/>. Folded by
        /// <see cref="Global.SightRules"/>, which never lets it fall below zero. Only the reach of
        /// the eye - what higher ground hides from it is decided by the tiles in between.
        /// </summary>
        public virtual int ModifySightRange(int range, SightContext context) => range;

        /// <summary>
        /// Whether the carrier strikes back after being hit, given what the match rules allow.
        /// Folded by <see cref="Global.CombatRules.CanRetaliate"/> over the traits of whoever would
        /// answer, so gear can grant a counter-strike the rules withhold or take one away. The
        /// context is the counter-strike itself, i.e. the carrier is its
        /// <see cref="CombatContext.Attacker"/>. Only the right to answer - the reach to do it is
        /// still checked afterwards, through <see cref="ModifyAttackRange"/>.
        /// </summary>
        public virtual bool ModifyRetaliation(bool canRetaliate, CombatContext context) => canRetaliate;

        /// <summary>
        /// The carrier's turn has come round, before it may act. The one hook that <b>does</b>
        /// something rather than shaping a number somebody else is folding - a bleed takes health, a
        /// regeneration gives it back - so it goes through the unit it is handed rather than
        /// returning a value: what a status does is not one number.
        ///
        /// Asked of everything <c>CombatRules.TraitsAffecting</c> folds, so ground a unit is standing
        /// on can act on it as surely as something it carries. Called only for the team whose turn it
        /// is, once per turn, and never while a snapshot is being restored - see
        /// <c>StatusRunner</c>, which is the one caller.
        /// </summary>
        public virtual void OnTurnBegan(TurnContext context)
        {
        }

        /// <summary>
        /// The action points one step onto <see cref="MoveContext.Tile"/> costs. Folded by
        /// <see cref="Global.MovementRules"/>, which clamps the result so a step is never free.
        /// </summary>
        public virtual int ModifyMoveCost(int cost, MoveContext context) => cost;
    }
}
