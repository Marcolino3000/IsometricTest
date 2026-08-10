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
        [Header("Debug")]
        [Tooltip("Items the player owns.")]
        [SerializeField] private List<Item> items = new();

        /// <summary>
        /// What each slot holds, indexed by <see cref="SlotKind"/>. A slot keeps showing what was put
        /// in it even when it is not the thing currently in effect, so drawing a bow does not empty
        /// the slot the sword is in.
        /// </summary>
        private readonly Item[] equippedByKind = new Item[Enum.GetValues(typeof(SlotKind)).Length];

        private ItemBar itemBar;
        private Unit playerUnit;

        public IReadOnlyList<Item> Items => items;

        public void Setup(ItemBar bar)
        {
            itemBar = bar;
            itemBar.SlotActivated += HandleSlotActivated;
            itemBar.OptionChosen += HandleOptionChosen;
        }

        public void Begin(Unit unit)
        {
            playerUnit = unit;

            items.Clear();
            Array.Clear(equippedByKind, 0, equippedByKind.Length);

            var startingWeapon = playerUnit.CurrentState.AttackAction;

            if (startingWeapon != null)
                items.Add(startingWeapon);

            ShowSlots();
        }

        /// <summary>
        /// Takes an item into the inventory - what a lootbox calls. One already owned is ignored: it
        /// would be offered twice in the same picker.
        /// </summary>
        public void Pickup(Item item)
        {
            if (item == null || items.Contains(item))
                return;

            items.Add(item);
            ShowSlots();
        }

        /// <summary>
        /// What the player owns, for a history snapshot. Taking a lootbox and using an active item
        /// both cost action points, so both are turn actions and have to be undoable like any other -
        /// which means the inventory travels with the snapshot. Which weapon is *in hand* and which
        /// passive is *worn* still do not: those are loadout, free to change and reported as no
        /// action, and both are re-derived from the inventory in <see cref="RefreshEquipped"/>.
        /// </summary>
        public List<Item> CaptureItems()
        {
            return new List<Item>(items);
        }

        /// <summary>
        /// Puts a recorded inventory back. Anything no longer owned cannot stay in use, so a weapon
        /// the undo took away is dropped for one the character does own, and a passive it took away
        /// has its traits removed - both by way of <see cref="ShowSlots"/>, which re-derives every
        /// slot from what is owned.
        /// </summary>
        public void RestoreItems(IReadOnlyList<Item> recorded)
        {
            items.Clear();
            items.AddRange(recorded);

            if (playerUnit != null && !items.Contains(playerUnit.CurrentState.AttackAction))
                playerUnit.CurrentState.AttackAction = FirstOwned(SlotKind.Melee) as AttackActionData
                                                      ?? FirstOwned(SlotKind.Ranged) as AttackActionData;

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
            var slotItems = ItemsForSlot(slot);

            if (slotItems.Count == 0)
                return;

            var options = new List<ItemOption>(slotItems.Count);

            foreach (var item in slotItems)
                options.Add(new ItemOption(item.Symbol, item.Tooltip));

            itemBar.OpenPicker(slot, options, slotItems.IndexOf(EquippedIn(slot)));
        }

        private void HandleOptionChosen(int slot, int option)
        {
            var slotItems = ItemsForSlot(slot);

            if (playerUnit == null || option < 0 || option >= slotItems.Count)
                return;

            Choose(slot, slotItems[option]);
            ShowSlots();
        }

        private void ShowSlots()
        {
            // Whatever a picker was offering is stale once the equipment or the character changed.
            itemBar.ClosePicker();

            RefreshEquipped();

            for (var i = 0; i < itemBar.SlotCount; i++)
            {
                var item = EquippedIn(i);

                itemBar.SetSlotIcon(i, item != null ? item.Symbol : null);
                itemBar.SetSlotTooltip(i, item != null ? item.Tooltip : string.Empty);
                itemBar.SetSlotActive(i, IsInUse(i, item));
            }
        }

        /// <summary>
        /// Brings what the slots hold in line with what the player owns: the weapon in hand always
        /// fills its own slot, and a slot holding something no longer owned - an undone pickup, a
        /// used-up potion, or nothing yet - takes the first owned item of its kind instead, so a
        /// looted bow turns up in its slot without having to be drawn first.
        /// </summary>
        private void RefreshEquipped()
        {
            var inHand = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

            if (inHand != null)
                equippedByKind[(int)inHand.Slot] = inHand;

            for (var kind = 0; kind < equippedByKind.Length; kind++)
            {
                if (items.Contains(equippedByKind[kind]))
                    continue;

                var replacement = FirstOwned((SlotKind)kind);

                // The passive slot is the one whose contents are in effect merely by being shown, so
                // it is filled through the wearer rather than written straight into the array.
                if ((SlotKind)kind == SlotKind.Passive)
                    Wear(replacement);
                else
                    equippedByKind[kind] = replacement;
            }
        }

        private Item FirstOwned(SlotKind kind)
        {
            foreach (var item in items)
                if (item != null && item.Slot == kind)
                    return item;

            return null;
        }

        // The methods below are the whole seam between slots and categories: which category a slot
        // stands for, what it offers, what it holds, whether that is in effect, and what choosing it
        // does. A further category is a further branch here and nowhere else - the bar never learns
        // that items exist.

        /// <summary>
        /// The category <paramref name="slot"/> stands for, or false for a slot that stands for none.
        /// The bar's slots follow <see cref="SlotKind"/> in order - slot 0 melee, 1 ranged, 2 active,
        /// 3 passive - so the mapping is the enum itself rather than one serialized index per
        /// category. Four such indices can silently be given the same number, and the category
        /// checked last then loses its slot; the enum cannot collide with itself.
        /// </summary>
        private bool KindForSlot(int slot, out SlotKind kind)
        {
            kind = slot >= 0 && slot < (int)SlotKind.None ? (SlotKind)slot : SlotKind.None;

            return kind != SlotKind.None;
        }

        private List<Item> ItemsForSlot(int slot)
        {
            var slotItems = new List<Item>();

            if (!KindForSlot(slot, out var kind))
                return slotItems;

            foreach (var item in items)
                if (item != null && item.Slot == kind)
                    slotItems.Add(item);

            return slotItems;
        }

        /// <summary>What the character carries in <paramref name="slot"/>, or null.</summary>
        private Item EquippedIn(int slot)
        {
            return KindForSlot(slot, out var kind) ? equippedByKind[(int)kind] : null;
        }

        /// <summary>
        /// Whether what a slot holds is currently doing something: the weapon that is drawn and the
        /// passive that is worn are, the potion waiting to be drunk is not. Marks the slot on the bar.
        /// </summary>
        private bool IsInUse(int slot, Item item)
        {
            if (item == null || !KindForSlot(slot, out var kind))
                return false;

            return kind switch
            {
                SlotKind.Melee or SlotKind.Ranged => playerUnit != null && item == playerUnit.CurrentState.AttackAction,
                SlotKind.Passive => true,
                _ => false
            };
        }

        /// <summary>
        /// What choosing an item in a slot means, which is a different thing per category: a weapon
        /// is drawn, a passive is worn, an active is used up. This is the only place the three kinds
        /// are told apart.
        /// </summary>
        private void Choose(int slot, Item item)
        {
            if (!KindForSlot(slot, out var kind) || item == null)
                return;

            switch (kind)
            {
                case SlotKind.Melee:
                case SlotKind.Ranged:
                    // The other weapon slot keeps what it holds; only one of them is the attack.
                    equippedByKind[(int)kind] = item;
                    playerUnit.CurrentState.AttackAction = item as AttackActionData;
                    break;

                case SlotKind.Passive:
                    Wear(item);
                    break;

                case SlotKind.Active:
                    Use(item as ActiveItemData);
                    break;
            }
        }

        /// <summary>
        /// Puts a passive item on and takes the previous one off: its traits leave the character's
        /// trait list and the new one's join it, which is all a passive item is. Only one instance of
        /// each trait is removed, so one the blueprint grants as well survives taking the item off.
        /// </summary>
        private void Wear(Item item)
        {
            var previous = equippedByKind[(int)SlotKind.Passive];

            if (previous == item)
                return;

            if (playerUnit != null)
            {
                var traits = playerUnit.CurrentState.Traits;

                if (previous is PassiveItem worn)
                    foreach (var trait in worn.Traits)
                        traits.Remove(trait);

                if (item is PassiveItem chosen)
                    foreach (var trait in chosen.Traits)
                        if (trait != null)
                            traits.Add(trait);
            }

            equippedByKind[(int)SlotKind.Passive] = item;
        }

        /// <summary>
        /// Uses an active item on the character and uses it up. The item leaves the inventory
        /// *before* the action runs: the executor announces itself once it is done, and the snapshot
        /// taken at that moment has to already show the item gone, or an undo would hand it back
        /// twice. One that could not be afforded goes back where it was.
        /// </summary>
        private void Use(ActiveItemData item)
        {
            if (playerUnit == null || item == null)
                return;

            var index = items.IndexOf(item);

            if (index < 0)
                return;

            items.RemoveAt(index);

            if (!playerUnit.ActionExecutor.ExecuteItemAction(item))
                items.Insert(index, item);
        }
    }
}
