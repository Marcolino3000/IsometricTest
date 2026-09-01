using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    public abstract class Trait : ScriptableObject
    {
        [Tooltip("Designer-facing note describing what this trait does. Purely informational.")]
        [TextArea] public string Description;

        [Tooltip("Symbol shown on the badge over a unit carrying this trait. Optional - a trait with " +
                 "none is badged with its name instead, so nothing has to be drawn before a trait works.")]
        public Sprite Icon;

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
        /// The action points one step onto <see cref="MoveContext.Tile"/> costs. Folded by
        /// <see cref="Global.MovementRules"/>, which clamps the result so a step is never free.
        /// </summary>
        public virtual int ModifyMoveCost(int cost, MoveContext context) => cost;
    }
}
