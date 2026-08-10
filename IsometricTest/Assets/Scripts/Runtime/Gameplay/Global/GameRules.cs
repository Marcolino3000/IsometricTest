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

        [Tooltip("When on, the map switches to the acting team's vision while an AI plays its turn, so you " +
                 "watch every move it makes. When off, the view stays with your own units and the AI's moves " +
                 "only show where you can actually see them. A turn you took over manually is never hidden.")]
        public bool ShowEnemyTurns = true;
    }
}
