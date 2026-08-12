namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Something standing in front of the game — a card the player has to put away before play goes
    /// on. While one says it blocks, the <see cref="InputHandler"/> announces nothing, so the click
    /// or key that dismisses it does not also move a unit, end the turn or empty a slot.
    ///
    /// Asked rather than told: a view that is hidden, replaced or destroyed stops blocking by
    /// itself, and there is no flag left set behind it.
    /// </summary>
    public interface IInputBlocker
    {
        bool BlocksInput { get; }
    }
}
