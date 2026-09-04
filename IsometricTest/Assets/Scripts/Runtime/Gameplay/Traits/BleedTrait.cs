using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Takes health at the start of each of the carrier's turns until it wears off. Ignores defence
    /// and terrain on purpose: it is not a strike, so it goes through no <c>CombatRules</c> fold -
    /// armour keeps a blade out, it does not stop the wound it already made.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Status/Bleed")]
    public class BleedTrait : StatusTrait
    {
        [Tooltip("Health taken at the start of each of the carrier's turns. Not reduced by defence " +
                 "or terrain - a bleed is not a strike.")]
        [Min(1)] public int DamagePerTurn = 2;

        protected override string StatusSummary => $"{DamagePerTurn} damage per turn";

        public override void OnTurnBegan(TurnContext context)
        {
            context.Unit.TakeStatusDamage(DamagePerTurn, name);
        }
    }
}
