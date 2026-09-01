using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Whether the carrier answers a hit at all, whatever the match rules say: on, it strikes back
    /// even while <see cref="Global.GameRules.RetaliationEnabled"/> is off; off, it never strikes
    /// back even while that is on. One class for both because it is one question with two answers,
    /// and gear granting it is worth nothing unless something can take it away again.
    ///
    /// It is a class of its own rather than another field on <see cref="EquipmentTrait"/> because
    /// that one is flat numbers - one <c>int</c> per hook, folded into a running total - and this is
    /// a yes or no that overrules whatever came before it.
    ///
    /// Only permission: the counter-strike still has to reach, which is
    /// <see cref="Global.CombatRules.GetEffectiveAttackRange"/>'s business and stays a separate
    /// question - reach is what <see cref="EquipmentTrait.RangeBonus"/> is for.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Traits/Unit/Retaliation")]
    public class RetaliationTrait : UnitTrait
    {
        [Tooltip("On, the carrier strikes back at whoever attacks it - even when retaliation is " +
                 "turned off in the GameRules. Off, it never strikes back, even when it is turned " +
                 "on. Either way the counter-strike still has to reach the attacker.")]
        public bool Retaliates = true;

        [Tooltip("Weapon the carrier must have drawn for this to count. Any means it always " +
                 "applies; the other two make this gear that pays off one weapon.")]
        public WeaponRequirement RequiresWeapon = WeaponRequirement.Any;

        public override string Summary =>
            (Retaliates ? "Strikes back" : "No retaliation") + RequiresWeapon.Describe();

        public override bool ModifyRetaliation(bool canRetaliate, CombatContext context)
        {
            // The context is the counter-strike, so its attacker is the one who would answer - the
            // carrier, and the unit whose weapon the requirement is about.
            return RequiresWeapon.IsMetBy(context.Attacker) ? Retaliates : canRetaliate;
        }
    }
}
