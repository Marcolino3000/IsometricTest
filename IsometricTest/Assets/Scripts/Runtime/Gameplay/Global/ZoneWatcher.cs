using System.Collections.Generic;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Watches which ring of the map the player's character stands in and says so the first time it
    /// reaches a new one. It asks nothing of the zones but where a tile lies - what a zone is worth
    /// is the spawners' business, settled when the board was built - so this is only the moment the
    /// player is told about it.
    ///
    /// It listens on <see cref="UnitSpawner.UnitEnteredTile"/>, the same seam the loot takes: the
    /// spawner that owns the units announces an arrival, so nothing about zones has to be threaded
    /// into a unit. Only the character's arrivals count; an opponent crossing a ring is nobody's
    /// news, and a snapshot restore never passes through there at all - undo puts the board back,
    /// it does not walk into it.
    ///
    /// Which rings have been announced is deliberately <b>not</b> in <c>GameSnapshot</c>, exactly as
    /// a first find is not: stepping back over a border and crossing it again is the same crossing,
    /// not a second one. Where the character actually stands is never stored either - it is asked of
    /// its tile, so an undo needs to move nothing here.
    /// </summary>
    public class ZoneWatcher : MonoBehaviour
    {
        private UnitSpawner unitSpawner;
        private AnnouncementScreen announcements;

        // The rings already announced this match. Outside the snapshot on purpose - see above.
        private readonly HashSet<int> announced = new();

        /// <summary>
        /// Which ring the character stands in, or -1 while there are none. Derived from its tile
        /// rather than remembered, so nothing has to put it back after an undo.
        /// </summary>
        public int CurrentZone => ZoneRules.IndexAt(PlayerTile);

        private Tile PlayerTile
        {
            get
            {
                var player = unitSpawner != null ? unitSpawner.PlayerUnit : null;

                return player != null && player.IsAlive ? player.CurrentState.Position : null;
            }
        }

        public void Setup(UnitSpawner spawner, AnnouncementScreen screen)
        {
            unitSpawner = spawner;
            announcements = screen;

            unitSpawner.UnitEnteredTile += HandleUnitEnteredTile;
        }

        private void OnDestroy()
        {
            if (unitSpawner != null)
                unitSpawner.UnitEnteredTile -= HandleUnitEnteredTile;
        }

        /// <summary>
        /// Starts a match: the ring the character spawns in counts as reached, since it was never
        /// entered. Called after the board is built, and again on a restart - a fresh character
        /// spawns somewhere else and every ring is news again.
        /// </summary>
        public void Begin(Unit player)
        {
            announced.Clear();
            if (announcements != null)
                announcements.Hide();

            var zone = ZoneRules.IndexAt(player != null ? player.CurrentState.Position : null);

            if (zone >= 0)
                announced.Add(zone);
        }

        private void HandleUnitEnteredTile(Unit unit)
        {
            if (unitSpawner == null || unit != unitSpawner.PlayerUnit)
                return;

            var zone = ZoneRules.IndexAt(unit.CurrentState.Position);

            if (zone < 0 || !announced.Add(zone))
                return;

            if (announcements != null)
                announcements.Show(ZoneRules.HeadlineOf(zone), ZoneRules.DetailOf(zone));
        }
    }
}
