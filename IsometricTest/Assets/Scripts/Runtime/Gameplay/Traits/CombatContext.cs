using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// Immutable snapshot of a single strike (one unit hitting another). A retaliation is resolved as its own strike with attacker
    /// and defender swapped and <see cref="IsRetaliation"/> set, so traits can treat counter-attacks
    /// differently if they want to.
    /// </summary>
    public readonly struct CombatContext
    {
        public readonly Unit Attacker;
        public readonly Unit Defender;
        public readonly Tile AttackerTile;
        public readonly Tile DefenderTile;
        public readonly bool IsRetaliation;

        public CombatContext(Unit attacker, Unit defender, bool isRetaliation)
        {
            Attacker = attacker;
            Defender = defender;
            AttackerTile = attacker.CurrentState.Position;
            DefenderTile = defender.CurrentState.Position;
            IsRetaliation = isRetaliation;
        }
    }
    
    /// <summary>
    /// One step of movement: the unit about to enter <see cref="Tile"/>. Carries the state rather
    /// than the <see cref="Unit"/> because the tile highlighter, which asks what is reachable, only
    /// ever holds the state - and the traits and the move action are both on it anyway.
    /// </summary>
    public readonly struct MoveContext
    {
        public readonly UnitState Mover;
        public readonly Tile Tile;

        public MoveContext(UnitState mover, Tile tile)
        {
            Mover = mover;
            Tile = tile;
        }
    }

    public readonly struct RangeContext
    {
        public readonly Unit Unit;
        public readonly Tile Tile;
        public readonly int BaseRange;
        public readonly bool IsRanged;

        public RangeContext(Unit unit, Tile tile, int baseRange)
        {
            Unit = unit;
            Tile = tile;
            BaseRange = baseRange;
            IsRanged = baseRange > 1;
        }
    }
}
