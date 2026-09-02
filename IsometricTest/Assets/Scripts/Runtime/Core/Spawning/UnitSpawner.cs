using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using UI;
using UnityEngine;

namespace Runtime.Core.Spawning
{
    public class UnitSpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private List<Unit> units;

        // Units that were removed from play. They are kept (hidden) rather than destroyed so undo can
        // put them back on the board; a respawn clears them out for good.
        [SerializeField] private List<Unit> removedUnits = new();

        // The player's single character, held from the moment it spawns so nothing has to scan the
        // board for it. Survives its own death: a removed unit is only hidden, and undo restores this
        // very object, so ask IsAlive rather than expecting null.
        [SerializeField] private Unit playerUnit;

        public IReadOnlyList<Unit> AllUnits => units;

        /// <summary>The player's character. Null only before the first spawn.</summary>
        public Unit PlayerUnit => playerUnit;

        /// <summary>Every unit of this match, in play or removed. Used to snapshot the board.</summary>
        public IEnumerable<Unit> AllSpawnedUnits => units.Concat(removedUnits);

        /// <summary>
        /// A unit has arrived on a tile - once per step of a move, since a move is one action per
        /// tile stepped on. Announced by the spawner that owns the units rather than by the unit
        /// itself, so whoever cares what lies on the ground (the loot) needs no reference handing
        /// down into every unit. Putting a unit back from a snapshot is not an arrival and is
        /// silent: undo restores what walking over something did, it does not do it again.
        /// </summary>
        public event Action<Unit> UnitEnteredTile;

        /// <summary>
        /// A unit has fallen - announced by the spawner that owns the units, for the same reason an
        /// arrival is: whoever cares what a death leaves behind (the loot) needs no reference handing
        /// down into every unit. Restoring a snapshot takes units off the board without going
        /// through here on purpose - undo puts back what a death did, it does not do it again.
        /// </summary>
        public event Action<Unit> UnitRemoved;

        /// <summary>Says a unit has just been placed on a tile. Called by the unit that moved.</summary>
        public void NotifyEnteredTile(Unit unit)
        {
            UnitEnteredTile?.Invoke(unit);
        }

        /// <summary>Says a unit has just fallen. Called by the unit that was removed.</summary>
        public void NotifyRemoved(Unit unit)
        {
            UnitRemoved?.Invoke(unit);
        }

        [Header("References")]
        [SerializeField] private UnitSpawnerSettings settings;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private FogOfWar fogOfWar;

        // Where a unit's state is bundled for whoever asks about a whole team. The spawner owns the
        // units' lifetime, so it is what says which ones are on the board - but it does not compute
        // the aggregate itself any more, and nothing here pushes one into turn state.
        [SerializeField] private UnitStateManager unitStateManager;

        // Handed to every unit it spawns: whether a unit wears capability badges and whether it
        // shows a threat zone are match rules, and a unit is where both are drawn.
        [SerializeField] private GameRules gameRules;

        // Handed on to every unit as well, and kept apart from the rules on purpose: how fast a
        // unit is drawn moving decides nothing about the match.
        [SerializeField] private AnimationSettings animationSettings;

        // The HUD rows the character's health and points are drawn in, over the item slots. Only
        // its own: an enemy's numbers stay over the enemy, and there is one HUD.
        private PlayerVitals playerVitals;

        // Which ring each opponent belongs to. Never changes once it is spawned, so there is nothing
        // here for a snapshot to put back: what *does* change - whether it has arrived - is the unit
        // being in play at all, which the snapshot already records.
        private readonly Dictionary<Unit, int> zoneOfUnit = new();

        /// <summary>
        /// Takes a unit out of play. It is hidden instead of destroyed - that keeps it, and every
        /// reference to it, intact so <see cref="RestoreUnit"/> can bring it back when the action that
        /// killed it is undone.
        /// </summary>
        public void RemoveUnit(Unit unit)
        {
            if (unit == null || !units.Remove(unit))
                return;

            unitStateManager.Untrack(unit);
            removedUnits.Add(unit);
            unit.SetInPlay(false);

            // Nothing may go on pointing at a unit that is off the board: it is hidden rather than
            // destroyed, so a stale selection or hover survives its death looking valid.
            selector.DropUnit(unit);
        }

        /// <summary>Puts a previously removed unit back into play. Does nothing for a living unit.</summary>
        public void RestoreUnit(Unit unit)
        {
            if (unit == null || !removedUnits.Remove(unit))
                return;

            units.Add(unit);
            unit.SetInPlay(true);
            unitStateManager.Track(unit);
        }

