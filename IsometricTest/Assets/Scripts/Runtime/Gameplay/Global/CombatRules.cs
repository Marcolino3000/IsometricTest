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
        private static GameRules rules;

        /// <summary>
        /// Injected by the Initiator before anything can resolve combat.
        /// </summary>
        public static void Setup(GameRules gameRules)
        {
            rules = gameRules;

            if (rules == null)
                Debug.LogError("CombatRules got no GameRules asset - falling back to the built-in defaults.");
        }

        /// <summary>
        /// Never null: a missing asset yields a default-valued instance so combat still resolves.
        /// </summary>
        private static GameRules Rules
        {
            get
            {
                if (rules == null)
                    rules = ScriptableObject.CreateInstance<GameRules>();

                return rules;
            }
        }

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
        
        /// <summary>
        /// Whether <paramref name="defender"/> strikes back at <paramref name="attacker"/> after being hit:
        /// only if the match rules allow retaliation at all and the attacker is within the defender's
        /// effective range - so a ranged unit retaliating from a hill benefits from its terrain bonus.
        /// </summary>
        public static bool CanRetaliate(Unit defender, Unit attacker)
        {
            if (!Rules.RetaliationEnabled)
                return false;

            var distance = GetManhattanDistance(defender.CurrentState.Position, attacker.CurrentState.Position);

            return distance <= GetEffectiveAttackRange(defender);
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

        private static int GetManhattanDistance(Tile from, Tile to)
        {
            var dx = Mathf.Abs(from.Position.x - to.Position.x);
            var dy = Mathf.Abs(from.Position.y - to.Position.y);

            return dx + dy;
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
