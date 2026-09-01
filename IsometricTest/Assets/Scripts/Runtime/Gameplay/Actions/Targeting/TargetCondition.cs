using System;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Actions
{
    /// <summary>
    /// Whether one unit an <see cref="ActionEffect"/> reached is actually affected. Authored inline
    /// on the effect, so a renamed or moved subclass needs <c>[MovedFrom]</c>.
    ///
    /// <b>Asked once per candidate, never once per effect.</b> "Only units that are already damaged"
    /// is a filter over who is hit, not a switch on whether the effect happens - asking it once would
    /// mean "if anybody nearby is hurt, hit everybody", which is a different mechanic. A gate on the
    /// whole effect (only on a crit, only if the target fell) would be its own list.
    /// </summary>
    [Serializable]
    public abstract class TargetCondition
    {
        [Tooltip("Turns the test around: the candidates that would be let through are the ones kept out.")]
        public bool Invert;

        /// <summary>The test itself, before <see cref="Invert"/> has its say.</summary>
        protected abstract bool Matches(Unit candidate, EffectContext context);

        /// <summary>One short line naming the test, before <see cref="Invert"/>.</summary>
        protected abstract string Describe { get; }

        public bool Holds(Unit candidate, EffectContext context)
        {
            if (candidate == null)
                return false;

            return Matches(candidate, context) != Invert;
        }

        public string Summary => Invert ? $"not {Describe}" : Describe;
    }

    /// <summary>
    /// A candidate that has already lost health. The fraction is what counts as hurt enough, so one
    /// class covers "already damaged" (1) and "below half" (0.5) alike.
    /// </summary>
    [Serializable]
    public class DamagedCondition : TargetCondition
    {
        [Tooltip("Health the candidate has to be under, as a share of its maximum. 1 is anything " +
                 "short of full health, 0.5 is below half.")]
        [Range(0.01f, 1f)] public float BelowFraction = 1f;

        protected override string Describe => BelowFraction >= 1f
            ? "already damaged"
            : $"below {BelowFraction:P0} health";

        protected override bool Matches(Unit candidate, EffectContext context)
        {
            var threshold = Mathf.CeilToInt(candidate.MaxHealth * BelowFraction);

            return candidate.CurrentState.Health < threshold;
        }
    }
}
