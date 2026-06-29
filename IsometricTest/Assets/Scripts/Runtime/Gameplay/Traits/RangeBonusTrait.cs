using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Terrain trait that extends the attack range of the unit standing on it (e.g. a ranged unit on a
    /// hill seeing further). With <see cref="RangedOnly"/> set, only units whose base attack already
    /// reaches beyond melee get the bonus, so a swordsman on a hill does not suddenly outrange archers.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Terrain/Range Bonus")]
    public class RangeBonusTrait : TerrainTrait
    {
        [Tooltip("Extra tiles of attack range granted while a unit occupies this terrain.")]
        public int RangeBonus = 1;

        [Tooltip("When enabled, only units with a ranged base attack (base range greater than 1) get the bonus.")]
        public bool RangedOnly = true;

        public override int ModifyAttackRange(int range, RangeContext context)
        {
            if (RangedOnly && !context.IsRanged)
                return range;

            return range + RangeBonus;
        }
    }
}
