using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Makes every step cost more until it wears off or is cured - the status counterpart to
    /// <see cref="MoveCostTrait"/>, and it needs no rule code of its own: <c>MovementRules</c> folds
    /// <see cref="Trait.ModifyMoveCost"/> for the route, the reachable-tile highlight, the threat
    /// overlay, the AI's plan and the bill alike, so a crippled unit is slowed in all five at once.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Status/Cripple")]
    public class CrippleTrait : StatusTrait
    {
        [Tooltip("Action points added to every step while it lasts.")]
        [Min(1)] public int ExtraMoveCost = 1;

        protected override string StatusSummary => $"Move cost {Signed(ExtraMoveCost)}";

        public override int ModifyMoveCost(int cost, MoveContext context)
        {
            return cost + ExtraMoveCost;
        }
    }
}
