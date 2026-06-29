namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// A <see cref="Trait"/> attached to a terrain type. It affects whichever unit is currently occupying
    /// the tile (e.g. hills granting extra defence or range). Kept as its own type so terrain trait lists
    /// in the inspector only accept terrain traits.
    /// </summary>
    public abstract class TerrainTrait : Trait
    {
    }
}
