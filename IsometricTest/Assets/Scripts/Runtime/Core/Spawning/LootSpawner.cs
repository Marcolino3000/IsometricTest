using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    /// <summary>
    /// Makes every box of the match and owns them, the way the other spawners own their tiles and
    /// units. Taking one happens here too: the boxes are its, so the inventory only has to be told
    /// what was found.
    ///
    /// A box's kind (<see cref="LootboxType"/>) says what it looks like, what it costs and how many
    /// of it there are. What may be *inside* it is authored the other way round - every
    /// <see cref="Item"/> names the kind it turns up in - so the loot goes and asks the items rather
    /// than being handed a list; see <see cref="CollectItems"/>.
    ///
    /// A kind comes from one of the two ways a box can turn up (<see cref="LootboxSource"/>), never
    /// from some of each. A scattered kind is put down over the map when it is generated; a dropped
    /// one is made at the same moment and simply waits, to be handed to the tile of an opposing unit
    /// as it falls - one box per unit, so there are always as many drops as there are units to fall.
    /// Making them up front is what keeps a drop undoable and repeatable - its contents are rolled
    /// once, so redo hands back the very item undo took away.
    /// </summary>
    public class LootSpawner : MonoBehaviour
    {
        
        [Header("Settings")]
        [SerializeField] private Vector3 GroundClearance = new(0, 0.25f, 0);

        [Header("References")]
        [SerializeField] private LootSpawnerSettings settings;

        [Header("Debug")]
        // Every box made for this match, whatever state it is in - lying about, taken, or waiting for I've gone
        // a unit to fall. One list rather than one per state: which of the three a box is in is the
        // box's own (see LootboxState), the way Unit.IsAlive rather than list membership is what says
        // a unit is in play.
        [SerializeField] private List<Lootbox> lootboxes = new();

        // The order dropped boxes are left behind in, shuffled once when they are made. The next drop
        // is the first one in here still pending, so nothing about the drops has to be recorded: undo
        // puts a box back to pending and it is next again, and redo takes that same one out.
        private readonly List<Lootbox> dropOrder = new();

        // Every item that named a kind of box, filed under that kind and its own category - the loot
        // table, gathered from the items themselves rather than authored here. Rebuilt on every spawn
        // so an item reassigned in the inspector takes effect on the next match.
        private readonly Dictionary<LootboxType, List<Item>[]> pools = new();

        // Which ring each box waiting on one belongs to. Only scattered boxes are in here, and only
        // while the rings hold them back: a drop belongs to no ring, it lands where its unit fell.
        // Never changes once a box is made, so there is nothing here to snapshot - whether it is on
        // the board is the box's own state, which is recorded.
        private readonly Dictionary<Lootbox, int> zoneOfBox = new();

        // One bag per kind of box and per category within it, so each category's box count can be
        // honoured on its own and two kinds offering the same item do not deal from one pile. Each is
        // a shuffled copy of the matching pool, refilled only once it runs out: rolling every box
        // independently would keep handing out the same one out of a handful.
        private readonly Dictionary<LootboxType, List<Item>[]> bags = new();

        private TileSpawner tileSpawner;
        private UnitSpawner unitSpawner;
        private ItemManager itemManager;
        private GameStateManager gameStateManager;
        private GameRules rules;

        /// <summary>Every box of this match, lying about, taken or pending. Used to snapshot the board.</summary>
        public IReadOnlyList<Lootbox> AllSpawnedLootboxes => lootboxes;

        /// <summary>
        /// Takes the box the player's character is standing on - the pressed way of taking one, see
        /// <see cref="HandleUnitEnteredTile"/> for walking over it. A turn action like any other: it
        /// costs what <see cref="Lootbox.Cost"/> asks, so it only works on the character's own turn
        /// and only while it can still afford it, and it announces itself so the history can undo it.
        /// </summary>
        public void TryPickup()
        {
            // A removed unit is only hidden, so IsAlive is the in-play test rather than a null check.
            var unit = unitSpawner.PlayerUnit;

            if (!unit.IsAlive || unit.CurrentState.Team != gameStateManager.State.Team)
                return;

            var lootbox = unit.CurrentState.Position.Lootbox;

            if (lootbox == null)
                return;

            // The other condition of the action: the points it asks for, which is none at all while
            // walking over a box is enough to have it.
            var cost = lootbox.Cost;

            if (unit.CurrentState.ActionPoints < cost)
                return;

            if (!TryTake(unit, lootbox))
                return;

            unit.CurrentState.ActionPoints -= cost;

            ActionReporter.Report(ActionReport.Pickup(unit));
        }

        /// <summary>
        /// Takes the box a unit has just stepped onto, while
        /// <see cref="GameRules.AutoCollectLootboxes"/> says walking over one is enough. Every tile of
        /// a move arrives here, so a box on the way is picked up rather than only the one the path
        /// ends on.
        ///
        /// Free and unannounced on purpose. It costs no action points, and it happens *inside* a
        /// move - one step of one - so the move's own report is what records it: that report's
        /// after-snapshot has the box gone and the item owned, and one undo takes back the step and
        /// the find together. A report of its own here would capture a move half walked, its points
        /// not yet paid, since the executor charges the whole plan at the end.
        ///
        /// Only the player's character: nobody else has an inventory to put anything in.
        /// </summary>
        private void HandleUnitEnteredTile(Unit unit)
        {
            if (rules == null || !rules.AutoCollectLootboxes || unit != unitSpawner.PlayerUnit)
                return;

            var lootbox = unit.CurrentState.Position.Lootbox;

            if (lootbox != null)
                TryTake(unit, lootbox);
        }

        /// <summary>
        /// Leaves a box behind on the tile of a unit that has just fallen - what a dropped
        /// <see cref="LootboxType"/> is for. Nothing is made here: the drop was rolled and built with
        /// all the other boxes, so what a given match yields is fixed the moment it starts and a
        /// redone kill hands back exactly what the undone one took away.
        ///
        /// Unannounced, for the same reason walking over a box is: the fall happens inside the attack
        /// that caused it, and the attack reports itself afterwards - so the drop is already on the
        /// board in that report's after-snapshot, and one undo takes back the blow and the spoils
        /// together.
        /// </summary>
        private void HandleUnitRemoved(Unit unit)
        {
            // Only what the opponents leave behind. The player commands a single character, and its
            // fall ends the match rather than furnishing it.
            if (unit == null || unit.CurrentState.Team == Team.Player)
                return;

            var tile = unit.CurrentState.Position;

            // A tile already holding a box keeps the one it has - two cannot lie on one tile - and
            // ground a box may not lie on leaves nothing behind either. Neither loses the drop: it
            // stays pending and the next unit to fall leaves it instead.
            if (!CanLieOn(tile) || tile.Lootbox != null)
                return;

            var drop = NextPendingDrop();

            if (drop != null)
                Place(drop, LootboxState.InPlay, tile);
        }

        /// <summary>
        /// Hands what a box holds to the unit standing on it and takes the box off the board, or
        /// leaves both where they are and says why. The two ways of taking a box - pressing for it
        /// and walking over it - differ in what they cost and what they announce, not in this.
        /// </summary>
        private bool TryTake(Unit unit, Lootbox lootbox)
        {
            // A box holding something the character cannot take is left where it lies, unopened and
            // costing nothing, so it can be come back for once a slot is free or the copy already
            // carried has been used up. The player is told why, since an unopened box looks exactly
            // like one that was never walked over. The reason comes from the inventory: the rule for
            // what can be taken is the inventory's, and this must not have a second opinion.
            if (!itemManager.CanTake(lootbox.Content, out var reason))
            {
                if (!string.IsNullOrEmpty(reason))
                    unit.ShowNotice(reason);

                return false;
            }

            itemManager.Pickup(lootbox.Content);
            TakeLootbox(lootbox);

            return true;
        }

        /// <summary>
        /// Takes a box off the board. It is kept aside rather than destroyed - see
        /// <see cref="LootboxState.Taken"/> - so undo can put it back on the tile it remembers.
        /// Does nothing for one already gone or one that was never put down.
        /// </summary>
        public void TakeLootbox(Lootbox lootbox)
        {
            if (lootbox == null || !lootbox.IsInPlay)
                return;

            Place(lootbox, LootboxState.Taken, lootbox.Tile);
        }

        /// <summary>
        /// Puts a box back into a state a snapshot recorded - which is a state *and* a tile, since a
        /// dropped box lies wherever a unit happened to fall and undoing that kill has to take it
        /// back off that tile.
        /// </summary>
        public void RestoreLootbox(Lootbox lootbox, LootboxState state, Tile tile)
        {
            if (lootbox != null)
                Place(lootbox, state, tile);
        }

        #region Helpers

        /// <summary>
        /// The one place a box changes state, because where it stands and whether it is on the board
        /// are the same question: a scattered box is put down here, a drop arrives here, a taken one
        /// is set aside here and undo brings it back through here.
        ///
        /// Which is also why the ground is asked here rather than at each caller: a box is taken by
        /// standing on it, so it may only ever lie where a unit can stand (<see cref="CanLieOn"/>).
        /// A box refused a tile is left where it was - a drop stays pending and the next unit to fall
        /// leaves it instead - rather than being put somewhere nobody could ever reach it.
        /// </summary>
        private void Place(Lootbox lootbox, LootboxState state, Tile tile)
        {
            if (state == LootboxState.InPlay && !CanLieOn(tile))
            {
                Debug.LogWarning($"{lootbox.name} was not put down: a box cannot lie on ground that " +
                                 "cannot be walked on, since it is taken by standing on it.", lootbox);
                return;
            }

            lootbox.SetState(state, tile);

            if (tile != null)
                lootbox.transform.position = tileSpawner.GridIndexToWorldPosition(tile.Position)
                                             + Vector3.up * tile.HeightOffset + GroundClearance;
        }

        /// <summary>The next box a fallen unit leaves behind, or null once they have all been left.</summary>
        private Lootbox NextPendingDrop()
        {
            foreach (var lootbox in dropOrder)
                if (lootbox != null && lootbox.State == LootboxState.Pending)
                    return lootbox;

            return null;
        }

        private void ClearLootboxes()
        {
            foreach (var lootbox in lootboxes)
            {
                if (lootbox == null)
                    continue;

                // A respawn rebuilds the grid first, so the tile a box remembers may already be gone.
                if (lootbox.Tile != null && lootbox.Tile.Lootbox == lootbox)
                    lootbox.Tile.SetLootbox(null);

                Destroy(lootbox.gameObject);
            }

            lootboxes.Clear();
            dropOrder.Clear();
            zoneOfBox.Clear();
            bags.Clear();
        }

        /// <summary>
        /// Builds the loot table by reading it off the items. Every <see cref="Item"/> authored
        /// anywhere under a Resources folder is asked which kind of box it belongs in
        /// (<see cref="Item.FoundIn"/>) and filed under that kind and its own category; one naming
        /// none is simply never found, which is what the starting weapon wants.
        ///
        /// Loaded rather than listed because the assignment lives on the item: nothing may hold a
        /// second copy of which items are in play, or the two would drift apart the first time an
        /// item was added and only half wired up.
        /// </summary>
        private void CollectItems()
        {
            pools.Clear();

            foreach (var item in Resources.LoadAll<Item>(string.Empty))
            {
                if (item == null || item.FoundIn == null || item.Slot == SlotKind.None)
                    continue;

                if (!pools.TryGetValue(item.FoundIn, out var byKind))
                    pools[item.FoundIn] = byKind = new List<Item>[(int)SlotKind.None];

                (byKind[(int)item.Slot] ??= new List<Item>()).Add(item);
            }
        }

        /// <summary>Everything assigned to <paramref name="type"/> in <paramref name="kind"/>.</summary>
        private IReadOnlyList<Item> PoolFor(LootboxType type, SlotKind kind)
        {
            if (pools.TryGetValue(type, out var byKind) && byKind[(int)kind] != null)
                return byKind[(int)kind];

            return Array.Empty<Item>();
        }

        /// <summary>
        /// Builds a box of <paramref name="type"/> around <paramref name="content"/>. It is not on
        /// the board yet: putting it somewhere is <see cref="Place"/>, which is what a drop waits for.
        /// </summary>
        private Lootbox CreateLootbox(LootboxType type, Item content)
        {
            var lootbox = Instantiate(settings.LootboxPrefab, transform);
            lootbox.name = $"{type.Title} {lootboxes.Count}";
            lootbox.Setup(type, content, settings.OrderInLayer, settings.Scale, rules);

            lootboxes.Add(lootbox);

            return lootbox;
        }

        /// <summary>
        /// Whether a box may lie on <paramref name="tile"/> at all: it is taken by standing on it, so
        /// it may only ever be put on ground a unit can walk onto. A box on a mountain could never be
        /// reached, and would hold up the win for collecting all the loot for as long as an opponent
        /// was left standing.
        /// </summary>
        private static bool CanLieOn(Tile tile)
        {
            return tile != null && tile.IsPassable;
        }

        /// <summary>
        /// The tiles a box may lie on, in randomized order: walkable ground with nobody standing on
        /// it. Impassable terrain is ruled out by <see cref="CanLieOn"/>, and an occupied tile because
        /// the unit spawned there would be standing on free loot.
        ///
        /// Shuffled here rather than where they are handed out, so that each kind's ring
        /// (<see cref="OrderByRing"/>) is scattered along its own length by one shuffle they all share.
        /// </summary>
        private List<Tile> GetShuffledLootTiles()
        {
            var candidates = new List<Tile>();

            foreach (var tile in tileSpawner.AllTiles)
            {
                if (CanLieOn(tile) && !tile.IsOccupied)
                    candidates.Add(tile);
            }

            Shuffle(candidates);

            return candidates;
        }

        /// <summary>
        /// The tiles left, ordered by how far each misses the ring this entry names: its ground
        /// first, then the nearest outside it. Ordered rather than filtered, exactly as a spawn zone
        /// is, so a ring walled off by mountains or already taken up by the entries before it spills
        /// over its border instead of losing its boxes. An entry naming no ring is left in the order
        /// it came in, which scatters it over the whole map.
        ///
        /// The list handed in was shuffled once, and the sort is stable, so that shuffle survives as
        /// the random tiebreak within a ring - the tier lands somewhere else along its ring each
        /// match while staying at its distance.
        /// </summary>
        private List<Tile> OrderByRing(List<Tile> tiles, LootboxAmount entry)
        {
            if (entry == null || !entry.HasZone)
                return tiles;

            return tiles
                .OrderBy(tile => ZoneRules.DistanceOutside(entry.Zone, tile.Position))
                .ToList();
        }

        /// <summary>
        /// How many boxes the fallen leave behind: one per unit, so exactly as many as there are
        /// units to fall. That is the whole of how many drops a match holds - a dropped kind is
        /// never asked what it wants. More than this and a box nobody could ever reach would sit
        /// pending forever, holding up the win for collecting all the loot; fewer and a unit would
        /// fall leaving nothing.
        /// </summary>
        private int CountDroppableUnits()
        {
            var count = 0;

            // Every opponent this match will field, in play or still waiting for its ring to be
            // walked into: a unit that arrives later falls like any other and has to have a box for
            // it, and asking only for the ones already on the board would leave the last arrivals
            // with nothing to leave behind.
            foreach (var unit in unitSpawner.AllSpawnedUnits)
                if (unit != null && unit.CurrentState.Team != Team.Player)
                    count++;

            return count;
        }

        /// <summary>
        /// How many boxes each kind in the settings makes. A scattered kind makes what it asks for;
        /// a dropped one makes no number of its own but one box per unit there is to fall, since
        /// every unit leaves one behind. Two dropped kinds split those units evenly - the only sense
        /// in which how many drops there are is authored at all.
        /// </summary>
        private int[] CountPerType()
        {
            var counts = new int[settings.Boxes.Count];
            var dropping = new List<int>();

            for (var index = 0; index < settings.Boxes.Count; index++)
            {
                var entry = settings.Boxes[index];

                if (entry?.Type == null)
                    continue;

                if (entry.Type.Source == LootboxSource.DroppedByUnits)
                    dropping.Add(index);
                else
                    counts[index] = entry.Count;
            }

            if (dropping.Count == 0)
                return counts;

            // Equal shares, handed out by the same rule the categories are shared out with, so the
            // parts add back up to the units exactly however they round.
            var shares = new int[dropping.Count];

            for (var i = 0; i < shares.Length; i++)
                shares[i] = 1;

            var split = LootboxType.Distribute(CountDroppableUnits(), shares);

            for (var i = 0; i < dropping.Count; i++)
                counts[dropping[i]] = split[i];

            return counts;
        }

        /// <summary>
        /// Puts down whatever of ring <paramref name="index"/> is still waiting - what answers
        /// <see cref="Runtime.Gameplay.Global.ZoneWatcher.ZoneReached"/>, alongside the units. Safe
        /// to call for a ring already lying open, and meant to be: it is said on every step, so a
        /// ring an undo has emptied fills again when the character walks back into it.
        ///
        /// The ground is chosen now rather than at the start, so a box never lands under a unit that
        /// arrived with it.
        /// </summary>
        public void ReleaseZone(int index)
        {
            var waiting = new List<Lootbox>();

            foreach (var pair in zoneOfBox)
                if (pair.Value == index && pair.Key != null && pair.Key.IsPending)
                    waiting.Add(pair.Key);

            if (waiting.Count == 0)
                return;

            var candidates = GetShuffledLootTiles();

            candidates = candidates
                .OrderBy(tile => ZoneRules.DistanceOutside(index, tile.Position))
                .ToList();

            var placed = 0;

            foreach (var lootbox in waiting)
            {
                if (placed >= candidates.Count)
                    break;

                Place(lootbox, LootboxState.InPlay, candidates[placed++]);
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            // Fisher-Yates
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The bag <paramref name="kind"/> is dealt from for this type, created on first use.</summary>
        private List<Item> BagFor(LootboxType type, SlotKind kind)
        {
            if (!bags.TryGetValue(type, out var typeBags))
                bags[type] = typeBags = new List<Item>[(int)SlotKind.None];

            return typeBags[(int)kind] ??= new List<Item>();
        }

        /// <summary>The next item of <paramref name="kind"/> this type offers, or null if it lists none.</summary>
        private Item TakeItem(LootboxType type, SlotKind kind)
        {
            var bag = BagFor(type, kind);

            if (bag.Count == 0)
                RefillBag(bag, type, kind);

            if (bag.Count == 0)
                return null;

            var index = bag.Count - 1;
            var item = bag[index];
            bag.RemoveAt(index);

            return item;
        }

        private void RefillBag(List<Item> bag, LootboxType type, SlotKind kind)
        {
            bag.AddRange(PoolFor(type, kind));
            Shuffle(bag);
        }

        /// <summary>
        /// What <paramref name="total"/> boxes of this kind hold, one item at a time: the categories
        /// in turn, each getting the share of the total its percentage asks for. Lazy on purpose -
        /// a caller that runs out of tiles stops asking, and nothing is drawn from the bag that no
        /// box will hold.
        /// </summary>
        private IEnumerable<Item> RollContents(LootboxType type, int total)
        {
            var counts = type.CategoryCounts(total);

            for (var kind = 0; kind < counts.Length; kind++)
            {
                var wanted = counts[kind];

                for (var i = 0; i < wanted; i++)
                {
                    var content = TakeItem(type, (SlotKind)kind);

                    // No item of this category named this kind of box, so no box of it can be
                    // filled. The item is what says where it belongs, so that is where to look.
                    if (content == null)
                    {
                        Debug.LogWarning($"{type.name} asks for {wanted} {(SlotKind)kind} box(es), " +
                                         "but no item of that category names it in FoundIn.", type);
                        break;
                    }

                    yield return content;
                }
            }
        }

        #endregion

        #region Setup

        public void Setup(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, ItemManager itemManagerArg,
            GameStateManager gameStateManagerArg, InputHandler inputHandler, GameRules gameRules)
        {
            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            itemManager = itemManagerArg;
            gameStateManager = gameStateManagerArg;
            rules = gameRules;

            inputHandler.InteractPressed += TryPickup;
            inputHandler.RightClicked += TryPickup;

            // Both ways of taking a box stay wired whichever the rules allow: the switch is live, so
            // it may be flipped mid-match, and pressing for a box that has already been walked over
            // simply finds nothing there.
            unitSpawner.UnitEnteredTile += HandleUnitEnteredTile;

            // What a fallen unit leaves behind. Announced by the spawner that owns the units, so a
            // unit needs no more idea of what it is worth than it has of what it is standing on.
            unitSpawner.UnitRemoved += HandleUnitRemoved;
        }

        /// <summary>
        /// Makes a fresh set of boxes: the scattered ones straight onto the map, the dropped ones
        /// aside to wait for a unit to fall. Runs after the units are placed, which is why the
        /// Initiator calls it last in its spawning step - a scattered box must not end up under
        /// somebody, and how many drops are worth making is how many units there are to fall.
        ///
        /// The loot table is read off the items first (<see cref="CollectItems"/>), so an item moved
        /// to another kind of box in the inspector is in the right one from the next match on.
        /// </summary>
        [ContextMenu("Spawn Lootboxes")]
        public void SpawnLootboxes()
        {
            ClearLootboxes();

            if (settings == null || settings.LootboxPrefab == null)
            {
                Debug.LogWarning($"No lootbox prefab set in {nameof(LootSpawnerSettings)}.", settings);
                return;
            }

            if (settings.Boxes == null || settings.Boxes.Count == 0)
            {
                Debug.LogWarning($"No boxes listed in {nameof(LootSpawnerSettings)}.", settings);
                return;
            }

            CollectItems();

            var tiles = GetShuffledLootTiles();
            var counts = CountPerType();

            // One kind at a time, and within a kind one category at a time, so each gets the number
            // of boxes it was asked for. A kind is also placed as a whole because its ring is its
            // own: the tiles it takes are the ones nearest its distance from the middle of the map,
            // and what it takes is gone for the kinds after it.
            for (var index = 0; index < settings.Boxes.Count; index++)
            {
                var entry = settings.Boxes[index];
                var type = entry?.Type;
                var total = counts[index];

                if (type == null || total <= 0)
                    continue;

                // Boxes were asked for with nothing to put in them. The percentages are what shares
                // them out, so a kind that authored none can fill none of them.
                if (type.TotalPercent <= 0)
                {
                    Debug.LogWarning($"{type.name} asks for {total} box(es) but sets no category " +
                                     "percentage, so none of them can be filled.", type);
                    continue;
                }

                var contents = new List<Item>();

                foreach (var content in RollContents(type, total))
                    contents.Add(content);

                // The categories are rolled in turn, and a ring's tiles are handed out in order, so
                // an unshuffled list would lay every melee box along the ring's inner edge. Shuffled
                // for the drops too: the first kill leaves the kind's authored mix rather than
                // whichever category was listed first.
                Shuffle(contents);

                // A dropped kind is not put anywhere - it waits for a unit to fall, and lands
                // wherever that unit happened to be standing, which is no tier's business.
                if (type.Source == LootboxSource.DroppedByUnits)
                {
                    foreach (var content in contents)
                        dropOrder.Add(CreateLootbox(type, content));

                    continue;
                }

                // Held back until its ring is walked into, when the rings say so and this entry
                // names one. The box is made and filled here all the same - what a match yields is
                // settled before the first turn either way - and only its arrival waits, which is
                // what lets undo take that arrival back without any history code.
                if (ZoneRules.SpawnOnEntry && entry.HasZone)
                {
                    foreach (var content in contents)
                        zoneOfBox[CreateLootbox(type, content)] = entry.Zone;

                    continue;
                }

                var candidates = OrderByRing(tiles, entry);
                var placed = 0;

                foreach (var content in contents)
                {
                    // Nothing left to scatter onto. The box is not made at all rather than left
                    // pending: a pending box no fall will ever place could never be collected.
                    if (placed >= candidates.Count)
                        break;

                    var tile = candidates[placed++];

                    Place(CreateLootbox(type, content), LootboxState.InPlay, tile);
                    tiles.Remove(tile);
                }
            }

            // Shuffled once more, so that two dropped kinds arrive mixed rather than one kind's
            // boxes and then the other's. Fixed from here on, which is what lets undo and redo agree
            // about which box a fall leaves behind.
            Shuffle(dropOrder);
        }

        #endregion
    }
}
