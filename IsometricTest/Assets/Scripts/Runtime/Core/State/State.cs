using System;
using Runtime.Gameplay.Entities;

namespace Runtime.Core.State
{
    /// <summary>
    /// Why a value moved. Carried by every <see cref="ChangeEvent{T}"/> so a subscriber can tell an
    /// action apart from a board being put back, without keeping a flag of its own: a damage popup,
    /// a sound or a hit flash answers <see cref="Gameplay"/> only, while a bar redraws on all three.
    /// </summary>
    public enum ChangeReason
    {
        /// <summary>The match being played - the only reason worth reacting to presentationally.</summary>
        Gameplay,

        /// <summary>Undo/redo putting a recorded value back. Nothing happened; a value merely is what it was.</summary>
        Restore,

        /// <summary>Spawning and wiring, before the match is under way.</summary>
        Setup
    }

    [Serializable]
    public class State
    {
        public Team Team;

        public State Clone()
        {
            return new State
            {
                Team = Team
            };
        }
    }

    /// <summary>
    /// The one payload shape for "a value changed": what it was, what it is, and why it moved.
    /// Observers get values rather than live references - an entity is the documented exception,
    /// since it cannot be usefully cloned.
    /// </summary>
    public class ChangeEvent<T>
    {
        public ChangeEvent(T previousValue, T newValue, ChangeReason reason = ChangeReason.Gameplay)
        {
            PreviousValue = previousValue;
            NewValue = newValue;
            Reason = reason;
        }

        public readonly T PreviousValue;
        public readonly T NewValue;
        public readonly ChangeReason Reason;
    }
}
