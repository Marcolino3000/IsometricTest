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
            : this(attacker, defender, isRetaliation, attacker.CurrentState.Position)
        {
        }

        /// <summary>
        /// The same strike made from a tile the attacker is not standing on yet - what the AI asks
        /// while weighing an attack it would walk up to first, the way <c>RangeContext</c> carries a
        /// tile for the same reason.
        /// </summary>
        public CombatContext(Unit attacker, Unit defender, bool isRetaliation, Tile attackerTile)
        {
            Attacker = attacker;
            Defender = defender;
            AttackerTile = attackerTile;
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

    /// <summary>
    /// A unit looking out from a tile - what <see cref="Trait.ModifySightRange"/> is handed. The
    /// tile is passed rather than read off the unit for the same reason <see cref="RangeContext"/>
    /// carries one: the AI asks what it would see from a tile it is only considering stepping onto.
    /// </summary>
    public readonly struct SightContext
    {
        public readonly Unit Unit;
        public readonly Tile Tile;
        public readonly int BaseRange;

        public SightContext(Unit unit, Tile tile, int baseRange)
        {
            Unit = unit;
            Tile = tile;
            BaseRange = baseRange;
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

    /// <summary>
    /// A unit's turn beginning, on the tile it stands on - what <see cref="Trait.OnTurnBegan"/> is
    /// handed. The one context that carries no second party: nothing is being aimed at, the turn has
    /// simply come round to whoever carries the trait.
    /// </summary>
    public readonly struct TurnContext
    {
        public readonly Unit Unit;
        public readonly Tile Tile;

        public TurnContext(Unit unit, Tile tile)
        {
            Unit = unit;
            Tile = tile;
        }
    }
}
