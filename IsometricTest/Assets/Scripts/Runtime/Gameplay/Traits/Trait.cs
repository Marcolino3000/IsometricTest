using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    public abstract class Trait : ScriptableObject
    {
        [Tooltip("Designer-facing note describing what this trait does. Purely informational.")]
        [TextArea] public string Description;

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
