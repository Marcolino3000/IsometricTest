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
        [Tooltip("Whether a defender strikes back after being attacked. Only the default: a unit's " +
                 "traits are asked afterwards, so gear can grant a counter-strike while this is off " +
                 "or forgo one while it is on. A counter-strike still has to reach the attacker.")]
        public bool RetaliationEnabled = true;

        [Tooltip("When on, the map switches to the acting team's vision while an AI plays its turn, so you " +
                 "watch every move it makes. When off, the view stays with your own units and the AI's moves " +
                 "only show where you can actually see them. A turn you took over manually is never hidden.")]
        public bool ShowEnemyTurns = true;

        [Tooltip("When on, the same active item may be carried more than once - three Healing Draughts " +
                 "fill the three active slots. Only active items can stack: a weapon or a passive is " +
                 "identified by its asset and shares one slot with its whole category, so a second " +
                 "copy would have nowhere of its own to be and would do nothing besides. When off, a " +
                 "lootbox holding something already carried is left where it lies.")]
        public bool StackDuplicateActiveItems = true;

        [Header("Victory")]
        [Tooltip("When on, the match is won the moment the last opposing unit falls.")]
        public bool WinByDefeatingAllEnemies = true;

        [Tooltip("When on, the match is won once every walkable tile has been uncovered. Impassable " +
                 "terrain is not counted - the inside of a mountain range may never come within sight " +
                 "of anywhere the character can stand.")]
        public bool WinByExploringMap = true;

        [Tooltip("When on, losing your character ends the match in defeat. Asked before the two above, " +
                 "so a character that falls to the retaliation of the last enemy it struck down has lost " +
                 "rather than won by a hair.")]
        public bool LoseWhenCharacterFalls = true;

        [Header("Debug")]
        [Tooltip("Prints every attack to the console with its damage breakdown: the weapon's base " +
                 "damage, each trait that changed the number and what it changed it to, the damage " +
                 "dealt and the health left, why nobody struck back and who died. Debugging only - " +
                 "costs a formatted string per attack. Can be toggled during play.")]
        public bool LogCombatCalculations;

        [Tooltip("Adds everything around the numbers to that log: which tiles the two stood on and how " +
                 "far apart they were, what a trait rolled (crit chances), and the traits that were " +
                 "asked and changed nothing. Needs the switch above.")]
        public bool LogCombatDetails;
    }
}
