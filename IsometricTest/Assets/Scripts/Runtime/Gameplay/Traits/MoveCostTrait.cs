using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Makes every step cheaper, wherever it leads - the untargeted counterpart to
    /// <see cref="TerrainMoveCostTrait"/>, which discounts one kind of ground.
    ///
    /// What it actually buys is rough ground: <see cref="Global.MovementRules"/> clamps a step at one
    /// action point, so flat ground is already as cheap as it can be and a discount cannot make it
    /// cheaper. A hill or a wood that asked for two points asks for one, which is why the summary
    /// says so rather than promising something off every step.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Move Discount")]
    public class MoveCostTrait : UnitTrait
    {
        [Tooltip("Action points knocked off every step. A step is never free - movement clamps the " +
                 "cost at one point - so this is felt on rough ground rather than on flat.")]
        [Min(0)] public int CostReduction = 1;

        public override string Summary => $"Move cost {Signed(-CostReduction)}";

        public override int ModifyMoveCost(int cost, MoveContext context)
        {
            return cost - CostReduction;
        }
    }
}
