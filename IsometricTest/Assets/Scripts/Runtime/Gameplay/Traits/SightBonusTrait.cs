using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Terrain trait that lets the unit standing on it see further - the sight counterpart to
    /// <see cref="RangeBonusTrait"/>, and what makes a hill worth climbing before anything is in
    /// reach of it. It only widens the circle; whether the ground lets that sight through is
    /// <see cref="Global.SightRules.BlocksSight"/>'s business, so a hill both sees further and
    /// sees over what a unit below it cannot.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Terrain/Sight Bonus")]
    public class SightBonusTrait : TerrainTrait
    {
        [Tooltip("Extra tiles of sight granted while a unit occupies this terrain.")]
        public int SightBonus = 1;

        public override string Summary => $"+{SightBonus} sight while standing on this terrain";

        public override int ModifySightRange(int range, SightContext context)
        {
            return range + SightBonus;
        }
    }
}
