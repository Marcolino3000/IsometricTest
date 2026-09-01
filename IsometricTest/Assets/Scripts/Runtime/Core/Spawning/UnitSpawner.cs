using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Core.State;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
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

        private void SpawnOpponentUnits()
        {
            foreach (var unitAmount in settings.OpponentUnits)
            {
                for (int i = 0; i < unitAmount.Amount; i++)
                {
                    SpawnUnit(Team.Opponent, unitAmount.Blueprint, i);
                }
            }
        }

        /// <summary>
        /// Puts one unit of a kind on the board. A kind is a <see cref="UnitBlueprint"/> and nothing
        /// else - the prefab is the one shared body every unit is drawn with, dressed by the
        /// blueprint, exactly as a lootbox is one prefab dressed by its type. There is no
        /// prefab-per-kind to keep in step with the blueprint any more.
        /// </summary>
        private Unit SpawnUnit(Team team, UnitBlueprint blueprint, int index)
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
                blueprint);

            PlaceUnit(unit, team);

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

            units.Add(instance);

            selector.RegisterClickable(instance.GetComponentInChildren<Clickable>());

            unitStateManager.Track(unit);

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
        /// Puts a freshly spawned unit on the first tile of its spawn zone that will take it. The zone is
        /// ranked rather than rolled: it can be walled off by mountains or filled by the units placed before
        /// it, and re-rolling inside it would spin forever - the tail of the list spills over its border
        /// instead.
        /// </summary>
        private void PlaceUnit(Unit unit, Team team)
        {
            foreach (var gridPosition in tileSpawner.GetSpawnZonePositions(team))
            {
                var tile = tileSpawner.GetTileAtPosition(gridPosition);

                if (!unit.TryPlaceAtTile(tile))
                    continue;

                // Placed, not moved: a fresh unit would otherwise be seen walking to its spawn tile
                // from wherever the prefab was instantiated.
                unit.SnapToCurrentTile();
                return;
            }

            Debug.LogError($"No free tile left to spawn {unit.name} on.", settings);
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
            playerUnit = null;
        }

        #region Setup

        public void Setup(GameStateManager gameStateManagerArg, Selector selectorArg, FogOfWar fogOfWarArg,
            GameRules gameRulesArg, AnimationSettings animationSettingsArg, UnitStateManager unitStateManagerArg)
        {
            gameStateManager = gameStateManagerArg;
            selector = selectorArg;
            fogOfWar = fogOfWarArg;
            gameRules = gameRulesArg;
            animationSettings = animationSettingsArg;
            unitStateManager = unitStateManagerArg;
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