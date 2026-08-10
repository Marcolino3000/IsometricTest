using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Entities;
using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>What the carrier has to be holding for a trait's bonuses to count.</summary>
    public enum WeaponRequirement
    {
        /// <summary>Always applies, whatever is in hand.</summary>
        Any,
        Melee,
        Ranged
    }

    /// <summary>
    /// The one place a <see cref="WeaponRequirement"/> is answered and named. Gear that hangs on a
    /// weapon is authored on more than one trait (<see cref="EquipmentTrait"/> for its numbers,
    /// <see cref="RetaliationTrait"/> for its counter-strike), and all of them have to read the
    /// requirement the same way or the same wording would mean two things.
    /// </summary>
    public static class WeaponRequirements
    {
        /// <summary>
        /// Whether <paramref name="carrier"/> currently has the required weapon drawn. Which unit
        /// carries the trait differs per hook - the attacker deals damage, the defender takes it -
        /// so it is passed in rather than guessed.
        /// </summary>
        public static bool IsMetBy(this WeaponRequirement requirement, Unit carrier)
        {
            if (requirement == WeaponRequirement.Any)
                return true;

            var weapon = carrier != null ? carrier.CurrentState.AttackAction : null;

            if (weapon == null)
                return false;

            return requirement == WeaponRequirement.Melee
                ? weapon.Kind == WeaponKind.Melee
                : weapon.Kind == WeaponKind.Ranged;
        }

        /// <summary>
        /// The requirement as a suffix for a trait's <see cref="Trait.Summary"/> - empty when there
        /// is none, and never left off when there is, since it is the whole point of gear that has one.
        /// </summary>
        public static string Describe(this WeaponRequirement requirement)
        {
            return requirement == WeaponRequirement.Any
                ? ""
                : $" (with a {requirement.ToString().ToLowerInvariant()} weapon)";
        }
    }

    /// <summary>
    /// The flat modifiers a piece of equipment grants whoever carries it: one number per hook a
    /// <see cref="Trait"/> has, so a single asset covers "+3 damage", "+2 defence", "+1 reach" or any
    /// combination of them. One class rather than three because a piece of gear usually changes more
    /// than one of those numbers and each field maps to exactly one hook - there is nothing to
    /// disentangle, and a sword that also extends reach is one asset instead of two.
    ///
    /// <see cref="RequiresWeapon"/> is where item *combinations* live. Only one passive can be worn,
    /// so a passive cannot pay off another passive - but it can pay off the weapon that is drawn, and
    /// that is a real choice, because the weapon slots keep both and swapping is free. Gear that only
    /// works with a bow simply says so here rather than needing a class of its own.
    ///
    /// The terrain equivalents (<see cref="DefenseTrait"/>, <see cref="RangeBonusTrait"/>) stay their
    /// own classes: they belong to the <see cref="TerrainTrait"/> branch and carry terrain-only
    /// authoring (<see cref="RangeBonusTrait.RangedOnly"/>). Terrain synergy on the unit side is
    /// <see cref="TerrainDamageTrait"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Equipment Bonus")]
    public class EquipmentTrait : UnitTrait
    {
        [Tooltip("Added to every attack the carrier makes.")]
        public int DamageBonus;

        [Tooltip("Subtracted from every hit the carrier takes. The resolver clamps damage at zero.")]
        public int DefenseBonus;

        [Tooltip("Extra tiles of attack range while the carrier has this.")]
        public int RangeBonus;

        [Tooltip("Weapon the carrier must have drawn for any of the bonuses above to count. Any means " +
                 "it always applies; the other two make this a piece of gear that pays off one weapon.")]
        public WeaponRequirement RequiresWeapon = WeaponRequirement.Any;

        /// <summary>
        /// The bonuses that are actually set, and the weapon they hang on. One asset covers up to
        /// three numbers, so the line is built from whichever of them a given piece of gear moves.
        /// </summary>
        public override string Summary
        {
            get
            {
                var parts = new List<string>();

                if (DamageBonus != 0)
                    parts.Add($"{Signed(DamageBonus)} Damage");

                if (DefenseBonus != 0)
                    parts.Add($"{Signed(DefenseBonus)} Defense");

                if (RangeBonus != 0)
                    parts.Add($"{Signed(RangeBonus)} Range");

                if (parts.Count == 0)
                    return base.Summary;

                return string.Join(", ", parts) + RequiresWeapon.Describe();
            }
        }

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

        public override int ModifyOutgoingDamage(int damage, CombatContext context)
        {
            return RequiresWeapon.IsMetBy(context.Attacker) ? damage + DamageBonus : damage;
        }

        public override int ModifyIncomingDamage(int damage, CombatContext context)
        {
            return RequiresWeapon.IsMetBy(context.Defender) ? damage - DefenseBonus : damage;
        }

        public override int ModifyAttackRange(int range, RangeContext context)
        {
            return RequiresWeapon.IsMetBy(context.Unit) ? range + RangeBonus : range;
        }
    }
}
