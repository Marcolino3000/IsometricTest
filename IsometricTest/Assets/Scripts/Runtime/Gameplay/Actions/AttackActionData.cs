using System.Collections.Generic;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.Items;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Actions
{
    [CreateAssetMenu(menuName = "Actions/Attack")]
    public class AttackActionData : ActionData<AttackCondition>
    {
        [Tooltip("Which item slot the weapon is offered in. Authored rather than read off the range: " +
                 "traits change the effective range, the category must not change with them.")]
        public WeaponKind Kind;

        [Tooltip("Traits granted to whoever has this weapon drawn - a sword that crits, a bow that " +
                 "sees further. Only while it is drawn: the weapon in the other hand grants nothing. " +
                 "Leave the weapon requirement on an EquipmentTrait at Any here, since a trait on a " +
                 "weapon is only ever asked while that weapon is the one in hand.")]
        public List<UnitTrait> Traits = new();

        /// <summary>
        /// What a swing does. A list because a swing does more than one thing: the effects that name
        /// no targeting of their own are the blow itself, folded into one number by
        /// <see cref="CombatRules.BaseDamageOf"/> so the card, the log and the strike can never
        /// disagree about what a weapon hits for; the ones that do name an area are further hits on
        /// further units, resolved separately by <c>CombatRunner</c>. Which of the two an effect is,
        /// is the effect's own business - see <see cref="ActionEffect"/>.
        /// </summary>
        public IReadOnlyList<AttackEffect> Effects => effects;

        [Tooltip("What a swing does. The entries that name no targeting are the blow itself and " +
                 "their damage is summed; one that names an area is a further hit beside it.")]
        [SerializeReference] private List<AttackEffect> effects = new();

        // Weapons are the only items whose category is authored as a kind of its own, so that a
        // weapon asset can only ever be tagged melee or ranged - never active or passive.
        public override SlotKind Slot => Kind == WeaponKind.Melee ? SlotKind.Melee : SlotKind.Ranged;

        /// <summary>
        /// The weapon as authored - what it hits for, how far it reaches, what a swing costs, and
        /// then whatever its <see cref="Traits"/> add. Traits move the first three, so those are the
        /// numbers before anything the carrier or the ground adds; the trait lines are what this
        /// weapon itself is beyond them.
        /// </summary>
        public override IReadOnlyList<string> Stats
        {
            get
            {
                var stats = new List<string>();

                stats.Add($"Damage {CombatRules.BaseDamageOf(this)}");

                if (Condition != null)
                {
                    stats.Add($"Range {Condition.Range}");
                    stats.Add($"Cost {Condition.Cost} AP");
                }

                // Anything the swing does beyond the blow says so in its own words: the effect knows
                // who it reaches and under what conditions, so nothing here has to take it apart.
                // An effect aimed at nothing in particular is already counted in the damage above,
                // so it earns a line only when it does something besides deal it.
                foreach (var effect in effects)
                    if (effect != null && (effect.HasOwnTargets || effect.HasStatuses))
                        stats.Add(effect.Summary);

                // Reported here rather than badged separately: a weapon's traits belong to the
                // weapon, so they read on its card, its tooltip and its capability line at once.
                foreach (var trait in Traits)
                {
                    var summary = trait != null ? trait.Summary : null;

                    if (!string.IsNullOrWhiteSpace(summary))
                        stats.Add(summary);
                }

                return stats;
            }
        }

        public override IUnitAction CreateAction(ActionContext context)
        {
            return new AttackAction(Condition, context);
        }
    }

    /// <summary>The category a weapon belongs to, which is the item slot it is carried in.</summary>
    public enum WeaponKind
    {
        Melee,
        Ranged
    }
}
