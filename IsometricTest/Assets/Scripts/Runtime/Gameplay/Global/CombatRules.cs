using Actions;
using System.Collections.Generic;
using Runtime.Core.Spawning;
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
        /// The board, for the one combat question that cannot be answered from its arguments alone:
        /// which tiles an area covers. Injected the way <see cref="SightRules"/>' spawner is; without
        /// one an area simply catches nobody rather than throwing.
        /// </summary>
        private static TileSpawner tiles;

        /// <summary>
        /// Injected by the Initiator before anything can resolve combat.
        /// </summary>
        public static void Setup(GameRules gameRules, TileSpawner tileSpawner)
        {
            rules = gameRules;
            tiles = tileSpawner;

            if (rules == null)
                Debug.LogError("CombatRules got no GameRules asset - falling back to the built-in defaults.");

            if (tiles == null)
                Debug.LogError("CombatRules got no TileSpawner - area effects will catch nobody.");
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
        /// What a weapon hits for before whoever carries it has a say - its damage effects added up.
        /// The one place that sum is taken, so the weapon card, the combat log and the strike itself
        /// all read the same base; a weapon with nothing authored hits for nothing rather than null.
        /// </summary>
        public static int BaseDamageOf(AttackActionData weapon)
        {
            if (weapon?.Effects == null)
                return 0;

            var damage = 0;

            // Only the effects aimed at whatever the attack was aimed at. One that names an area of
            // its own is a further hit on further units, so counting it here would put its damage on
            // the weapon card and into the blow that lands on the primary target.
            foreach (var effect in weapon.Effects)
                if (effect != null && !effect.HasOwnTargets)
                    damage += effect.Damage;

            return damage;
        }

        /// <summary>
        /// Damage a single strike deals once every trait has had its say: the attacker's traits shape the
        /// outgoing hit, then the defender's shape what actually lands. Never returns less than zero.
        /// </summary>
        public static int CalculateDamage(Unit attacker, Unit defender, bool isRetaliation = false)
        {
            return CalculateDamage(attacker, defender, BaseDamageOf(attacker.CurrentState.AttackAction),
                isRetaliation);
        }

        /// <summary>
        /// The same fold over a base the caller names rather than the weapon's own: what one of the
        /// weapon's area effects deals, which is its own number but everybody's traits. One fold for
        /// both, so defence, terrain and crits cannot apply to a blow and skip the spill beside it.
        /// </summary>
        public static int CalculateDamage(Unit attacker, Unit defender, int baseDamage, bool isRetaliation)
        {
            var context = new CombatContext(attacker, defender, isRetaliation);

            var damage = baseDamage;

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
        
        #region Area effects

        /// <summary>
        /// Every tile within <paramref name="radius"/> of <paramref name="centre"/>, itself included.
        /// Manhattan, like attack range and grid distance - enumerated off the spawner's circle,
        /// which the diamond fits inside, rather than by walking the grid again.
        /// </summary>
        public static IEnumerable<Tile> TilesWithin(Tile centre, int radius)
        {
            if (centre == null || tiles == null)
                yield break;

            foreach (var tile in tiles.GetTilesInSightRange(centre.Position, radius))
                if (centre.DistanceTo(tile) <= radius)
                    yield return tile;
        }

        /// <summary>
        /// The effects on the weapon in this unit's hand that name an area of their own - everything
        /// a swing does besides landing on what it was aimed at.
        /// </summary>
        public static IEnumerable<AttackEffect> AreaEffectsOf(Unit unit)
        {
            var weapon = unit != null ? unit.CurrentState.AttackAction : null;

            if (weapon?.Effects == null)
                yield break;

            foreach (var effect in weapon.Effects)
                if (effect != null && effect.HasOwnTargets)
                    yield return effect;
        }

        /// <summary>Whether a swing by this unit does anything beyond hitting what it is aimed at.</summary>
        public static bool HasAreaEffects(Unit unit)
        {
            foreach (var _ in AreaEffectsOf(unit))
                return true;

            return false;
        }

        /// <summary>
        /// Who a swing's area effects would catch, as the board stands right now. A pure query, so
        /// the resolver, the AI weighing a target and anything that previews an attack all read the
        /// same answer - and so it can be asked <i>before</i> the blow lands, which is what makes
        /// "already damaged" mean damaged when the swing started rather than damaged by the swing.
        /// </summary>
        public static List<EffectHit> PlanAreaEffects(Unit attacker, Unit defender, bool isRetaliation)
        {
            return PlanAreaEffects(attacker, null, defender, isRetaliation);
        }

        /// <summary>
        /// The same question asked of a swing made from <paramref name="fromTile"/> - what the AI
        /// wants, since it weighs attacks it would walk up to first and an area centred on the
        /// attacker moves with it.
        /// </summary>
        public static List<EffectHit> PlanAreaEffects(Unit attacker, Tile fromTile, Unit defender,
            bool isRetaliation)
        {
            var hits = new List<EffectHit>();

            if (attacker == null || !attacker.IsAlive || defender == null || !defender.IsAlive)
                return hits;

            var context = new EffectContext(attacker, defender, fromTile, null, isRetaliation);

            foreach (var effect in AreaEffectsOf(attacker))
            foreach (var victim in effect.ResolveTargets(context))
                hits.Add(new EffectHit(effect, victim));

            return hits;
        }

        /// <summary>
        /// The ground a swing's area effects cover, whoever happens to be standing on it - what the
        /// preview marks. Asked of the same selectors <see cref="PlanAreaEffects"/> takes its units
        /// from, so the tiles shown and the units hit describe one shape.
        ///
        /// Unlike the units, this needs no board state beyond the two tiles, so it answers while an
        /// attack is only being hovered.
        /// </summary>
        public static IEnumerable<Tile> AreaEffectTiles(Unit attacker, Tile fromTile, Unit defender)
        {
            if (attacker == null || !attacker.IsAlive || defender == null || !defender.IsAlive)
                yield break;

            var context = new EffectContext(attacker, defender, fromTile);

            // Two effects may well cover the same ground; a tile is marked once.
            var marked = new HashSet<Tile>();

            foreach (var effect in AreaEffectsOf(attacker))
            foreach (var tile in effect.Targets.ResolveTiles(context))
                if (marked.Add(tile))
                    yield return tile;
        }

        /// <summary>
        /// Which statuses a swing would put on which units. Asked of <b>every</b> effect the weapon
        /// carries, not only the ones naming an area: an effect with no targeting reaches the unit
        /// being struck, so a plain sword that wounds is one status on the one effect it already has.
        /// The effect's own conditions apply per candidate, which is what lets a weapon wound only
        /// what it has already hurt.
        ///
        /// A pure query over the board as it stands, like <see cref="PlanAreaEffects"/> - and asked
        /// at the same moment and for the same reason, before the blow lands, so a condition reads
        /// the board the swing started against. Whoever asked applies them, once the dead are known.
        /// </summary>
        public static List<StatusHit> PlanStatuses(Unit attacker, Unit defender, bool isRetaliation)
        {
            var hits = new List<StatusHit>();

            if (attacker == null || !attacker.IsAlive || defender == null || !defender.IsAlive)
                return hits;

            var weapon = attacker.CurrentState.AttackAction;

            if (weapon?.Effects == null)
                return hits;

            var context = new EffectContext(attacker, defender, null, null, isRetaliation);

            foreach (var effect in weapon.Effects)
            {
                if (effect == null || !effect.HasStatuses)
                    continue;

                foreach (var victim in effect.ResolveTargets(context))
                foreach (var status in effect.Applies)
                    if (status != null)
                        hits.Add(new StatusHit(status, victim));
            }

            return hits;
        }

        /// <summary>
        /// What one caught unit takes: the effect's own damage, folded through
        /// <see cref="CalculateDamage"/> so defence, terrain and the weapon's traits all still apply.
        ///
        /// Resolves a real strike - it rolls what a trait rolls and writes to the log - so it is for
        /// whoever is actually applying the damage. Anything merely weighing a swing reads
        /// <see cref="AttackEffect.Damage"/> instead.
        /// </summary>
        public static int AreaDamage(AttackEffect effect, Unit attacker, Unit victim, bool isRetaliation)
        {
            if (effect == null || attacker == null || victim == null)
                return 0;

            return CalculateDamage(attacker, victim, effect.Damage, isRetaliation);
        }

        #endregion

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

    /// <summary>
    /// One unit an <see cref="AttackEffect"/> caught, paired with the effect that caught it. Planned
    /// before the blow lands and applied after, so the pair has to be carried rather than re-derived
    /// from a board that has meanwhile changed.
    /// </summary>
    /// <summary>
    /// One status a swing would put on one unit. The <b>asset</b> rather than an instance of it -
    /// making the copy is the unit's business, since re-applying a status somebody already carries
    /// refreshes it rather than adding a second.
    /// </summary>
    public readonly struct StatusHit
    {
        public readonly StatusTrait Status;
        public readonly Unit Victim;

        public StatusHit(StatusTrait status, Unit victim)
        {
            Status = status;
            Victim = victim;
        }
    }

    public readonly struct EffectHit
    {
        public readonly AttackEffect Effect;
        public readonly Unit Victim;

        public EffectHit(AttackEffect effect, Unit victim)
        {
            Effect = effect;
            Victim = victim;
        }
    }
}
