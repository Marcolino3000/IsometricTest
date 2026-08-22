using System;
using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Items;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// The whole of merging, as a <see cref="CombatRules"/>-style query: pure functions over two
    /// items, no side effects and no state, shared by the merge screen (which draws the odds), the
    /// <see cref="ItemManager"/> (which pays them) and anything that later wants to ask. What the
    /// player is shown and what is actually rolled can never disagree, because they are the same call.
    ///
    /// A merge takes the traits off the item on the right and puts them on the weapon on the left,
    /// consuming the right one either way. It gets riskier the more the weapon already carries:
    /// the chance is <b>one over the number of traits the weapon would end up with</b>, so a bare
    /// weapon takes its first trait for certain, its second at even odds, its third at a third.
    /// That is one formula rather than a starting value and a penalty - "100% while it has none"
    /// falls out of it, since a merge that would leave one trait is one over one.
    ///
    /// Nothing here reaches for a random number. The roll belongs to whoever spends the items, so
    /// that the odds can be drawn as often as the cursor moves without deciding anything.
    /// </summary>
    public static class MergeRules
    {
        private const string NothingChosen = "Choose an item for both slots";
        private const string SameItem = "An item cannot be merged into itself";
        private const string NotAWeapon = "Only a weapon can be improved";
        private const string NotMaterial = "Only a weapon or a passive item can be merged in";
        private const string NoTraits = "That item carries no traits to pass on";

        /// <summary>
        /// The traits an item carries in its own right. Two kinds of item have any, and they keep
        /// them in two different places - a weapon grants its while it is drawn, a passive item
        /// while it is worn - so this is the one place that difference is bridged. An item with none
        /// answers an empty list rather than null, since every caller folds over it.
        /// </summary>
        public static IReadOnlyList<UnitTrait> TraitsOf(Item item)
        {
            return item switch
            {
                AttackActionData weapon => weapon.Traits,
                PassiveItem passive => passive.Traits,
                _ => Array.Empty<UnitTrait>()
            };
        }

        /// <summary>
        /// Whether an item can stand in the left slot - the one that is improved and survives. Only a
        /// weapon, because a weapon is the one thing whose traits are carried without being worn:
        /// <see cref="CombatRules.TraitsAffecting"/> reads them straight off the drawn weapon, so a
        /// merged one needs no bookkeeping anywhere to take effect.
        /// </summary>
        public static bool CanBeImproved(Item item)
        {
            return item is AttackActionData;
        }

        /// <summary>
        /// Whether an item can stand in the right slot - the one that is taken apart. A weapon or a
        /// passive item, and only one carrying something worth passing on: an item with no traits
        /// would be spent for nothing.
        ///
        /// An <see cref="Artefact"/> is a passive item and is deliberately turned away by the type
        /// test: it is worn for good and never given up, and the match can be won by collecting the
        /// set, so it is not material.
        /// </summary>
        public static bool CanBeConsumed(Item item)
        {
            if (item == null)
                return false;

            bool material = item is AttackActionData || (item is PassiveItem && item is not Artefact);

            return material && TraitsOf(item).Count > 0;
        }

        /// <summary>
        /// Whether these two can be merged, and what to tell the player when not. The one gate: the
        /// merge screen greys its button by it and the manager asks it again before spending
        /// anything, the way <see cref="ItemManager.CanTake"/> is asked twice.
        /// </summary>
        public static bool CanMerge(Item left, Item right, out string reason)
        {
            reason = string.Empty;

            if (left == null || right == null)
            {
                reason = NothingChosen;

                return false;
            }

            if (left == right)
            {
                reason = SameItem;

                return false;
            }

            if (!CanBeImproved(left))
            {
                reason = NotAWeapon;

                return false;
            }

            if (!CanBeConsumed(right))
            {
                reason = right is AttackActionData || right is PassiveItem ? NoTraits : NotMaterial;

                return false;
            }

            return true;
        }

        /// <summary>
        /// How many traits the weapon would end up with - what the odds are one over. Counted rather
        /// than reasoned about: duplicates are not weeded out, so passing on a trait the weapon
        /// already has both stacks it and makes the next merge harder, which is the same trade every
        /// merge is.
        /// </summary>
        public static int TraitCountAfterMerge(Item left, Item right)
        {
            return TraitsOf(left).Count + TraitsOf(right).Count;
        }

        /// <summary>
        /// The chance this merge succeeds, from 0 to 1 - one over
        /// <see cref="TraitCountAfterMerge"/>. A pair that cannot be merged at all has no odds and
        /// answers zero, so nothing has to guard the division.
        /// </summary>
        public static float SuccessChance(Item left, Item right)
        {
            if (!CanMerge(left, right, out _))
                return 0f;

            return 1f / TraitCountAfterMerge(left, right);
        }

        /// <summary>The same, as the whole percent the screen prints.</summary>
        public static int SuccessPercent(Item left, Item right)
        {
            return Mathf.RoundToInt(SuccessChance(left, right) * 100f);
        }

        /// <summary>
        /// The weapon this merge would produce: a runtime copy of <paramref name="left"/> carrying
        /// the traits of <paramref name="right"/> on top of its own.
        ///
        /// <b>A copy, never the asset.</b> A weapon is a <see cref="ScriptableObject"/> shared by
        /// every unit whose blueprint names it and by the loot table that hands it out; writing a
        /// trait into one would improve it for the enemy holding the same sword, and would survive
        /// the match into the project files. The copy is the same kind of runtime-only object as a
        /// cut icon sprite - hidden and never saved - and it is what the inventory then holds, so
        /// it travels through the history snapshots as an ordinary item reference and an undo puts
        /// the two originals back by itself.
        ///
        /// It is not put in the loot table either: the table is read off <c>Resources</c>, which a
        /// runtime copy is not in, and clearing <see cref="Item.FoundIn"/> says so rather than
        /// relying on that.
        /// </summary>
        public static AttackActionData Combine(Item left, Item right)
        {
            if (!CanMerge(left, right, out _))
                return null;

            var merged = UnityEngine.Object.Instantiate((AttackActionData)left);

            // Instantiate names the copy "<asset> (Clone)", which would show on the card and the
            // tooltip of any weapon that has no display name authored yet.
            merged.name = left.name;
            merged.hideFlags = HideFlags.HideAndDontSave;
            merged.FoundIn = null;

            // Instantiate gives the copy its own list of the same trait assets, so this adds to the
            // copy and never to the weapon it was made from.
            foreach (var trait in TraitsOf(right))
                if (trait != null)
                    merged.Traits.Add(trait);

            return merged;
        }
    }
}
