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
        /// Damage a single strike deals once every trait has had its say: the attacker's traits shape the
        /// outgoing hit, then the defender's shape what actually lands. Never returns less than zero.
        /// </summary>
        public static int CalculateDamage(Unit attacker, Unit defender, bool isRetaliation = false)
        {
            var context = new CombatContext(attacker, defender, isRetaliation);

            var damage = attacker.CurrentState.AttackAction.Effect.Damage;

            foreach (var trait in TraitsAffecting(attacker))
                damage = trait.ModifyOutgoingDamage(damage, context);

            foreach (var trait in TraitsAffecting(defender))
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

            var distance = defender.CurrentState.Position.DistanceTo(attacker.CurrentState.Position);

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

            foreach (var trait in TraitsAffecting(unit, fromTile))
                range = trait.ModifyAttackRange(range, context);

            return range;
        }
        
        private static IEnumerable<Trait> TraitsAffecting(Unit unit)
        {
            return TraitsAffecting(unit, unit.CurrentState.Position);
        }
        
        private static IEnumerable<Trait> TraitsAffecting(Unit unit, Tile tile)
        {
            foreach (var trait in unit.CurrentState.Traits)
                if (trait != null)
                    yield return trait;

            if (tile == null)
                Debug.LogError("Unit does not have a tile/position assigned!");

            foreach (var trait in tile.Traits)
                if (trait != null)
                    yield return trait;
        }
    }
}
