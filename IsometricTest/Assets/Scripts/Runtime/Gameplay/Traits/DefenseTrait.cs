using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Terrain trait that shields the occupying unit: subtracts a flat amount from every incoming hit
    /// (e.g. hills giving cover). The combat resolver clamps damage at zero, so over-shielding simply
    /// negates the hit rather than healing.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Terrain/Defense Bonus")]
    public class DefenseTrait : TerrainTrait
    {
        [Tooltip("Flat amount subtracted from each incoming attack while a unit occupies this terrain.")]
        public int DefenseBonus = 2;

        public override int ModifyIncomingDamage(int damage, CombatContext context)
        {
            return damage - DefenseBonus;
        }
    }
}
