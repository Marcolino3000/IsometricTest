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

            // The fold is written step by step rather than in place so CombatLog can report what each
            // trait did to the number. It does nothing at all while logging is off.
            CombatLog.BeginStrike(context, damage);

            foreach (var trait in TraitsAffecting(attacker))
            {
                var modified = trait.ModifyOutgoingDamage(damage, context);
                CombatLog.Modifier(trait, outgoing: true, damage, modified);
                damage = modified;
            }

            foreach (var trait in TraitsAffecting(defender))
            {
                var modified = trait.ModifyIncomingDamage(damage, context);
                CombatLog.Modifier(trait, outgoing: false, damage, modified);
                damage = modified;
            }

            var final = Mathf.Max(0, damage);

            CombatLog.EndStrike(damage, final);

            return final;
        }

        /// <summary>
        /// Whether <paramref name="defender"/> strikes back at <paramref name="attacker"/> after being hit:
        /// only if the match rules allow retaliation at all and the attacker is within the defender's
        /// effective range - so a ranged unit retaliating from a hill benefits from its terrain bonus.
        /// </summary>
        public static bool CanRetaliate(Unit defender, Unit attacker)
        {
            // Only a refusal is logged: a counter-strike announces itself with its own line.
            if (!Rules.RetaliationEnabled)
            {
                CombatLog.Note("no retaliation (turned off in GameRules)");
                return false;
            }

            var distance = defender.CurrentState.Position.DistanceTo(attacker.CurrentState.Position);
            var range = GetEffectiveAttackRange(defender);

            if (distance > range)
            {
                // The refusal explains a damage number that never happened, so it is logged either
                // way; how far out of reach it was is detail.
                CombatLog.Note(CombatLog.Details
                    ? $"no retaliation (distance {distance} > range {range})"
                    : "no retaliation (out of range)");

                return false;
            }

            return true;
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
            return TraitsAffecting(unit.CurrentState, tile);
        }

        /// <summary>
        /// Every trait that has a say about <paramref name="state"/> standing on <paramref name="tile"/>:
        /// the ones it carries, then the ones the ground grants. Public because it is not a combat
        /// question - <see cref="MovementRules"/> asks it too - and both have to fold the same set or
        /// a trait would apply to one and not the other.
        /// </summary>
        public static IEnumerable<Trait> TraitsAffecting(UnitState state, Tile tile)
        {
            foreach (var trait in state.Traits)
                if (trait != null)
                    yield return trait;

            if (tile == null)
            {
                Debug.LogError("Unit does not have a tile/position assigned!");
                yield break;
            }

            foreach (var trait in tile.Traits)
                if (trait != null)
                    yield return trait;
        }
    }
}
