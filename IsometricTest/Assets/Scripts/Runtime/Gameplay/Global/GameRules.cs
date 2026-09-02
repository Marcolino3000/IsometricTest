using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Match-wide rule switches that belong to no single unit or tile. Handed to <see cref="CombatRules"/>
    /// by the Initiator, which is also where any future consumer should get it from. Held as a reference
    /// rather than copied into fields, so toggling a rule applies immediately - including during play.
    ///
    /// A <see cref="RuntimeSettings"/>, so a switch flipped in the inspector announces itself:
    /// whoever draws from these subscribes to <see cref="RuntimeSettings.Changed"/> instead of
    /// watching its own fields for drift.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Rules/Game Rules")]
    public class GameRules : RuntimeSettings
    {
        [Tooltip("Whether a defender strikes back after being attacked. Only the default: a unit's " +
                 "traits are asked afterwards, so gear can grant a counter-strike while this is off " +
                 "or forgo one while it is on. A counter-strike still has to reach the attacker.")]
        public bool RetaliationEnabled = true;

        [Header("Zones")]
        [Tooltip("When on, an opposing unit may only walk inside the ring of the map it spawned in - " +
                 "it holds the distance it guards instead of following the character across the " +
                 "board. Only its steps are confined: it still strikes across the border at anything " +
                 "its weapon reaches. A map with no rings authored confines nobody.")]
        public bool ConfineOpponentsToSpawnZone;

        [Header("Fog of war")]
        [Tooltip("When on, the map switches to the acting team's vision while an AI plays its turn, so you " +
                 "watch every move it makes. When off, the view stays with your own units and the AI's moves " +
                 "only show where you can actually see them. A turn you took over manually is never hidden.")]
        public bool ShowEnemyTurns = true;

        [Tooltip("When on, ground nobody has scouted is drawn as plain flat terrain - a hill or a " +
                 "mountain only shows what it is once it has been seen. Remembered ground keeps its " +
                 "own look: terrain does not move, so what was seen there is still true. Off, the " +
                 "whole board's terrain is readable from the start and the fog only darkens it.")]
        public bool HideUnexploredTerrain = true;

        [Header("Items and loot")]
        [Tooltip("When on, the same active item may be carried more than once - three Healing Draughts " +
                 "fill the three active slots. Only active items can stack: a weapon or a passive is " +
                 "identified by its asset and shares one slot with its whole category, so a second " +
                 "copy would have nowhere of its own to be and would do nothing besides. When off, a " +
                 "lootbox holding something already carried is left where it lies.")]
        public bool StackDuplicateActiveItems = true;

        [Tooltip("When on, walking onto a lootbox takes it - for free, and every box the path crosses " +
                 "rather than only the one it ends on. Off, a box is taken by standing on it and " +
                 "pressing the interact key, which costs action points. Either way a box holding " +
                 "something that cannot be carried is left where it lies.")]
        public bool AutoCollectLootboxes;

        [Header("Board overlays")]
        [Tooltip("When on, selecting or hovering a unit tints every tile it could walk to. The path " +
                 "preview under the cursor is drawn either way, so with this off the reach is read " +
                 "off the blue line instead of the white field.")]
        public bool ShowMovementRange = true;

        [Header("Reading the enemy")]
        [Tooltip("When on, every unit wears a row of badges: the weapon it has drawn, and one per " +
                 "trait it carries. A trait with no symbol authored is badged with its name.")]
        public bool ShowUnitBadges = true;

        [Tooltip("When on, hovering a unit puts a card beside it with its health, its points and " +
                 "what each of its badges means. The badges say what it can do; this says what the " +
                 "numbers are. The same card everything else is labelled with - see TooltipSettings " +
                 "for how long it waits and how it is drawn.")]
        public bool ShowUnitCard = true;

        [Tooltip("When on, the line where one zone of the map ends and the next begins is drawn " +
                 "between the tiles it divides. What lies in which zone is unaffected - this is only " +
                 "whether the border is shown.")]
        public bool ShowZoneBorders = true;

        [Tooltip("When on, hovering an enemy tints every tile it could strike next turn - measured " +
                 "from the points it starts a turn with, not the ones it has left, so it reads the " +
                 "same on your turn as on its own. The tiles it could stand on stay marked over the " +
                 "top, so what is left orange is what it can reach you at without moving there.")]
        public bool ShowThreatZone = true;

        [Header("Victory")]
        [Tooltip("When on, the match is won the moment the last opposing unit falls.")]
        public bool WinByDefeatingAllEnemies = true;

        [Tooltip("When on, the match is won once every walkable tile has been uncovered. Impassable " +
                 "terrain is not counted - the inside of a mountain range may never come within sight " +
                 "of anywhere the character can stand.")]
        public bool WinByExploringMap = true;

        [Tooltip("When on, the match is won once all three artefacts have been found. They lie open " +
                 "on the map rather than inside a box, each is worn for good in a slot of its own, " +
                 "and each grants one bonus - to attacks, to movement, to defence. Asked before the " +
                 "condition below, since collecting the set is the more particular achievement and " +
                 "an artefact counts as loot for that one too.")]
        public bool WinByCollectingAllArtefacts = true;

        [Tooltip("When on, the match is won once every lootbox on the map has been taken. A box " +
                 "holding something that cannot be carried is left where it lies, so it has to be " +
                 "come back for with a slot free - the win waits for it either way.")]
        public bool WinByCollectingAllLoot = true;

        [Tooltip("When on, losing your character ends the match in defeat. Asked before the two above, " +
                 "so a character that falls to the retaliation of the last enemy it struck down has lost " +
                 "rather than won by a hair.")]
        public bool LoseWhenCharacterFalls = true;

        [Header("Debug")]
        [Tooltip("Writes each tile's grid position onto it. Debugging only - it is the one thing on " +
                 "the board that is not part of the game being played. Can be toggled during play.")]
        public bool ShowTileCoordinates = true;


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
