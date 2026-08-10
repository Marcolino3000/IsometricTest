using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    public abstract class Trait : ScriptableObject
    {
        [Tooltip("Designer-facing note describing what this trait does. Purely informational.")]
        [TextArea] public string Description;

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
        /// The action points one step onto <see cref="MoveContext.Tile"/> costs. Folded by
        /// <see cref="Global.MovementRules"/>, which clamps the result so a step is never free.
        /// </summary>
        public virtual int ModifyMoveCost(int cost, MoveContext context) => cost;
    }
}
