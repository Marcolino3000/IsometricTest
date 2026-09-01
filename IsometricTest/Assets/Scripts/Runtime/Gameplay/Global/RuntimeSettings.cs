using System;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// A settings asset that says when it has been edited. Settings SOs are held as live references
    /// so a switch applies mid-play, but a ScriptableObject changed in the inspector announces
    /// nothing - which is why every consumer used to either poll for drift in <c>Update</c> or miss
    /// the change entirely.
    ///
    /// <see cref="Changed"/> is raised by the inspector's own <c>OnValidate</c> and by
    /// <see cref="NotifyChanged"/> for anything that writes a field from code. Subscribers must
    /// unsubscribe: the asset outlives every scene object that listens to it, so a missed
    /// unsubscribe keeps a destroyed object alive and is called on a dead reference.
    /// </summary>
    public abstract class RuntimeSettings : ScriptableObject
    {
        /// <summary>Raised after any field on this asset has been edited.</summary>
        public event Action Changed;

        /// <summary>
        /// Says the asset has been written to from code. The inspector needs no help - it calls
        /// <see cref="OnValidate"/> itself - but a switch flipped by a debug hotkey or a test does.
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        protected virtual void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
