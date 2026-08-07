using System;
using UnityEngine.InputSystem;

namespace Runtime.Debugger
{
    /// <summary>
    /// Modifier keys a <see cref="DebugHotkeyAttribute"/> can ask for. Left and right count as the
    /// same modifier. Cmd is the Apple key on macOS and the Windows key elsewhere - the Input System
    /// calls both "Meta".
    /// </summary>
    [Flags]
    public enum HotkeyMods
    {
        None = 0,
        Ctrl = 1,
        Alt = 2,
        Shift = 4,
        Cmd = 8
    }

    /// <summary>
    /// Binds a parameterless method to a key while the game runs, the way <see cref="UnityEngine.ContextMenu"/>
    /// binds one to the inspector's context menu. <see cref="DebugHotkeys"/> collects every one of
    /// them at startup, so a new hotkey is this attribute and nothing else - no wiring in the
    /// Initiator, no entry in a list somewhere.
    ///
    /// Instance methods are invoked on the first object of the declaring type found in the scene,
    /// looked up at press time so a respawn cannot leave a stale target behind. Static methods need
    /// no target. Only components and statics can be reached; anything else is reported and skipped.
    ///
    /// Play mode only, and only while the Game view has focus: the dispatcher rides the player loop
    /// and <see cref="Keyboard.current"/>, neither of which runs when the game does not.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DebugHotkeyAttribute : Attribute
    {
        public Key Key { get; }

        public HotkeyMods Mods { get; }

        /// <summary>Names the binding in log messages. Falls back to the method it sits on.</summary>
        public string Label { get; }

        public DebugHotkeyAttribute(Key key, HotkeyMods mods = HotkeyMods.None, string label = null)
        {
            Key = key;
            Mods = mods;
            Label = label;
        }
    }
}
