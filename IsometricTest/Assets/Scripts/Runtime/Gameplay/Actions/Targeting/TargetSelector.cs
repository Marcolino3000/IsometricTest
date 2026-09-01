using System;
using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Actions
{
    /// <summary>
    /// Who an <see cref="ActionEffect"/> reaches. Authored inline on the effect as a
    /// <c>[SerializeReference]</c>, so a renamed or moved subclass needs <c>[MovedFrom]</c>.
    ///
    /// <b>Left empty, the effect applies to what the action was aimed at</b> - the unit being struck,
    /// the character drinking the potion - which is what every effect authored so far wants and why
    /// none of them had to say anything.
    ///
    /// <b>This is the one place "who is affected" is answered, and it is meant to stay the one
    /// place.</b> An effect resolves the set and applies to all of it; a player aiming an action
    /// would highlight the same set and accept a click inside it. Two hierarchies for the two would
    /// drift the way a precomputed distance drifted from the tile the range was read off.
    ///
    /// Pure: it reads the board and returns units. Whoever asked applies the effect.
    /// </summary>
    [Serializable]
    public abstract class TargetSelector
    {
        /// <summary>
        /// Every unit this selector reaches for <paramref name="context"/>, as the board stands when
        /// it is asked. Never returns a unit that is not in play.
        /// </summary>
        public abstract IEnumerable<Unit> Resolve(EffectContext context);

        /// <summary>
        /// The ground this selector covers, whoever is standing on it. What the preview draws, and
        /// the shape <see cref="Resolve"/> then picks its units out of - asked of the selector rather
        /// than worked out beside it, so the tiles marked and the units hit can never disagree about
        /// where the effect reaches.
        ///
        /// Abstract on purpose: a selector that cannot say where it reaches cannot be previewed, and
        /// a shape nobody can see coming is not one to author.
        /// </summary>
        public abstract IEnumerable<Tile> ResolveTiles(EffectContext context);

        /// <summary>One short line naming the set, for the card and the tooltip.</summary>
        public abstract string Summary { get; }
    }

    /// <summary>Whose side of the action a selector picks from, seen from its source.</summary>
    public enum AffectedSide
    {
        Enemies,
        Allies,
        Everyone
    }

    /// <summary>What an area is drawn around.</summary>
    public enum AreaCentre
    {
        /// <summary>What the action was aimed at - a blast where the blow lands.</summary>
        Target,

        /// <summary>Whoever the action comes from - a cleave around the one swinging.</summary>
        Source
    }

    /// <summary>
    /// Everyone standing within a radius of the action. One class rather than two because a cleave
    /// around the attacker and a blast around its target differ only in what they are centred on.
    ///
    /// Distance is <b>Manhattan</b>, like every other reach in the game (attack range, grid
    /// distance), so "within 1" beside "range 3" means the same kind of tile in both.
    /// </summary>
    [Serializable]
    public class AreaSelector : TargetSelector
    {
        [Tooltip("What the area is drawn around: what the action was aimed at, or whoever it comes from.")]
        public AreaCentre Centre = AreaCentre.Target;

        [Tooltip("How far the area reaches, in tiles (Manhattan, like attack range).")]
        [Min(1)] public int Radius = 1;

        [Tooltip("Whose side is caught, seen from the source.")]
        public AffectedSide Side = AffectedSide.Enemies;

        [Tooltip("Whether what the action was aimed at is caught as well. Off by default: an attack's " +
                 "own target already takes the effects that name no area.")]
        public bool IncludeTarget;

        [Tooltip("Whether the source catches its own area. Off by default - a melee attacker stands " +
                 "inside a radius drawn around its target.")]
        public bool IncludeSource;

        [Tooltip("Whether the area stops at what blocks a shot. On, a mountain between the centre " +
                 "and a unit keeps that unit out of it.")]
        public bool RequireLineOfFire = true;

        public override string Summary
        {
            get
            {
                var who = Side switch
                {
                    AffectedSide.Enemies => "enemies",
                    AffectedSide.Allies => "allies",
                    _ => "units"
                };

                var where = Centre == AreaCentre.Target ? "the target" : "the user";

                return $"{who} within {Radius} of {where}";
            }
        }

        public override IEnumerable<Tile> ResolveTiles(EffectContext context)
        {
            var centre = Centre == AreaCentre.Target ? context.TargetTile : context.SourceTile;

            if (centre == null)
                yield break;

            foreach (var tile in CombatRules.TilesWithin(centre, Radius))
            {
                // The same line a shot travels, so an area no arrow could reach does not spill
                // through a mountain either.
                if (RequireLineOfFire && !SightRules.HasClearLine(centre, tile))
                    continue;

                yield return tile;
            }
        }

        public override IEnumerable<Unit> Resolve(EffectContext context)
        {
            if (context.Source == null)
                yield break;

            // The shape is asked for once and picked over here, so the tiles the preview marks are
            // exactly the tiles the units are taken from.
            foreach (var tile in ResolveTiles(context))
            {
                var candidate = tile.Unit;

                if (candidate == null || !candidate.IsAlive)
                    continue;

                if (candidate == context.Target && !IncludeTarget)
                    continue;

                if (candidate == context.Source && !IncludeSource)
                    continue;

                if (!IsOnAffectedSide(candidate, context.Source))
                    continue;

                yield return candidate;
            }
        }

        private bool IsOnAffectedSide(Unit candidate, Unit source)
        {
            if (Side == AffectedSide.Everyone)
                return true;

            var sameTeam = candidate.CurrentState.Team == source.CurrentState.Team;

            return Side == AffectedSide.Allies ? sameTeam : !sameTeam;
        }
    }
}
