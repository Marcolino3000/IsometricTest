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
        /// reports when it is found. A trait with numbers of its own builds it from them; the default
        /// falls back to the authored note, so a trait that has none still reads as something.
        /// </summary>
        public virtual string Summary => string.IsNullOrWhiteSpace(Description) ? name : Description;

        public virtual int ModifyOutgoingDamage(int damage, CombatContext context) => damage;

        public virtual int ModifyIncomingDamage(int damage, CombatContext context) => damage;

        public virtual int ModifyAttackRange(int range, RangeContext context) => range;

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
