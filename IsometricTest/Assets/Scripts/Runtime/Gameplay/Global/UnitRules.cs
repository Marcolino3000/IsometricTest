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
    /// Two sources go in and no view has to know there are two: the weapon it has drawn and the
    /// traits it carries. How far it walks is deliberately not one of them - the action point bar and
    /// the tiles it can reach say that already, and on a card it was a number beside the point. Derived on every call and
    /// never stored, so it needs no place in <c>GameSnapshot</c> and follows an undo for free - the
    /// same reason the match outcome is a question rather than a field. A new trait shows up in
    /// every view the moment it is worn, without any of them being touched.
    /// </summary>
    public static class UnitRules
    {
        /// <summary>
        /// Everything <paramref name="unit"/> can do, the weapon first, then one entry per trait it
        /// carries. Terrain traits are deliberately left out: they belong to the tile it happens to
        /// stand on rather than to the unit, and the card labelling that tile prints them.
        /// </summary>
        public static IReadOnlyList<Capability> GetCapabilities(Unit unit)
        {
            var capabilities = new List<Capability>();

            if (unit == null || unit.CurrentState == null)
                return capabilities;

            AddWeapon(capabilities, unit);
            AddTraits(capabilities, unit.CurrentState);

            return capabilities;
        }

        /// <summary>
        /// The weapon in hand, which is the attack action itself. Its name and nothing else: the
        /// numbers belong to the item and are read where the item is - on its slot, its find card,
        /// its entry in a picker - and repeating them here only made the longest line on the card.
        /// </summary>
        private static void AddWeapon(List<Capability> capabilities, Unit unit)
        {
            var weapon = unit.CurrentState.AttackAction;

            if (weapon == null)
                return;

            capabilities.Add(new Capability(weapon.Symbol, weapon.Title, null));
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
