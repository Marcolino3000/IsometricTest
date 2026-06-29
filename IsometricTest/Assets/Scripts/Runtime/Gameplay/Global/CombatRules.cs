using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Pure combat math shared by the combat resolver, the attack action and the action planner. Centralises
    /// how unit and terrain <see cref="Trait"/>s fold into a single strike's damage and into a unit's effective
    /// attack range.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>
        /// Damage a single strike deals once every trait has had its say (terrain, attacker, defender). Never returns less than zero.
        /// </summary>
        public static int CalculateDamage(Unit attacker, Unit defender, bool isRetaliation = false)
        {
            var context = new CombatContext(attacker, defender, isRetaliation);

            var damage = attacker.CurrentState.AttackAction.Effect.Damage;

            foreach (var trait in AttackerTraits(attacker))
                damage = trait.ModifyOutgoingDamage(damage, context);

            foreach (var trait in DefenderTraits(defender))
                damage = trait.ModifyIncomingDamage(damage, context);

            return Mathf.Max(0, damage);
        }
        
        public static int GetEffectiveAttackRange(Unit unit)
        {
            return GetEffectiveAttackRange(unit, unit.CurrentState.Position);
        }

        /// <summary>
        /// A unit's effective attack range as if it were standing on <paramref name="fromTile"/>.
        /// </summary>
        public static int GetEffectiveAttackRange(Unit unit, Tile fromTile)
        {
            var baseRange = unit.CurrentState.AttackAction.Condition.Range;

            var context = new RangeContext(unit, fromTile, baseRange);

            var range = baseRange;

            foreach (var trait in TerrainTraits(fromTile))
                range = trait.ModifyAttackRange(range, context);

            foreach (var trait in UnitTraits(unit))
                range = trait.ModifyAttackRange(range, context);

            return range;
        }

        private static IEnumerable<Trait> AttackerTraits(Unit unit)
        {
            foreach (var trait in UnitTraits(unit))
                yield return trait;

            foreach (var trait in TerrainTraits(unit.CurrentState.Position))
                yield return trait;
        }

        private static IEnumerable<Trait> DefenderTraits(Unit unit)
        {
            foreach (var trait in UnitTraits(unit))
                yield return trait;

            foreach (var trait in TerrainTraits(unit.CurrentState.Position))
                yield return trait;
        }

        private static IEnumerable<UnitTrait> UnitTraits(Unit unit)
        {
            var traits = unit.CurrentState.Traits;
            if (traits == null)
                yield break;

            foreach (var trait in traits)
                if (trait != null)
                    yield return trait;
        }

        private static IEnumerable<TerrainTrait> TerrainTraits(Tile tile)
        {
            if (tile == null || tile.Traits == null)
                yield break;

            foreach (var trait in tile.Traits)
                if (trait != null)
                    yield return trait;
        }
    }
}
