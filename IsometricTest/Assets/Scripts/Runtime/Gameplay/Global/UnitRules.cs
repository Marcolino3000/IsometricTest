using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// One thing a unit can do, in the form the views draw it: a symbol where the asset carries one,
    /// a short label to fall back on where it does not, and one line saying what it amounts to.
    ///
    /// The badges show <see cref="Icon"/> or, failing that, <see cref="Label"/>; the card shows the
    /// label and the detail. Neither knows where any of it came from, the same way
    /// <c>ItemOption</c> and <c>ItemCard</c> are handed strings and a sprite and told nothing about
    /// items.
    /// </summary>
    public readonly struct Capability
    {
        /// <summary>The asset's own symbol, or null where nothing is authored.</summary>
        public readonly Sprite Icon;

        /// <summary>Two or three words - what a badge with no symbol reads as.</summary>
        public readonly string Label;

        /// <summary>One line of explanation, with the numbers in it.</summary>
        public readonly string Detail;

        public Capability(Sprite icon, string label, string detail)
        {
            Icon = icon;
            Label = label;
            Detail = detail;
        }
    }

    /// <summary>
    /// What a unit is able to do, folded into one list - the <see cref="CombatRules"/>-style query
    /// behind everything that shows a unit's capabilities: the badges over its head, the card shown
    /// while it is hovered, and anything added later.
    ///
    /// Three sources go in and no view has to know there are three: the weapon it has drawn, the
    /// action points it starts a turn with, and the traits it carries. Derived on every call and
    /// never stored, so it needs no place in <c>GameSnapshot</c> and follows an undo for free - the
    /// same reason the match outcome is a question rather than a field. A new trait shows up in
    /// every view the moment it is worn, without any of them being touched.
    /// </summary>
    public static class UnitRules
    {
        /// <summary>
        /// Everything <paramref name="unit"/> can do, weapon first, then how far it gets, then one
        /// entry per trait it carries. Terrain traits are deliberately left out: they belong to the
        /// tile it happens to stand on rather than to the unit, and what they are worth right now is
        /// already in the weapon's effective reach.
        /// </summary>
        public static IReadOnlyList<Capability> GetCapabilities(Unit unit)
        {
            var capabilities = new List<Capability>();

            if (unit == null || unit.CurrentState == null)
                return capabilities;

            AddWeapon(capabilities, unit);
            AddMobility(capabilities, unit);
            AddTraits(capabilities, unit.CurrentState);

            return capabilities;
        }

        /// <summary>
        /// The weapon in hand, which is the attack action itself - so the numbers are the ones the
        /// asset already reports to the find popup, and there is nothing to keep in step. What it
        /// actually reaches from where the unit stands is added only when the ground or the gear has
        /// moved it, since that is the part the authored range does not tell you.
        /// </summary>
        private static void AddWeapon(List<Capability> capabilities, Unit unit)
        {
            var weapon = unit.CurrentState.AttackAction;

            if (weapon == null)
                return;

            var detail = $"{weapon.Title}: {string.Join(", ", weapon.Stats)}";

            var tile = unit.CurrentState.Position;

            if (tile != null && weapon.Condition != null)
            {
                var effective = CombatRules.GetEffectiveAttackRange(unit, tile);

                if (effective != weapon.Condition.Range)
                    detail += $" - reaches {effective} from here";
            }

            capabilities.Add(new Capability(weapon.Symbol, weapon.Kind.ToString(), detail));
        }

        /// <summary>
        /// How far it gets in one turn, measured from the points it *starts* a turn with rather than
        /// the ones it has left: a unit that spent its turn is not a slow unit, and what the player
        /// is judging is what it will do next. Level ground, since a step onto rough terrain costs
        /// more - <see cref="MovementRules"/> is what actually charges for a route.
        /// </summary>
        private static void AddMobility(List<Capability> capabilities, Unit unit)
        {
            var move = unit.CurrentState.MoveAction;

            if (move == null || move.Condition == null || move.Condition.Cost <= 0)
                return;

            var points = unit.MaxActionPoints;
            var tiles = points / move.Condition.Cost;

            capabilities.Add(new Capability(move.Symbol, $"{tiles} tiles",
                $"Moves {tiles} tiles over level ground ({points} AP, {move.Condition.Cost} per step)"));
        }

        /// <summary>
        /// One entry per trait carried, which each describes itself - <see cref="Traits.Trait.Summary"/>
        /// is the same line a passive item reports when it is found.
        /// </summary>
        private static void AddTraits(List<Capability> capabilities, UnitState state)
        {
            foreach (var trait in state.Traits)
            {
                if (trait == null)
                    continue;

                capabilities.Add(new Capability(trait.Icon, trait.name, trait.Summary));
            }
        }
    }
}