        /// <summary>
        /// Puts the opposition on the board: first what each ring of the map fields, inside that
        /// ring, then whatever belongs to no ring at all, in the band along the rim as before.
        ///
        /// The two lists are not two ways of authoring one thing. A zone's roster is who guards that
        /// distance - which is how an opponent gets stronger the further out it stands, since a ring
        /// lists its own blueprints - while <see cref="UnitSpawnerSettings.OpponentUnits"/> is the
        /// roster of a map that is not divided into rings at all. A match authoring zones leaves it
        /// empty.
        /// </summary>
        private void SpawnOpponentUnits()
        {
            var index = 0;
            var onEntry = ZoneRules.SpawnOnEntry;

            foreach (var roster in settings.OpponentUnits)
            {
                if (roster == null)
                    continue;

                // One that belongs to no ring takes the spawn band along the rim and is placed at
                // once: there is no arrival to wait for. One that belongs to a ring is placed
                // inside it, and waits for the character to walk in when the rings say so.
                var zone = roster.HasZone ? ZoneRules.Settings.At(roster.Zone) : null;
                var hold = onEntry && zone != null;

                for (var i = 0; i < roster.Amount; i++)
                {
                    var unit = SpawnUnit(Team.Opponent, roster.Blueprint, index++, zone, hold);

                    if (unit != null && zone != null)
                        zoneOfUnit[unit] = roster.Zone;
                }
            }
        }

        /// <summary>
        /// Brings on whatever of ring <paramref name="index"/> is still waiting - what answers
        /// <see cref="Runtime.Gameplay.Global.ZoneWatcher.ZoneReached"/>. Safe to call for a ring
        /// that is already there, and meant to be: it is said on every step, so a ring an undo has
        /// emptied fills again the next time the character walks into it.
        ///
        /// A held-back unit is one that has never been placed. It is made with all the others, so
        /// what a match fields is settled before the first turn, and only its arrival waits - which
        /// is why undo needs nothing new: the snapshot already records whether a unit is in play and
        /// which tile it stands on.
        /// </summary>
        public void ReleaseZone(int index)
        {
            // Copied, since placing one puts it back into the list this walks.
            foreach (var unit in removedUnits.ToList())
            {
                if (unit == null || unit.CurrentState.Position != null)
                    continue;

                if (!zoneOfUnit.TryGetValue(unit, out var zone) || zone != index)
                    continue;

                if (PlaceUnit(unit, unit.CurrentState.Team, ZoneRules.Zones[index]))
                    RestoreUnit(unit);
            }
        }

        /// <summary>
        /// Puts one unit of a kind on the board. A kind is a <see cref="UnitBlueprint"/> and nothing
        /// else - the prefab is the one shared body every unit is drawn with, dressed by the
        /// blueprint, exactly as a lootbox is one prefab dressed by its type. There is no
        /// prefab-per-kind to keep in step with the blueprint any more.
        /// </summary>
        private Unit SpawnUnit(Team team, UnitBlueprint blueprint, int index, MapZone zone = null,
            bool hold = false)
        {
            if (blueprint == null)
            {
                Debug.LogError($"No unit blueprint set for team {team} in {nameof(UnitSpawnerSettings)}.", settings);
                return null;
            }

            if (settings.UnitPrefab == null)
            {
                Debug.LogError($"No shared unit prefab set in {nameof(UnitSpawnerSettings)}.", settings);
                return null;
            }

            var instance = Instantiate(settings.UnitPrefab, transform);

            var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sortingOrder = settings.OrderInLayer;
            spriteRenderer.sprite = blueprint.Sprite;

            // The body is shared, so the outline that was fitted to each old per-kind prefab has to
            // be refitted here instead - what can be clicked follows what is drawn.
            FitColliderToSprite(instance, blueprint.Sprite);

            var unit = instance.GetComponentInChildren<Unit>();
            unit.Init(tileSpawner, this, team, gameStateManager, fogOfWar, gameRules, animationSettings,
                blueprint, team == Team.Player ? playerVitals : null);

            // A held-back unit is made and dressed like any other and then simply waits: no tile,
            // out of play, and hidden by the same call a fallen unit is hidden with.
            if (hold)
                SetAside(instance);
            else
                PlaceUnit(unit, team, zone);

            if(team == Team.Opponent)
            {
                spriteRenderer.flipX = true;
                spriteRenderer.color = settings.OpponentColor;
                instance.name = $"Opponent {blueprint.name} {index}";
            }
            else
            {
                instance.name = $"Player {blueprint.name}";
            }

            selector.RegisterClickable(instance.GetComponentInChildren<Clickable>());

            if (!hold)
            {
                units.Add(instance);
                unitStateManager.Track(unit);
            }

            return instance;
        }

