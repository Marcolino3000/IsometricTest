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

            // The player-facing counterpart: whatever a trait wants shown beside the damage number.
            StrikeNotes.Begin();

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
        /// only if it is allowed to answer at all - the match rules, and then whatever
        /// <see cref="Trait.ModifyRetaliation"/> makes of them - and the attacker is within the
        /// defender's effective range, so a ranged unit retaliating from a hill benefits from its
        /// terrain bonus. Permission and reach stay separate questions: a trait grants the right to
        /// answer, never the reach to do it.
        /// </summary>
        public static bool CanRetaliate(Unit defender, Unit attacker)
        {
            // The counter-strike as it would be resolved - the defender answering, roles swapped -
            // so a trait sees itself on the same side here as it does while the damage is folded.
            var context = new CombatContext(defender, attacker, isRetaliation: true);

            var allowed = Rules.RetaliationEnabled;

            // Whoever would answer has the say: the traits they carry and the ones the ground grants.
            Trait decidedBy = null;

            foreach (var trait in TraitsAffecting(defender))
            {
                var modified = trait.ModifyRetaliation(allowed, context);

                if (modified == allowed)
                    continue;

                allowed = modified;
                decidedBy = trait;
            }

            // Only a refusal is logged: a counter-strike announces itself with its own line.
            if (!allowed)
            {
                CombatLog.Note(decidedBy != null
                    ? $"no retaliation ({decidedBy.name})"
                    : "no retaliation (turned off in GameRules)");

                return false;
            }

            // That there is one at all is worth a line only when a trait, rather than the rules, is
            // why - and then it is a detail, since the counter-strike itself is already reported.
            if (decidedBy != null && CombatLog.Details)
                CombatLog.Note($"retaliation granted by {decidedBy.name}");

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

            // Reach is not enough - the counter-strike still has to have somewhere to travel. Asked
            // after the distance so a shot that is both too far and blocked reports the shorter answer.
            if (!SightRules.HasClearLine(defender.CurrentState.Position, attacker.CurrentState.Position))
            {
                CombatLog.Note("no retaliation (no line of fire)");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether <paramref name="attacker"/> could strike <paramref name="targetTile"/> standing on
        /// <paramref name="fromTile"/>: close enough for its effective range, and with a clear line to
        /// it. One query rather than two tests spread about, because four things ask it and have to
        /// agree - the attack condition, the path that walks up to shoot, the threat overlay and the
        /// AI choosing a target.
        ///
        /// The line is the same one sight travels (<see cref="SightRules.HasClearLine"/>), so a shot
        /// goes exactly as far as the eye: an archer on a hill shoots over the hills and no further
        /// than the mountains. Melee needs no exception - adjacent tiles have nothing in between.
        /// </summary>
        public static bool CanAttackFrom(Unit attacker, Tile fromTile, Tile targetTile)
        {
            if (attacker == null || fromTile == null || targetTile == null)
                return false;

            if (fromTile.DistanceTo(targetTile) > GetEffectiveAttackRange(attacker, fromTile))
                return false;

            return SightRules.HasClearLine(fromTile, targetTile);
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
        /// the ones it carries, the ones the weapon in its hand grants, then the ones the ground
        /// grants. Public because it is not a combat question - <see cref="MovementRules"/> asks it
        /// too - and all of them have to fold the same set or a trait would apply to one and not the
        /// other.
        ///
        /// A weapon's traits are read off <see cref="UnitState.AttackAction"/> rather than copied
        /// onto the trait list when it is drawn, which is what keeps them free of bookkeeping: the
        /// weapon in the other hand is not the attack, so its traits are simply never asked; a swap
        /// carries them without anything being put on or taken off; every unit gets them, not only
        /// the one the player is equipping through <c>ItemManager</c>; and since nothing is stored,
        /// an undo puts them back with the weapon it puts back.
        /// </summary>
        public static IEnumerable<Trait> TraitsAffecting(UnitState state, Tile tile)
        {
            foreach (var trait in state.Traits)
                if (trait != null)
                    yield return trait;

            if (state.AttackAction != null)
                foreach (var trait in state.AttackAction.Traits)
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
