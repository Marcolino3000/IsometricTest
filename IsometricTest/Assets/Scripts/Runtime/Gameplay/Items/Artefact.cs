using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// One of the unique finds lying about the map. A <see cref="PassiveItem"/> in everything it does -
    /// a bundle of traits, in effect merely by sitting in its slot - and different in only two ways:
    /// it has a slot to itself rather than sharing one with its whole category, so every artefact
    /// found is worn at once and none has to be chosen over another, and nothing is ever offered that
    /// slot afterwards, so one is never given up.
    ///
    /// Its own category rather than a flag on the passive item, because the category is what the slot
    /// layout, the loot table and <see cref="Global.VictoryRules"/> each read to tell an artefact
    /// apart - and all three would otherwise have to learn about the flag.
    ///
    /// It is not hidden inside a box: the <see cref="Data.LootboxType"/> it is found in shows its
    /// content (<see cref="Data.LootboxType.ShowsContent"/>), so what lies on the tile is the artefact
    /// itself and can be seen for what it is from across the map.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Items/Artefact")]
    public class Artefact : PassiveItem
    {
        public override SlotKind Slot => SlotKind.Artefact;
    }
}