        /// <summary>
        /// Reshapes the unit's polygon collider to its sprite. Each kind used to carry its own
        /// prefab whose collider was fitted to its own art; with one shared body the shape has to
        /// come from the sprite, which is the blueprint's. Uses the sprite's authored physics shape
        /// and leaves the collider alone when the importer has none, so a sprite nobody has fitted
        /// yet keeps the shared body's outline rather than losing its collider.
        /// </summary>
        private static void FitColliderToSprite(Unit instance, Sprite sprite)
        {
            if (sprite == null)
                return;

            var collider = instance.GetComponentInChildren<PolygonCollider2D>();

            if (collider == null)
                return;

            var shapeCount = sprite.GetPhysicsShapeCount();

            if (shapeCount == 0)
                return;

            collider.pathCount = shapeCount;

            var points = new List<Vector2>();

            for (var i = 0; i < shapeCount; i++)
            {
                points.Clear();
                sprite.GetPhysicsShape(i, points);
                collider.SetPath(i, points);
            }
        }

        /// <summary>
        /// Puts a freshly spawned unit on the first tile that will take it, best candidates first: a
        /// unit belonging to a ring of the map takes that ring's ground, and one belonging to none
        /// takes its team's spawn zone. The list is ranked rather than rolled either way: a ring or
        /// a zone can be walled off by mountains or filled by the units placed before it, and
        /// re-rolling inside it would spin forever - the tail of the list spills over its border
        /// instead.
        /// </summary>
        private bool PlaceUnit(Unit unit, Team team, MapZone zone = null)
        {
            var candidates = zone != null
                ? tileSpawner.GetZonePositions(zone)
                : tileSpawner.GetSpawnZonePositions(team);

            foreach (var gridPosition in candidates)
            {
                var tile = tileSpawner.GetTileAtPosition(gridPosition);

                if (!unit.TryPlaceAtTile(tile))
                    continue;

                // Which ring it belongs to for the rest of the match. Read off the tile it actually
                // landed on rather than off the roster entry that asked for it: a ring walled off by
                // mountains or already full spills its units over its own border, and what confines
                // a unit has to be where it is standing. Only opponents are ever confined, so only
                // they are given one - the character is free to walk the whole map.
                unit.CurrentState.HomeZone = team == Team.Player
                    ? UnitState.NoZone
                    : ZoneRules.IndexAt(tile);

                // Placed, not moved: a fresh unit would otherwise be seen walking to its spawn tile
                // from wherever the prefab was instantiated.
                unit.SnapToCurrentTile();
                return true;
            }

            Debug.LogError($"No free tile left to spawn {unit.name} on.", settings);

            return false;
        }

        /// <summary>
        /// Sets a freshly made unit aside until its ring is reached: kept where removed units are
        /// kept, on no tile at all. Standing on none is what tells a unit that has not arrived from
        /// one that has fallen - both are hidden, and only the first was never anywhere.
        /// </summary>
        private void SetAside(Unit unit)
        {
            removedUnits.Add(unit);
            unit.SetInPlay(false);
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return tileSpawner.GridIndexToWorldPosition(gridPosition);
        }

        private void ClearUnits()
        {
            foreach (var unit in AllSpawnedUnits.ToList())
            {
                if (unit == null)
                    continue;

                unitStateManager.Untrack(unit);
                Destroy(unit.gameObject);
            }

            units.Clear();
            removedUnits.Clear();
            zoneOfUnit.Clear();
            playerUnit = null;
        }

        #region Setup

        public void Setup(GameStateManager gameStateManagerArg, Selector selectorArg, FogOfWar fogOfWarArg,
            GameRules gameRulesArg, AnimationSettings animationSettingsArg, UnitStateManager unitStateManagerArg,
            PlayerVitals playerVitalsArg)
        {
            gameStateManager = gameStateManagerArg;
            selector = selectorArg;
            fogOfWar = fogOfWarArg;
            gameRules = gameRulesArg;
            animationSettings = animationSettingsArg;
            unitStateManager = unitStateManagerArg;
            playerVitals = playerVitalsArg;
        }

        [ContextMenu("Spawn Units")]
        public void SpawnUnits()
        {
            tileSpawner.ResetOccupiedTiles();
            ClearUnits();

            // The player fields a single character, the opponent a roster - hence the two paths.
            playerUnit = SpawnUnit(Team.Player, settings.PlayerUnit, 0);
            SpawnOpponentUnits();
        }

        #endregion
    }
}