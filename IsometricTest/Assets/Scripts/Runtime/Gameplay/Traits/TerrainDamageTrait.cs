using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Unit trait that rewards attacking from a particular kind of ground: while the attacker stands on
    /// <see cref="Terrain"/> (flat by default) its outgoing damage is increased by <see cref="DamageBonus"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Terrain Damage Bonus")]
    public class TerrainDamageTrait : UnitTrait
    {
        [Tooltip("Terrain the attacker must be standing on for the bonus to apply.")]
        public TerrainType Terrain = TerrainType.Flat;

        [Tooltip("Extra damage added to each attack made from the chosen terrain.")]
        public int DamageBonus = 3;

        public override int ModifyOutgoingDamage(int damage, CombatContext context)
        {
            if (context.AttackerTile != null && context.AttackerTile.Terrain == Terrain)
                return damage + DamageBonus;

            return damage;
        }
    }
}
