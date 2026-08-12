using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// How far a unit sees, and what stands in the way - the sight counterpart to
    /// <see cref="CombatRules"/> and <see cref="MovementRules"/>. Everything that draws or reasons
    /// about visibility asks here, so the fog, the AI's exploration and the unit card can never
    /// disagree about what a hill is worth.
    ///
    /// Two questions, deliberately apart: how far the eye reaches, which is a number every
    /// <see cref="Trait"/> may move, and whether the ground lets it through, which is a property of
    /// the tiles between and of nobody's traits - a unit does not carry the hill it is hiding behind.
    /// </summary>
    public static class SightRules
    {
        private static TileSpawner tiles;

        /// <summary>
        /// Injected by the Initiator, next to <see cref="CombatRules.Setup"/>. A line has to be walked
        /// tile by tile, and the tiles are the spawner's - this is the one rule query that cannot
        /// answer from its arguments alone.
        /// </summary>
        public static void Setup(TileSpawner tileSpawner)
        {
            tiles = tileSpawner;

            if (tiles == null)
                Debug.LogError("SightRules got no TileSpawner - nothing will block sight or fire.");
        }

        /// <summary>
        /// How far <paramref name="unit"/> sees from where it stands: its base sight and permanent
        /// bonuses (<see cref="Unit.SightRange"/>) with every trait's say folded in.
        /// </summary>
        public static int GetSightRange(Unit unit)
        {
            return GetSightRange(unit, unit != null ? unit.CurrentState.Position : null);
        }

        /// <summary>
        /// The same as if it were standing on <paramref name="fromTile"/> - what the AI weighs a step
        /// by, since a hill is only worth walking onto for the ground it would uncover from up there.
        /// </summary>
        public static int GetSightRange(Unit unit, Tile fromTile)
        {
            if (unit == null)
                return 0;

            var baseRange = unit.SightRange;

            if (fromTile == null)
                return baseRange;

            var context = new SightContext(unit, fromTile, baseRange);
            var range = baseRange;

            foreach (var trait in CombatRules.TraitsAffecting(unit.CurrentState, fromTile))
                range = trait.ModifySightRange(range, context);

            return Mathf.Max(0, range);
        }

        /// <summary>
        /// Whether <paramref name="blocker"/> hides what lies behind it from somebody standing on
        /// <paramref name="viewer"/>: only ground standing higher than the viewer's own does, so a
        /// unit on a hill looks over the hills and is stopped only by the mountains.
        ///
        /// The blocking tile itself is still seen - a hill is in the way, not invisible - which is
        /// why the caller never asks this about the tile it is testing for.
        /// </summary>
        public static bool BlocksSight(Tile blocker, Tile viewer)
        {
            return blocker != null && viewer != null && blocker.Elevation > viewer.Elevation;
        }

        /// <summary>
        /// Whether nothing standing between the two tiles is higher than <paramref name="from"/> is -
        /// the one line the eye and the arrow both have to travel, which is why an archer cannot
        /// shoot through the mountain it cannot see through either.
        ///
        /// The line is sampled once per step along its longer axis, so neither end is ever tested: a
        /// hill is seen and shot at from below, only what stands behind it is not. Adjacent tiles have
        /// nothing in between, which is what leaves melee untouched by this without a case for it.
        /// </summary>
        public static bool HasClearLine(Tile from, Tile target)
        {
            if (from == null || target == null || tiles == null)
                return true;

            var start = from.Position;
            var end = target.Position;

            var steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));

            for (var i = 1; i < steps; i++)
            {
                var t = (float)i / steps;

                var between = tiles.GetTileAtPosition(new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t))));

                if (BlocksSight(between, from))
                    return false;
            }

            return true;
        }
    }
}
