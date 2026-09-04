using System.Collections.Generic;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
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

        /// <summary>
        /// How many of this the unit has - a status stacked three deep, or a trait two things grant
        /// at once. One for everything that occurs once, which is most of it. Counted here rather
        /// than drawn twice because every copy is folded into the rules alike, so what is worth
        /// saying is how many, not the same line over again.
        /// </summary>
        public readonly int Count;

        public Capability(Sprite icon, string label, string detail, int count = 1)
        {
            Icon = icon;
            Label = label;
            Detail = detail;
            Count = Mathf.Max(1, count);
        }

        /// <summary>
        /// The label with the count on it where there is more than one. The one place a count is
        /// worded, so the badge over a unit's head and the card labelling it read the same.
        /// </summary>
        public string Title => Count > 1 ? $"{Label} ×{Count}" : Label;
    }

    /// <summary>
    /// One trait a unit carries and how many copies of it - what <see cref="UnitRules.GetTraits"/>
    /// yields. The trait itself rather than what it looks like, so whoever draws it can also ask it
    /// what to say.
    /// </summary>
    public readonly struct TraitCount
    {
        public readonly Trait Trait;
        public readonly int Count;

        /// <summary>
        /// Whether <b>every</b> copy of it comes off the weapon in hand rather than off the unit -
        /// which is what makes it go away again when the other weapon is drawn, and worth saying on
        /// the card. All of them rather than any, because a trait the unit also carries survives a
        /// swap and is not the weapon's to claim.
        /// </summary>
        public readonly bool FromWeapon;

        public TraitCount(Trait trait, int count, bool fromWeapon = false)
        {
            Trait = trait;
            Count = count;
            FromWeapon = fromWeapon;
        }

        /// <summary>The same trait, one copy deeper - and still the weapon's only if that one is too.</summary>
        public TraitCount AndOneMore(bool fromWeapon = false) => new(Trait, Count + 1, FromWeapon && fromWeapon);
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
        /// Everything <paramref name="unit"/> can do, the weapon first, then one entry per trait -
        /// the ones it carries and the ones the weapon in hand grants. Terrain traits are deliberately left out: they belong to the tile it happens to
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
        /// What the unit carries, folded so that two entries describing the same thing are one entry
        /// with a count. Its own door beside <see cref="GetCapabilities"/> because the HUD's trait
        /// row wants the traits and not the weapon - and it hands over the <see cref="Trait"/> rather
        /// than a <see cref="Capability"/>, since a trait describes itself and a view drawing one
        /// should ask it rather than be handed three of its fields.
        ///
        /// <b>Counting rather than repeating is not cosmetic.</b> A status stacked three deep really
        /// is three copies on the list - that is what makes every rule query fold it three times
        /// without knowing statuses exist - and a trait a blueprint grants that a worn item grants
        /// again is two copies for the same reason. Both fold more than once, so what is worth
        /// saying is how many, never the same line over again.
        /// </summary>
        public static IReadOnlyList<TraitCount> GetTraits(UnitState state)
        {
            var traits = new List<TraitCount>();

            if (state?.Traits == null)
                return traits;

            // Where in the list each key's entry ended up, so a later copy finds the one to count up.
            var at = new Dictionary<Trait, int>();

            // In the order CombatRules.TraitsAffecting folds them, so the row is read the way the
            // rules read it: what the unit carries, then what the weapon in hand grants. The ground
            // it happens to stand on is deliberately not here - that belongs to the tile, and the
            // card labelling the tile prints it.
            Fold(state.Traits, false);

            if (state.AttackAction != null)
                Fold(state.AttackAction.Traits, true);

            return traits;

            void Fold(IReadOnlyList<UnitTrait> carried, bool fromWeapon)
            {
                foreach (var trait in carried)
                {
                    if (trait == null)
                        continue;

                    // Copies of one status are distinct objects, so what they have in common is the
                    // asset they were made from - the same thing a cure names them by.
                    var key = trait is StatusTrait status ? status.Source : trait;

                    if (at.TryGetValue(key, out var index))
                    {
                        traits[index] = traits[index].AndOneMore(fromWeapon);
                        continue;
                    }

                    at[key] = traits.Count;
                    traits.Add(new TraitCount(trait, 1, fromWeapon));
                }
            }
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
            foreach (var carried in GetTraits(state))
                capabilities.Add(new Capability(carried.Trait.Icon, carried.Trait.name,
                    carried.Trait.Summary, carried.Count));
        }
    }
}
