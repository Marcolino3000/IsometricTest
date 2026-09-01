using Runtime.Gameplay.Entities;

namespace Actions
{
    /// <summary>
    /// What an <see cref="ActionEffect"/> is being applied for: who it comes from, what it was aimed
    /// at, and where both stand. Handed to the effect's <see cref="TargetSelector"/> and to every
    /// <see cref="TargetCondition"/> it carries.
    ///
    /// One context for both kinds of action, since both ask the same question. A strike fills it with
    /// attacker and defender; using an item on oneself fills both with the user, so a selector that
    /// draws an area around the source works without either knowing about the other.
    ///
    /// The tiles are carried rather than read off the units because a strike is planned from the end
    /// of an approach path the attacker has not walked yet - the same reason <c>RangeContext</c>
    /// carries one.
    /// </summary>
    public readonly struct EffectContext
    {
        /// <summary>Whoever the effect comes from - the attacker, or the character using an item.</summary>
        public readonly Unit Source;

        /// <summary>
        /// What it was aimed at, and what an effect with no selector of its own applies to. The
        /// source itself for anything self-targeted.
        /// </summary>
        public readonly Unit Target;

        public readonly Tile SourceTile;
        public readonly Tile TargetTile;

        /// <summary>Whether this is a counter-strike, so a condition can tell one apart.</summary>
        public readonly bool IsRetaliation;

        public EffectContext(Unit source, Unit target, Tile sourceTile = null, Tile targetTile = null,
            bool isRetaliation = false)
        {
            Source = source;
            Target = target;
            SourceTile = sourceTile ?? (source != null ? source.CurrentState.Position : null);
            TargetTile = targetTile ?? (target != null ? target.CurrentState.Position : null);
            IsRetaliation = isRetaliation;
        }

        /// <summary>An effect used on whoever triggered it - an item drunk by its carrier.</summary>
        public static EffectContext SelfTargeted(Unit user)
        {
            return new EffectContext(user, user);
        }
    }
}
