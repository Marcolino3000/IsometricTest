using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Entities;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// The player's inventory and the only translator between items and the <see cref="ItemBar"/>.
    /// A slot stands for a category: it shows what the character has equipped of that kind, and
    /// activating it offers everything owned that fits there.
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        [Tooltip("Slot the weapons are offered in. The remaining slots have no category yet.")]
        [SerializeField] private int weaponSlot;

        [Header("Debug")]
        [Tooltip("Weapons the player owns.")]
        [SerializeField] private List<AttackActionData> weapons = new();

        /// <summary>Answer for a slot that has no category, so the callers need no null check.</summary>
        private static readonly List<AttackActionData> NoItems = new();

        private ItemBar itemBar;
        private Unit playerUnit;

        public IReadOnlyList<AttackActionData> Weapons => weapons;

        public void Setup(ItemBar bar)
        {
            itemBar = bar;
            itemBar.SlotActivated += HandleSlotActivated;
            itemBar.OptionChosen += HandleOptionChosen;
        }

        public void Begin(Unit unit)
        {
            playerUnit = unit;

            weapons.Clear();

            var startingWeapon = playerUnit.CurrentState.AttackAction;

            if (startingWeapon != null)
                weapons.Add(startingWeapon);

            ShowSlots();
        }

        /// <summary>
        /// Takes a weapon into the inventory - what a lootbox calls. A weapon already owned is
        /// ignored: it would be offered twice in the same picker.
        /// </summary>
        public void Pickup(AttackActionData weapon)
        {
            if (weapon == null || weapons.Contains(weapon))
                return;

            weapons.Add(weapon);
            ShowSlots();
        }

        /// <summary>
        /// The bar builds its slots in its own Awake, which may run after the Initiator has already
        /// called <see cref="Begin"/> - what was pushed into it back then had nowhere to go yet.
        /// </summary>
        private void Start()
        {
            ShowSlots();
        }

        /// <summary>
        /// Answers the bar with everything that fits the activated slot, starting the choice on
        /// what is equipped there. A slot with nothing to offer opens no picker.
        /// </summary>
        private void HandleSlotActivated(int slot)
        {
            var items = ItemsForSlot(slot);

            if (items.Count == 0)
                return;

            var options = new List<ItemOption>(items.Count);

            foreach (var item in items)
                options.Add(new ItemOption(item.Symbol, item.Tooltip));

            itemBar.OpenPicker(slot, options, items.IndexOf(EquippedIn(slot)));
        }

        private void HandleOptionChosen(int slot, int option)
        {
            var items = ItemsForSlot(slot);

            if (playerUnit == null || option < 0 || option >= items.Count)
                return;

            Equip(slot, items[option]);
            ShowSlots();
        }

        private void ShowSlots()
        {
            // Whatever a picker was offering is stale once the equipment or the character changed.
            itemBar.ClosePicker();

            for (var i = 0; i < itemBar.SlotCount; i++)
            {
                var item = EquippedIn(i);

                itemBar.SetSlotIcon(i, item != null ? item.Symbol : null);
                itemBar.SetSlotTooltip(i, item != null ? item.Tooltip : string.Empty);
            }
        }

        // The three methods below are the whole seam between slots and categories: what a slot
        // offers, what it currently holds, and where the choice is written. A second category is a
        // second branch here and nowhere else - the bar never learns that items exist.

        private List<AttackActionData> ItemsForSlot(int slot)
        {
            return slot == weaponSlot ? weapons : NoItems;
        }

        /// <summary>What the character carries in <paramref name="slot"/>, or null.</summary>
        private AttackActionData EquippedIn(int slot)
        {
            return slot == weaponSlot && playerUnit != null ? playerUnit.CurrentState.AttackAction : null;
        }

        private void Equip(int slot, AttackActionData item)
        {
            if (slot == weaponSlot)
                playerUnit.CurrentState.AttackAction = item;
        }
    }
}
