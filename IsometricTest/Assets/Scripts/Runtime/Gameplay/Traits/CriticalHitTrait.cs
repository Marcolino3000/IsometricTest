using Runtime.Gameplay.Global;
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
            // Rolled once whether or not it lands, so the log can report the roll that missed too.
            var roll = Random.value;

            if (roll >= CritChance)
            {
                // Names itself: a miss changes no number, so this note stands in for its whole line.
                // Guarded so the message is only built while the log is asked for that much detail.
                if (CombatLog.Details)
                    CombatLog.Detail($"{name}: no crit ({roll:0.00} vs {CritChance:0.00})");

                return damage;
            }

            if (CombatLog.Details)
                CombatLog.Detail($"crit! ({roll:0.00} vs {CritChance:0.00}, x{CritMultiplier})");

            return Mathf.RoundToInt(damage * CritMultiplier);
        }
    }
}
