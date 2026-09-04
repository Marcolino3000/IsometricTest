using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Actions
{
    /// <summary>
    /// Damage a swing deals. Its targeting is inherited from <see cref="ActionEffect"/>: left empty
    /// it is the blow itself, aimed at whatever the attack was aimed at and summed with the weapon's
    /// other such effects by <c>CombatRules.BaseDamageOf</c>. Given a selector it is a second thing
    /// the same swing does - to units around the target, around the attacker, under conditions of its
    /// own - resolved as a hit of its own through the same damage rules.
    /// </summary>
    [System.Serializable]
    public class AttackEffect : ActionEffect
    {
        [Tooltip("Damage dealt to each unit this effect reaches, before traits and terrain have " +
                 "their say.")]
        public int Damage;

        [Tooltip("Statuses put on every unit this effect reaches - a bleed, a crippled leg. Applied " +
                 "to whoever survives the blow, and through the effect's own targeting and " +
                 "conditions, so a weapon can wound only what it has already hurt.")]
        public List<StatusTrait> Applies = new();

        [Tooltip("Shown beside the damage number over a unit this effect hit - worth setting on one " +
                 "that names an area, since a number appearing on somebody who was never attacked " +
                 "does not explain itself. Empty shows nothing.")]
        public string Note;

        /// <summary>Whether this effect does anything besides its damage.</summary>
        public bool HasStatuses => Applies != null && Applies.Any(status => status != null);

        /// <summary>One short line for the weapon card and its tooltip.</summary>
        public string Summary
        {
            get
            {
                var line = $"{Damage} damage";

                if (HasStatuses)
                {
                    var names = Applies.Where(status => status != null).Select(status => status.name);

                    line += $", inflicts {string.Join(", ", names)}";
                }

                return line + TargetSummary;
            }
        }
    }
}
