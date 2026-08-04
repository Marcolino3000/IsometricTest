using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Match-wide rule switches that belong to no single unit or tile. Handed to <see cref="CombatRules"/>
    /// by the Initiator, which is also where any future consumer should get it from. Held as a reference
    /// rather than copied into fields, so toggling a rule applies immediately - including during play.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Rules/Game Rules")]
    public class GameRules : ScriptableObject
    {
        [Tooltip("When off, a defender never strikes back after being attacked, no matter the range.")]
        public bool RetaliationEnabled = true;
    }
}
