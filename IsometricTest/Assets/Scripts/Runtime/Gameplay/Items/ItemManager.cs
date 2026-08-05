using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Entities;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    public class ItemManager : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Weapons the player owns, in slot order.")]
        [SerializeField] private List<AttackActionData> weapons = new();

        private ItemBar itemBar;
        private Unit playerUnit;

        public IReadOnlyList<AttackActionData> Weapons => weapons;

        public void Setup(ItemBar bar)
        {
            itemBar = bar;
            itemBar.SlotSelected += HandleSlotSelected;
        }
        
        public void Begin(Unit unit)
        {
            playerUnit = unit;

            weapons.Clear();

            var startingWeapon = playerUnit.CurrentState.AttackAction;

            if (startingWeapon != null)
                weapons.Add(startingWeapon);

            ShowWeapons();
        }

        /// <summary>
        /// Takes a weapon into the inventory - what a lootbox calls. A weapon already owned is
        /// ignored: two slots holding the same one would each claim to be the armed one.
        /// </summary>
        public void Pickup(AttackActionData weapon)
        {
            if (weapon == null || weapons.Contains(weapon))
                return;

            weapons.Add(weapon);
            ShowWeapons();
        }

        /// <summary>
        /// The bar builds its slots in its own Awake, which may run after the Initiator has already
        /// called <see cref="Begin"/> - what was pushed into it back then had nowhere to go yet.
        /// </summary>
        private void Start()
        {
            ShowWeapons();
        }

        private void HandleSlotSelected(int index)
        {
            var weapon = WeaponAt(index);

            // A weapon cannot be put down, only swapped: an empty slot, or clicking the armed slot to
            // clear it, leaves the character holding what it already had - so the bar is put back on
            // that slot rather than left showing nothing.
            if (weapon == null)
            {
                itemBar.ShowSelection(EquippedSlot);
                return;
            }

            if (playerUnit != null)
                playerUnit.CurrentState.AttackAction = weapon;
        }

        private void ShowWeapons()
        {
            for (var i = 0; i < itemBar.SlotCount; i++)
            {
                var weapon = WeaponAt(i);

                itemBar.SetSlotIcon(i, weapon != null ? weapon.Symbol : null);
                itemBar.SetSlotTooltip(i, weapon != null ? weapon.Tooltip : string.Empty);
            }

            itemBar.ShowSelection(EquippedSlot);
        }

        private AttackActionData WeaponAt(int index)
        {
            return index >= 0 && index < weapons.Count ? weapons[index] : null;
        }

        /// <summary>Slot holding the weapon in hand, or <see cref="ItemBar.NoSelection"/> when none does.</summary>
        private int EquippedSlot
        {
            get
            {
                var equipped = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

                // IndexOf reports -1 for a weapon the player does not own, which is NoSelection.
                return equipped != null ? weapons.IndexOf(equipped) : ItemBar.NoSelection;
            }
        }
    }
}
