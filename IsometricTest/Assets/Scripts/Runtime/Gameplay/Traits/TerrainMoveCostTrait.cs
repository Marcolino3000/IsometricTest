using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Makes one kind of ground cheaper to walk onto - boots that shrug off a climb. Mirrors
    /// <see cref="TerrainDamageTrait"/>, which rewards fighting from a terrain rather than entering it.
    ///
    /// A discount can never make a step free: <see cref="Global.MovementRules"/> clamps it, so
    /// reducing hills by more than they cost simply makes them as cheap as flat ground.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Terrain Move Discount")]
    public class TerrainMoveCostTrait : UnitTrait
    {
        [Tooltip("Terrain the discount applies to when stepping onto it.")]
        public TerrainType Terrain = TerrainType.Hills;

        [Tooltip("Action points knocked off the cost of entering that terrain.")]
        [Min(0)] public int CostReduction = 1;

        public override string Summary => $"-{CostReduction} AP to enter {Terrain}";

        public override int ModifyMoveCost(int cost, MoveContext context)
        {
            if (context.Tile != null && context.Tile.Terrain == Terrain)
                return cost - CostReduction;

            return cost;
        }
    }
}
