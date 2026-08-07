using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Data/Settings/LootSpawnerSettings")]
    public class LootSpawnerSettings : ScriptableObject
    {
        [Header("Lootbox Settings")]
        public Lootbox LootboxPrefab;

        [Tooltip("How many boxes to scatter. Capped by how many walkable tiles are free.")]
        public int LootboxCount = 5;

        [Tooltip("Action points taking a box costs. Belongs to the loot rather than to a unit, so it " +
                 "lives here instead of on a blueprint.")]
        public int PickupCost = 1;

        [Header("Contents")]
        [Tooltip("Weapons the boxes hand out. Dealt without repeating until every one has been placed once.")]
        public List<AttackActionData> Weapons = new();

        [Header("Visual Settings")]
        [Tooltip("Sorting order of the box sprite. Below the units so nobody is hidden behind a box.")]
        public int OrderInLayer = 1;
    }
}
