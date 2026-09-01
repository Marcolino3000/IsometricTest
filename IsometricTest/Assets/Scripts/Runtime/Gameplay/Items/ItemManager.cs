using System;
using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    public class ItemManager : MonoBehaviour
    {
        /// <summary>
        /// The category every slot of the bar stands for, in bar order: melee 0 (key 1), ranged 1,
        /// passive 2, the three active slots 3 to 5 (keys 4 to 6) and the three artefact slots 6 to 8
        /// (keys 7 to 9). A category may take more than one slot, which is what tells the two kinds of
        /// category apart: a weapon or a passive shares its one slot with everything of its kind and is
        /// picked from a column, while an active item or an artefact gets a slot to itself - so there
        /// is nothing to pick, and how many can be carried at once is how many slots the category has
        /// here. Three artefact slots because there are three artefacts to find, and every one found is
        /// worn: they are the set the match can be won by collecting, so the empty ones are also what
        /// says how much of it is still out there.
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
            SlotKind.Active,
            SlotKind.Artefact,
            SlotKind.Artefact,
            SlotKind.Artefact
        };

        /// <summary>Why an item cannot be taken - see <see cref="CanTake"/>. Short: they are shown
        /// over the character's head, where a damage number normally goes.</summary>
        private const string NoRoomNotice = "No free item slot";
        private const string AlreadyCarriedNotice = "Already carried";

        /// <summary>How a merge went - shown under the two merge slots, where the odds were.</summary>
        private const string MergeSucceededNotice = "The traits carried over.";
        private const string MergeFailedNotice = "The merge failed. The right item was lost.";
        private const string NothingToMergeNotice = "Nothing owned fits that slot";

        [Header("Look")]
        [Tooltip("What each category of item looks like in the bar: the symbol an empty slot of it " +
                 "shows in place of what it does not hold, and the colour every slot of it wears. " +
                 "Left empty, a slot looks as it did - blank while empty, and with no accent.")]
        [SerializeField] private SlotIconSet slotIcons;

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
        private MergeScreen mergeScreen;
        private GameRules gameRules;
        private Unit playerUnit;

        // The one owner of what the cursor is on. The bar's hover is reported to it rather than acted
        // on here, so the world's own hover and this one cannot undo each other within a frame.
        private HoverTarget hoverTarget;

        /// <summary>The slot the cursor rests on, or -1. Kept because what a slot holds can change
        /// under a resting cursor - a potion drunk out of it, a second copy taking its place.</summary>
        private int hoveredSlot = -1;

        /// <summary>
        /// What the two merge slots hold. Kept here rather than on the screen for the same reason the
        /// bar's slots are: the screen is a pure view that knows indices and icons, and this is the
        /// only place that knows they are items. Deliberately outside <see cref="GameSnapshot"/> -
        /// choosing what to feed a merge costs nothing and changes nothing, exactly like drawing a
        /// weapon; it is only ever re-checked against what is still owned (<see cref="ShowMerge"/>).
        /// </summary>
        private Item mergeLeft;
        private Item mergeRight;

        /// <summary>How the last merge went, until either slot is filled afresh.</summary>
        private string mergeNotice = string.Empty;

        public IReadOnlyList<Item> Items => items;

        /// <summary>How many slots the layout above describes - what the bar has to build.</summary>
        public static int SlotCount => SlotKinds.Length;

        public void Setup(ItemBar bar, ItemPopup popup, MergeScreen merge, GameRules rules,
            HoverTarget hover)
        {
            itemBar = bar;
            itemPopup = popup;
            mergeScreen = merge;
            gameRules = rules;
            hoverTarget = hover;
            itemBar.SlotActivated += HandleSlotActivated;
            itemBar.OptionChosen += HandleOptionChosen;
            itemBar.SlotHovered += HandleSlotHovered;

            if (hoverTarget != null)
                hoverTarget.Changed += HandleHoverChanged;

            // The same dialogue the bar has, over two slots instead of nine: the screen says which
            // one was activated, this answers with what fits, and the screen says which was picked.
            if (mergeScreen != null)
            {
                mergeScreen.Opened += ShowMerge;
                mergeScreen.SlotActivated += HandleMergeSlotActivated;
                mergeScreen.OptionChosen += HandleMergeOptionChosen;
                mergeScreen.MergeRequested += PerformMerge;
            }

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

            // A restart hands over a fresh character owning none of what was on the bench.
            mergeLeft = null;
            mergeRight = null;
            mergeNotice = string.Empty;

            if (mergeScreen != null)
                mergeScreen.Close();

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
        /// <item>An asset already owned, unless its category may <see cref="CanStack"/> it.</item>
        /// <item>A category carrying one item per slot is full once its slots are - active items and
        /// artefacts. Copies count, so three of the same draught fill the three active slots as surely
        /// as three different ones do; the three artefacts fill theirs by being all there is.</item>
        /// </list>
        /// </summary>
        public bool CanTake(Item item, out string reason)
        {
            reason = string.Empty;

            // No box should hold nothing; there is nothing to say about it if one does.
            if (item == null)
                return false;

            if (items.Contains(item) && !CanStack(item.Slot))
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
        /// Whether a category may hold the same item more than once, which the player switches with
        /// <see cref="GameRules.StackDuplicateActiveItems"/>. Only active items can, switch or no
        /// switch, and the reason is what a second copy would be *for*: everywhere else the whole
        /// category shares a single slot, so it would have nowhere of its own to be and would do
        /// nothing while it sat there - a second identical sword is not a sword to swing, and a second
        /// amulet grants no more traits. A copy is only worth owning where owning it means a further
        /// use in hand.
        ///
        /// Which is why this names the category rather than asking <see cref="HoldsOneItem"/>: an
        /// artefact has a slot of its own too, but it is a unique find and there is never a second of
        /// it to own.
        /// </summary>
        private bool CanStack(SlotKind kind)
        {
            return kind == SlotKind.Active && gameRules != null && gameRules.StackDuplicateActiveItems;
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

            DropUnownedWeapon();

            ShowSlots();

            // An undo can take away an item the merge slots were holding - including one this very
            // merge produced - so what they show is asked of the inventory again.
            ShowMerge();
        }

        /// <summary>
        /// Puts down a weapon the character no longer owns and draws whatever it does own instead.
        /// The one rule for it, because two things now take a weapon out of the inventory: an undo
        /// rewinding a pickup, and a merge spending one as material.
        /// </summary>
        private void DropUnownedWeapon()
        {
            if (playerUnit == null || items.Contains(playerUnit.CurrentState.AttackAction))
                return;

            playerUnit.CurrentState.AttackAction = FirstOwned(SlotKind.Melee) as AttackActionData
                                                  ?? FirstOwned(SlotKind.Ranged) as AttackActionData;
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

        /// <summary>
        /// Remembers what the cursor is on and tells the one owner of that. The preview follows from
        /// <see cref="HoverTarget.Changed"/> rather than from here, so it is set once per change
        /// instead of being re-asserted every frame against the world's own hover.
        /// </summary>
        private void HandleSlotHovered(int slot)
        {
            hoveredSlot = slot;

            if (hoverTarget != null)
                hoverTarget.SetUiSlot(slot);
            else
                ShowHoverPreview();
        }

        private void HandleHoverChanged() => ShowHoverPreview();

        /// <summary>
        /// Puts what the hovered slot would cost into the character's action point bar, the way
        /// hovering a tile puts the walk there into it. Only an active item has anything to show - a
        /// weapon is drawn and a passive worn for nothing - so every other slot, and no slot at all,
        /// clears the preview instead.
        /// </summary>
        private void ShowHoverPreview()
        {
            if (playerUnit == null || !playerUnit.IsAlive)
                return;

            if (hoveredSlot >= 0 && EquippedIn(hoveredSlot) is ActiveItemData item)
                playerUnit.ActionExecutor.PlanItemAction(item);
            else
                playerUnit.ActionExecutor.ClearPreview();
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

                itemBar.SetSlotLook(i, LookOf(i));
                itemBar.SetSlotIcon(i, item != null ? item.Symbol : null);
                itemBar.SetSlotTooltip(i, item != null ? item.Tooltip : string.Empty);
                itemBar.SetSlotActive(i, IsInUse(i, item));
            }

            // The cursor may still be resting on a slot that has just changed hands - a potion drunk
            // out of it, a looted one dropped into it - and it will not enter it a second time.
            ShowHoverPreview();
        }

        /// <summary>
        /// What the bar draws a slot as whatever it holds: the symbol of its category for while it is
        /// empty, that category's colour, and whether the row breaks in front of it. Pushed on every
        /// refresh beside the item, because a slot has to say what it is for before anything has been
        /// found to put in it - and which category a slot stands for is only known here.
        /// </summary>
        private SlotLook LookOf(int slot)
        {
            if (slotIcons == null || !KindForSlot(slot, out var kind))
                return default;

            return new SlotLook(slotIcons.IconFor(kind), slotIcons.AccentFor(kind), StartsGroup(slot));
        }

        /// <summary>
        /// Brings what the slots hold in line with what the player owns. The slots hold *copies*, not
        /// identities - the same draught may sit in two of them - so this counts rather than compares:
        /// a slot is backed by the inventory only while the copies shown up to it are copies owned.
        /// Two passes, and both fall out of that one rule.
        ///
        /// <list type="number">
        /// <item>A slot showing a copy the inventory no longer covers is emptied: an undone pickup, a
        /// used-up potion, or the second of two slots once one of the pair has been drunk.</item>
        /// <item>An empty slot takes the first owned item of its kind that has a copy in no slot yet.
        /// So a looted bow turns up in its slot without having to be drawn, a second draught finds a
        /// slot of its own, and the draught beside a drunk one stays where it was.</item>
        /// </list>
        /// </summary>
        private void RefreshEquipped()
        {
            var inHand = playerUnit != null ? playerUnit.CurrentState.AttackAction : null;

            if (inHand != null && FirstSlotOf(inHand.Slot, out var weaponSlot))
                equippedBySlot[weaponSlot] = inHand;

            for (var slot = 0; slot < equippedBySlot.Length; slot++)
                if (equippedBySlot[slot] != null && CopiesShownUpTo(slot) > CountOwnedOf(equippedBySlot[slot]))
                    Fill(slot, null);

            for (var slot = 0; slot < equippedBySlot.Length; slot++)
                if (equippedBySlot[slot] == null)
                    Fill(slot, FirstUnshown(slot));
        }

        /// <summary>
        /// How many slots up to and including <paramref name="slot"/> show what it shows - which copy
        /// of that item this slot is, counted from one. The later of two slots showing a draught owned
        /// only once is the one that comes out above the count and so the one that gives it up.
        /// </summary>
        private int CopiesShownUpTo(int slot)
        {
            var item = equippedBySlot[slot];
            var copies = 0;

            for (var i = 0; i <= slot; i++)
                if (equippedBySlot[i] == item)
                    copies++;

            return copies;
        }

        /// <summary>How many copies of <paramref name="item"/> the player owns.</summary>
        private int CountOwnedOf(Item item)
        {
            var copies = 0;

            foreach (var owned in items)
                if (owned == item)
                    copies++;

            return copies;
        }

        /// <summary>How many slots are showing <paramref name="item"/>.</summary>
        private int CopiesShown(Item item)
        {
            var copies = 0;

            foreach (var shown in equippedBySlot)
                if (shown == item)
                    copies++;

            return copies;
        }

        /// <summary>
        /// Puts <paramref name="item"/> in <paramref name="slot"/>. A worn slot is filled through the
        /// wearer rather than written straight into the array: what it holds is in effect merely by
        /// being shown there, so its traits have to follow it in and out.
        /// </summary>
        private void Fill(int slot, Item item)
        {
            if (IsWorn(SlotKinds[slot]))
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

        /// <summary>
        /// The first owned item fitting <paramref name="slot"/> that the slots do not already show
        /// every copy of, or null. Owning two draughts and showing one means the second still wants a
        /// slot; owning one and showing it means it does not.
        /// </summary>
        private Item FirstUnshown(int slot)
        {
            if (!KindForSlot(slot, out var kind))
                return null;

            foreach (var item in items)
                if (item != null && item.Slot == kind && CopiesShown(item) < CountOwnedOf(item))
                    return item;

            return null;
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
        // stands for, how many slots it has, where the row breaks between two of them, what one
        // offers, what it holds, whether that is in effect, and what choosing it does. A further category is a further entry in the layout and
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
        /// Whether <paramref name="slot"/> opens a run of the row - where the bar breaks it, so that
        /// the three actives read as one thing rather than as three of nine. Read off the layout
        /// rather than authored beside it: a category's slots are contiguous there, so a run ending
        /// is a <see cref="GroupOf"/> changing between two slots.
        /// </summary>
        private static bool StartsGroup(int slot)
        {
            return slot > 0 && slot < SlotKinds.Length &&
                   GroupOf(SlotKinds[slot]) != GroupOf(SlotKinds[slot - 1]);
        }

        /// <summary>
        /// Which run of the row a category belongs to, which is the category itself but for the two
        /// weapons: they are one thing to the player - the weapon in hand, split only by what kind it
        /// is - and both are drawn from the same gesture for free, so they stand together rather than
        /// as two runs of one. Told apart by their accent, as every category is.
        /// </summary>
        private static SlotKind GroupOf(SlotKind kind)
        {
            return kind == SlotKind.Ranged ? SlotKind.Melee : kind;
        }

        /// <summary>
        /// Whether a category carries one item per slot rather than a shelf of them behind one slot.
        /// The whole difference between the two: one is chosen from a column, the other is simply
        /// there, and how many of it can be carried is how many slots the layout gives it.
        /// </summary>
        private static bool HoldsOneItem(SlotKind kind)
        {
            return kind == SlotKind.Active || kind == SlotKind.Artefact;
        }

        /// <summary>
        /// Whether a category is in effect merely by sitting in its slot, rather than waiting to be
        /// used or drawn. Both such categories are bundles of traits (<see cref="PassiveItem"/>), and
        /// this is what puts those traits on the character and takes them off again - which is also
        /// what makes an undone pickup undo the bonus with it.
        /// </summary>
        private static bool IsWorn(SlotKind kind)
        {
            return kind == SlotKind.Passive || kind == SlotKind.Artefact;
        }

        /// <summary>
        /// Everything owned that <paramref name="slot"/> offers - its whole category. Only a category
        /// sharing one slot is ever picked from, and those are exactly the ones that cannot hold the
        /// same asset twice (<see cref="CanStack"/>), so no entry here is ever a duplicate of another.
        /// Which slot an item ends up *sitting* in is not this question - that is
        /// <see cref="FirstUnshown"/>, which has to count copies.
        /// </summary>
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
                SlotKind.Passive or SlotKind.Artefact => true,
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

                case SlotKind.Artefact:
                    // Nothing to choose and nothing to give up: an artefact is already worn by being
                    // in its slot, and nothing else is ever offered that slot. Pressing its key is
                    // how one reads what it does, not how one uses it up.
                    break;
            }
        }

        /// <summary>
        /// Puts a worn item on and takes the previous one off: its traits leave the character's trait
        /// list and the new one's join it, which is all a passive item or an artefact is. Only one
        /// instance of each trait is removed, so one the blueprint grants as well survives taking the
        /// item off. An artefact slot only ever goes the one way in play - nothing is offered it after
        /// the artefact - but it still goes back the other when an undo takes the find away.
        /// </summary>
        private void Wear(int slot, Item item)
        {
            var previous = equippedBySlot[slot];

            if (previous == item)
                return;

            if (playerUnit != null)
            {
                var state = playerUnit.CurrentState;

                if (previous is PassiveItem worn)
                    foreach (var trait in worn.Traits)
                        state.RemoveTrait(trait);

                if (item is PassiveItem chosen)
                    foreach (var trait in chosen.Traits)
                        state.AddTrait(trait);
            }

            equippedBySlot[slot] = item;
        }

        // Everything below is the merge, and it is the same shape as the bar above it: the screen
        // is a pure view that knows two slot indices and a list of icons, this is the only thing
        // that knows they stand for items, and MergeRules is the only thing that knows what merging
        // costs in odds. Nothing here decides a rule and nothing there touches an inventory.

        /// <summary>
        /// Answers the screen with everything owned that fits the activated merge slot, starting the
        /// choice on what is already in it. The two sides ask different questions of an item - the
        /// left one is improved and has to be a weapon, the right one is taken apart and has to have
        /// traits to give - and both are <see cref="MergeRules"/>'.
        /// </summary>
        private void HandleMergeSlotActivated(int side)
        {
            var candidates = MergeCandidates(side);

            if (candidates.Count == 0)
            {
                mergeScreen.SetNotice(NothingToMergeNotice);

                return;
            }

            var options = new List<ItemOption>(candidates.Count);

            foreach (var item in candidates)
                options.Add(new ItemOption(item.Symbol, item.Tooltip, item.Title));

            mergeScreen.OpenPicker(side, options, candidates.IndexOf(ChosenFor(side)));
        }

        private void HandleMergeOptionChosen(int side, int option)
        {
            var candidates = MergeCandidates(side);

            if (option < 0 || option >= candidates.Count)
                return;

            if (side == MergeScreen.LeftSide)
                mergeLeft = candidates[option];
            else
                mergeRight = candidates[option];

            // How the last merge went stops being the news the moment the next one is being set up.
            mergeNotice = string.Empty;

            ShowMerge();
        }

        /// <summary>
        /// Everything owned that may stand on <paramref name="side"/>, minus whatever the other side
        /// is holding - an item cannot be merged into itself, and offering it on both sides only
        /// invites that. No entry is ever a duplicate of another: neither category stacks.
        /// </summary>
        private List<Item> MergeCandidates(int side)
        {
            var candidates = new List<Item>();
            var other = side == MergeScreen.LeftSide ? mergeRight : mergeLeft;

            foreach (var item in items)
            {
                if (item == null || item == other)
                    continue;

                bool fits = side == MergeScreen.LeftSide
                    ? MergeRules.CanBeImproved(item)
                    : MergeRules.CanBeConsumed(item);

                if (fits)
                    candidates.Add(item);
            }

            return candidates;
        }

        /// <summary>An item as one of the two merge slots draws it, or an empty slot for none.</summary>
        private void ShowMergeSlot(int side, Item item)
        {
            mergeScreen.SetSlot(side,
                item != null ? item.Symbol : null,
                item != null ? item.Title : string.Empty,
                item != null ? item.Tooltip : string.Empty);
        }

        private Item ChosenFor(int side)
        {
            return side == MergeScreen.LeftSide ? mergeLeft : mergeRight;
        }

        /// <summary>
        /// Pushes the two slots, the odds and whatever there is to say onto the screen - the merge's
        /// answer to <see cref="ShowSlots"/>. Called whenever either could have changed, including
        /// from behind: an undo, a merge or a drunk potion can take away an item that was sitting in
        /// a merge slot, so what is shown is re-checked against what is still owned every time.
        /// </summary>
        private void ShowMerge()
        {
            if (mergeScreen == null)
                return;

            if (!items.Contains(mergeLeft))
                mergeLeft = null;

            if (!items.Contains(mergeRight))
                mergeRight = null;

            ShowMergeSlot(MergeScreen.LeftSide, mergeLeft);
            ShowMergeSlot(MergeScreen.RightSide, mergeRight);

            bool possible = MergeRules.CanMerge(mergeLeft, mergeRight, out var reason);

            mergeScreen.SetChance(possible
                ? $"{MergeRules.SuccessPercent(mergeLeft, mergeRight)}% chance to succeed"
                : string.Empty);
            mergeScreen.SetMergeEnabled(possible);

            // While a pair cannot be merged, why not is the only thing worth saying; once it can,
            // the line is free for how the last one went.
            mergeScreen.SetNotice(possible ? mergeNotice : reason);
        }

        /// <summary>
        /// Spends the item on the right and rolls for it. The right item is gone either way - that
        /// is what the odds are a risk on - and on a success the weapon on the left is replaced by
        /// the copy <see cref="MergeRules.Combine"/> makes of it, carrying both sets of traits.
        ///
        /// Costs no action points and does not wait for the player's turn: like drawing a weapon,
        /// it is loadout. It is still <b>reported</b>, because unlike drawing a weapon it destroys
        /// something - the snapshot taken around it is what puts both originals back on an undo, and
        /// the merged copy is an ordinary item reference in that list, so a redo hands back the very
        /// same weapon rather than rolling for it again.
        /// </summary>
        private void PerformMerge()
        {
            if (playerUnit == null || !MergeRules.CanMerge(mergeLeft, mergeRight, out _))
                return;

            if (!items.Contains(mergeLeft) || !items.Contains(mergeRight))
                return;

            var left = mergeLeft;
            var right = mergeRight;

            // Read before anything is spent: the odds are one over what the weapon would end up
            // carrying, so they have to be asked of the pair as it still stands.
            bool succeeded = UnityEngine.Random.value < MergeRules.SuccessChance(left, right);
            bool wasDrawn = playerUnit.CurrentState.AttackAction == left;

            items.Remove(right);
            mergeRight = null;

            if (succeeded)
            {
                var merged = MergeRules.Combine(left, right);

                // In place, so the improved weapon keeps the position the original had in the
                // inventory and turns up in the same slot rather than at the end of its category.
                items[items.IndexOf(left)] = merged;
                mergeLeft = merged;

                // The weapon in hand is a reference, not a slot: a merge while it is drawn has to
                // put the copy in hand, or the character would go on swinging the one it replaced.
                if (wasDrawn)
                    playerUnit.CurrentState.AttackAction = merged;
            }

            mergeNotice = succeeded ? MergeSucceededNotice : MergeFailedNotice;

            DropUnownedWeapon();
            ShowSlots();
            ShowMerge();

            // Last, once the board and the bar are whole: the after-snapshot is taken on this.
            ActionReporter.Report(ActionReport.Merge(playerUnit));
        }

        /// <summary>
        /// Uses an active item on the character and uses it up. The item leaves the inventory
        /// *before* the action runs: the executor announces itself once it is done, and the snapshot
        /// taken at that moment has to already show the item gone, or an undo would hand it back
        /// twice. One that could not be afforded goes back where it was.
        ///
        /// One copy leaves, not the item: with several of the same draught carried, which of them was
        /// drunk is not a question - they are the same asset, and it is <see cref="RefreshEquipped"/>
        /// that decides which slot goes empty over it.
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
