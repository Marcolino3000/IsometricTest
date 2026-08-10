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
        /// <summary>
        /// The category every slot of the bar stands for, in bar order: melee 0 (key 1), ranged 1,
        /// passive 2, and the three active slots 3 to 5 (keys 4 to 6). A category may take more than
        /// one slot, which is what tells the two kinds of category apart: a weapon or a passive shares
        /// its one slot with everything of its kind and is picked from a column, while an active item
        /// gets a slot to itself - so there is nothing to pick, and how many can be carried at once is
        /// how many slots the category has here.
        ///
        /// This table is the layout, not <see cref="SlotKind"/>'s own order: the enum is serialized on
        /// item assets and in the loot settings, so rearranging the bar must not mean rearranging it.
        /// </summary>
        private static readonly SlotKind[] SlotKinds =
        {
            SlotKind.Melee,
            SlotKind.Ranged,
            SlotKind.Passive,
            SlotKind.Active,
            SlotKind.Active,
            SlotKind.Active
        };

        /// <summary>Why an item cannot be taken - see <see cref="CanTake"/>. Short: they are shown
        /// over the character's head, where a damage number normally goes.</summary>
        private const string NoRoomNotice = "No free item slot";
        private const string AlreadyCarriedNotice = "Already carried";

        [Header("Debug")]
        [Tooltip("Items the player owns.")]
        [SerializeField] private List<Item> items = new();

        /// <summary>
        /// What each slot holds. Indexed by slot rather than by category, since a category can have
        /// several of them. A slot keeps showing what was put in it even when that is not the thing
        /// currently in effect, so drawing a bow does not empty the slot the sword is in, and a potion
        /// drunk out of slot 4 does not shuffle the one in slot 5 down into its place.
        /// </summary>
        private readonly Item[] equippedBySlot = new Item[SlotKinds.Length];

        /// <summary>
        /// Everything already announced by the find popup. Deliberately outside the snapshot: undoing
        /// a pickup and taking the same box again is the same find over, not a second one, and a find
        /// only surprises once - so a card shows for as long as the match runs and no longer.
        /// </summary>
        private readonly HashSet<Item> announced = new();

        private ItemBar itemBar;
        private ItemPopup itemPopup;
        private Unit playerUnit;

        public IReadOnlyList<Item> Items => items;

        /// <summary>How many slots the layout above describes - what the bar has to build.</summary>
        public static int SlotCount => SlotKinds.Length;

        public void Setup(ItemBar bar, ItemPopup popup)
        {
            itemBar = bar;
            itemPopup = popup;
            itemBar.SlotActivated += HandleSlotActivated;
            itemBar.OptionChosen += HandleOptionChosen;

            // A bar built too short leaves a category with no slot, and one built too long shows slots
            // that stand for nothing. Neither is visible from the bar's side, which knows no categories.
            if (itemBar.SlotCount != SlotCount)
                Debug.LogWarning($"The item bar builds {itemBar.SlotCount} slots but the layout " +
                                 $"describes {SlotCount}.", itemBar);
        }

        public void Begin(Unit unit)
        {
            playerUnit = unit;

            items.Clear();
            announced.Clear();
            Array.Clear(equippedBySlot, 0, equippedBySlot.Length);

            if (itemPopup != null)
                itemPopup.Hide();

            var startingWeapon = playerUnit.CurrentState.AttackAction;

            if (startingWeapon != null)
                items.Add(startingWeapon);

            ShowSlots();
        }

        /// <summary>
        /// Whether the player can take <paramref name="item"/>, and what to tell them when not. Two
        /// things stand in the way, and this is the *only* place either is decided: whoever is about
        /// to hand an item over asks first, and <see cref="Pickup"/> asks again. They were once two
        /// separate rules - one asked beforehand, one enforced inside - and a lootbox holding
        /// something that failed the second was opened and emptied for nothing.
        ///
        /// <list type="bullet">
        /// <item>The same asset cannot be owned twice: the slots, the find popup and using one up all
        /// identify an item *by the asset*, so a second copy would be the first one over again.</item>
        /// <item>A category carrying one item per slot is full once its slots are - active items.</item>
        /// </list>
        /// </summary>
        public bool CanTake(Item item, out string reason)
        {
            reason = string.Empty;

            // No box should hold nothing; there is nothing to say about it if one does.
            if (item == null)
                return false;

            if (items.Contains(item))
            {
                reason = AlreadyCarriedNotice;

                return false;
            }

            if (HoldsOneItem(item.Slot) && CountOwned(item.Slot) >= SlotsOf(item.Slot))
            {
                reason = NoRoomNotice;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Takes an item into the inventory - what a lootbox calls. Callers ask
        /// <see cref="CanTake"/> first so they can say why nothing happened and hold on to whatever
        /// they were going to hand over, but this asks again: nothing may enter the inventory that the
        /// one rule turns away.
        /// </summary>
        public void Pickup(Item item)
        {
            if (!CanTake(item, out _))
                return;

            items.Add(item);
            ShowSlots();

            // After the slots, not before: the card says where the item went, so it has to have gone
            // there. Only the first find of a thing is announced - see <see cref="announced"/>.
            if (announced.Add(item))
                Announce(item);
        }

        /// <summary>
        /// Puts a found item on the screen: everything it says about itself, plus the one thing only
        /// the owner of the slots knows - which of them it landed in.
        /// </summary>
        private void Announce(Item item)
        {
            if (itemPopup == null)
                return;

            itemPopup.Show(new ItemCard(item.Symbol, item.Title, Item.NameOf(item.Slot), SlotNameOf(item),
                item.Description, item.Stats));
        }

        /// <summary>
        /// What the slot an item ended up in is called, counted from one because that is the key the
        /// bar labels the slot with. Read off the slots rather than worked out from the category: a
        /// category can hold several slots, and only the slots know which one took this item. Empty
        /// for an item no slot is showing.
        /// </summary>
        private string SlotNameOf(Item item)
        {
            for (var slot = 0; slot < equippedBySlot.Length; slot++)
                if (equippedBySlot[slot] == item)
                    return $"Slot {slot + 1}";

            return string.Empty;
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

            // A card saying where a find went is only true until that find is undone, which is the
            // one case it can still be up for: nothing else rewinds the inventory.
            if (itemPopup != null)
                itemPopup.Hide();

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
            if (!KindForSlot(slot, out var kind))
                return;

            // A slot holding one item offers no choice, so activating it *is* choosing what it holds -
            // pressing 4 drinks the potion in slot 4 rather than opening a column of one entry.
            if (HoldsOneItem(kind))
            {
                Choose(slot, EquippedIn(slot));
                ShowSlots();

                return;
            }

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
        /// fills its own slot, a slot holding something no longer owned - an undone pickup, a used-up
        /// potion - is emptied, and an empty slot takes the first owned item of its kind no other slot
        /// is already showing. So a looted bow turns up in its slot without having to be drawn, a
        /// second potion finds a slot of its own, and the potion beside a drunk one stays where it was.
        /// </summary>
        private void RefreshEquipped()
        {
            var inHand = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

            if (inHand != null && FirstSlotOf(inHand.Slot, out var weaponSlot))
                equippedBySlot[weaponSlot] = inHand;

            for (var slot = 0; slot < equippedBySlot.Length; slot++)
                if (equippedBySlot[slot] != null && !items.Contains(equippedBySlot[slot]))
                    Fill(slot, null);

            for (var slot = 0; slot < equippedBySlot.Length; slot++)
                if (equippedBySlot[slot] == null)
                    Fill(slot, FirstFitting(slot));
        }

        /// <summary>
        /// Puts <paramref name="item"/> in <paramref name="slot"/>. The passive slot is filled through
        /// the wearer rather than written straight into the array: what it holds is in effect merely
        /// by being shown there.
        /// </summary>
        private void Fill(int slot, Item item)
        {
            if (SlotKinds[slot] == SlotKind.Passive)
                Wear(slot, item);
            else
                equippedBySlot[slot] = item;
        }

        private Item FirstOwned(SlotKind kind)
        {
            foreach (var item in items)
                if (item != null && item.Slot == kind)
                    return item;

            return null;
        }

        /// <summary>The first owned item that fits <paramref name="slot"/>, or null.</summary>
        private Item FirstFitting(int slot)
        {
            var slotItems = ItemsForSlot(slot);

            return slotItems.Count > 0 ? slotItems[0] : null;
        }

        private int CountOwned(SlotKind kind)
        {
            var count = 0;

            foreach (var item in items)
                if (item != null && item.Slot == kind)
                    count++;

            return count;
        }

        // The methods below are the whole seam between slots and categories: which category a slot
        // stands for, how many slots it has, what one offers, what it holds, whether that is in
        // effect, and what choosing it does. A further category is a further entry in the layout and
        // a further branch here, and nowhere else - the bar never learns that items exist.

        /// <summary>
        /// The category <paramref name="slot"/> stands for, or false for a slot that stands for none.
        /// </summary>
        private static bool KindForSlot(int slot, out SlotKind kind)
        {
            kind = slot >= 0 && slot < SlotKinds.Length ? SlotKinds[slot] : SlotKind.None;

            return kind != SlotKind.None;
        }

        /// <summary>How many slots <paramref name="kind"/> is given - how many can be carried.</summary>
        private static int SlotsOf(SlotKind kind)
        {
            var count = 0;

            foreach (var slotKind in SlotKinds)
                if (slotKind == kind)
                    count++;

            return count;
        }

        /// <summary>The lowest slot standing for <paramref name="kind"/>, or false for a category
        /// the layout gives no slot at all.</summary>
        private static bool FirstSlotOf(SlotKind kind, out int slot)
        {
            for (var i = 0; i < SlotKinds.Length; i++)
            {
                if (SlotKinds[i] != kind)
                    continue;

                slot = i;

                return true;
            }

            slot = -1;

            return false;
        }

        /// <summary>
        /// Whether a category carries one item per slot rather than a shelf of them behind one slot.
        /// The whole difference between the two: one is chosen from a column, the other is simply
        /// there, and how many of it can be carried is how many slots the layout gives it.
        /// </summary>
        private static bool HoldsOneItem(SlotKind kind)
        {
            return kind == SlotKind.Active;
        }

        /// <summary>
        /// Everything owned that <paramref name="slot"/> can show: items of its category that no other
        /// slot is already holding. The second half only ever matters to a category with several slots
        /// - a category with one can have nothing held elsewhere.
        /// </summary>
        private List<Item> ItemsForSlot(int slot)
        {
            var slotItems = new List<Item>();

            if (!KindForSlot(slot, out var kind))
                return slotItems;

            foreach (var item in items)
                if (item != null && item.Slot == kind && !HeldElsewhere(item, slot))
                    slotItems.Add(item);

            return slotItems;
        }

        /// <summary>Whether a slot other than <paramref name="slot"/> is holding the item.</summary>
        private bool HeldElsewhere(Item item, int slot)
        {
            for (var i = 0; i < equippedBySlot.Length; i++)
                if (i != slot && equippedBySlot[i] == item)
                    return true;

            return false;
        }

        /// <summary>What the character carries in <paramref name="slot"/>, or null.</summary>
        private Item EquippedIn(int slot)
        {
            return slot >= 0 && slot < equippedBySlot.Length ? equippedBySlot[slot] : null;
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
                    equippedBySlot[slot] = item;
                    playerUnit.CurrentState.AttackAction = item as AttackActionData;
                    break;

                case SlotKind.Passive:
                    Wear(slot, item);
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
        private void Wear(int slot, Item item)
        {
            var previous = equippedBySlot[slot];

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

            equippedBySlot[slot] = item;
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
