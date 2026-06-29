using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Critical Hit")]
    public class CriticalHitTrait : UnitTrait
    {
        [Tooltip("Probability that an attack crits (0 = never, 1 = always).")]
        [Range(0f, 1f)] public float CritChance = 0.25f;

        [Tooltip("Damage multiplier applied on a critical hit.")]
        [Min(1f)] public float CritMultiplier = 2f;

        public override int ModifyOutgoingDamage(int damage, CombatContext context)
        {
            if (Random.value >= CritChance)
                return damage;

            var crit = Mathf.RoundToInt(damage * CritMultiplier);
            Debug.Log($"{(context.Attacker != null ? context.Attacker.name : "Unit")} landed a critical hit! {damage} -> {crit}");
            return crit;
        }
    }
}
