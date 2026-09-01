using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Raises one of the character's stats for the rest of the match instead of spending itself on
    /// the turn it is used in - the lasting counterpart to <see cref="HealEffect"/> and
    /// <see cref="RestoreActionPointsEffect"/>, which give back what was already there.
    ///
    /// One class for every such item: which stat is authored on the asset, and what a raise takes to
    /// be felt is <see cref="Unit.GrantStatBonus"/>'s business - so a further stat is an entry in
    /// <see cref="UnitStat"/> and a branch there, never another effect here.
    ///
    /// The bonus lives on the unit's state, so it travels with the history snapshot: undoing the use
    /// takes the stat back down along with the item.
    /// </summary>
    [System.Serializable]
    public class IncreaseStatEffect : ActiveItemEffect
    {
        [Tooltip("Which stat a use raises for good.")]
        public UnitStat Stat = UnitStat.SightRange;

        [Tooltip("How much is added. Kept for the rest of the match, and added again by a second copy.")]
        [Min(1)] public int Amount = 1;

        public override string Summary => $"Permanently +{Amount} {UnitState.NameOf(Stat)}";

        public override void Apply(Unit target)
        {
            target.GrantStatBonus(Stat, Amount);
        }
    }
}
