namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// A <see cref="Trait"/> attached to an individual unit (via its <see cref="Entities.UnitState"/>).
    /// It travels with the unit regardless of where it stands (e.g. a chance to crit, or a bonus for
    /// attacking from certain ground). Kept as its own type so unit trait lists in the inspector only
    /// accept unit traits.
    /// </summary>
    public abstract class UnitTrait : Trait
    {
    }
}
