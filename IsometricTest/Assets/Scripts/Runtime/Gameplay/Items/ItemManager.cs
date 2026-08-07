using System;
using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Entities;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    public class ItemManager : MonoBehaviour
    {
        [SerializeField] private int meleeSlot = 0;
        [SerializeField] private int rangedSlot = 1;

        [Header("Debug")]
        [Tooltip("Weapons the player owns.")]
        [SerializeField] private List<AttackActionData> weapons = new();

        /// <summary>
        /// What each slot holds, indexed by <see cref="WeaponKind"/>. Only one weapon can be in hand
        /// (<see cref="UnitState.AttackAction"/>), but a slot keeps showing what was put in it, so
        /// drawing a bow does not empty the slot the sword is in.
        /// </summary>
        private readonly AttackActionData[] equippedByKind =
            new AttackActionData[Enum.GetValues(typeof(WeaponKind)).Length];

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
            Array.Clear(equippedByKind, 0, equippedByKind.Length);

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
        /// What the player owns, for a history snapshot. Taking a lootbox costs action points, so it
        /// is a turn action and has to be undoable like any other - which means the inventory it fills
        /// travels with the snapshot. Which weapon is *in hand* still does not: that is loadout, free
        /// to swap and reported as no action.
        /// </summary>
        public List<AttackActionData> CaptureWeapons()
        {
            return new List<AttackActionData>(weapons);
        }

        /// <summary>
        /// Puts a recorded inventory back. A weapon that is no longer owned cannot stay in hand, so
        /// the character falls back to something it does own rather than swinging a looted sword the
        /// undo just took away.
        /// </summary>
        public void RestoreWeapons(IReadOnlyList<AttackActionData> recorded)
        {
            weapons.Clear();
            weapons.AddRange(recorded);

            if (playerUnit != null && !weapons.Contains(playerUnit.CurrentState.AttackAction))
                playerUnit.CurrentState.AttackAction = weapons.Count > 0 ? weapons[^1] : null;

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

            RefreshEquipped();

            var inHand = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

            for (var i = 0; i < itemBar.SlotCount; i++)
            {
                var item = EquippedIn(i);

                itemBar.SetSlotIcon(i, item != null ? item.Symbol : null);
                itemBar.SetSlotTooltip(i, item != null ? item.Tooltip : string.Empty);
                itemBar.SetSlotActive(i, item != null && item == inHand);
            }
        }

        /// <summary>
        /// Brings what the slots hold in line with what the player owns: the weapon in hand is always
        /// the one its own slot shows, a slot still empty takes the first weapon of its kind - a
        /// looted bow appears in its slot without having to be drawn first - and one the player no
        /// longer owns (an undone pickup) cannot stay on the bar.
        /// </summary>
        private void RefreshEquipped()
        {
            var inHand = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

            if (inHand != null)
                equippedByKind[(int)inHand.Kind] = inHand;

            for (var kind = 0; kind < equippedByKind.Length; kind++)
                if (!weapons.Contains(equippedByKind[kind]))
                    equippedByKind[kind] = FirstOwned((WeaponKind)kind);
        }

        private AttackActionData FirstOwned(WeaponKind kind)
        {
            foreach (var weapon in weapons)
                if (weapon.Kind == kind)
                    return weapon;

            return null;
        }

        // The four methods below are the whole seam between slots and categories: which category a
        // slot stands for, what it offers, what it currently holds, and where the choice is written.
        // A further category is a further branch here and nowhere else - the bar never learns that
        // items exist.

        /// <summary>
        /// The kind of weapon <paramref name="slot"/> stands for, or false for a slot that stands
        /// for no category yet.
        /// </summary>
        private bool KindForSlot(int slot, out WeaponKind kind)
        {
            kind = WeaponKind.Melee;

            if (slot == meleeSlot)
                return true;

            if (slot != rangedSlot)
                return false;

            kind = WeaponKind.Ranged;

            return true;
        }

        private List<AttackActionData> ItemsForSlot(int slot)
        {
            var items = new List<AttackActionData>();

            if (!KindForSlot(slot, out var kind))
                return items;

            foreach (var weapon in weapons)
                if (weapon.Kind == kind)
                    items.Add(weapon);

            return items;
        }

        /// <summary>What the character carries in <paramref name="slot"/>, or null.</summary>
        private AttackActionData EquippedIn(int slot)
        {
            return KindForSlot(slot, out var kind) ? equippedByKind[(int)kind] : null;
        }

        /// <summary>
        /// Puts <paramref name="item"/> in its slot and in the character's hand - choosing a weapon
        /// is drawing it. The other slot keeps what it holds; only one of them is the attack.
        /// </summary>
        private void Equip(int slot, AttackActionData item)
        {
            if (!KindForSlot(slot, out var kind))
                return;

            equippedByKind[(int)kind] = item;
            playerUnit.CurrentState.AttackAction = item;
        }
    }
}
