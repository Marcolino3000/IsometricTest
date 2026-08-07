#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Debugger
{
    /// <summary>
    /// Collects every <see cref="DebugHotkeyAttribute"/> in the game assembly and invokes what it
    /// finds when the key comes down. Deliberately not a system the Initiator injects: it spawns
    /// itself and resolves its own targets, because the whole point is that a hotkey costs one
    /// attribute and no wiring.
    ///
    /// Compiled out of release builds; the attribute itself always compiles, so marked methods
    /// stay valid whether or not anything is listening.
    /// </summary>
    public class DebugHotkeys : MonoBehaviour
    {
        private sealed class Binding
        {
            public Key Key;
            public HotkeyMods Mods;
            public MethodInfo Method;

            /// <summary>The component to invoke on, or null for a static method.</summary>
            public Type TargetType;

            public string Label;
        }

        private readonly List<Binding> bindings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            GameObject host = new GameObject(nameof(DebugHotkeys)) { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<DebugHotkeys>();
        }

        private void Awake()
        {
            Scan();
        }

        /// <summary>
        /// Walks the assembly the attribute itself lives in - everything under Assets/ compiles into
        /// that one, and sweeping the rest of the AppDomain would cost far more than it could find.
        /// Declared methods only, or a base class's hotkey would be collected once per subclass.
        /// </summary>
        private void Scan()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.Instance | BindingFlags.Static
                                       | BindingFlags.DeclaredOnly;

            foreach (Type type in typeof(DebugHotkeyAttribute).Assembly.GetTypes())
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                DebugHotkeyAttribute attribute = method.GetCustomAttribute<DebugHotkeyAttribute>();

                if (attribute == null)
                    continue;

                if (TryCreateBinding(method, attribute, out Binding binding))
                    bindings.Add(binding);
            }

            if (bindings.Count > 0)
                Debug.Log($"Debug hotkeys: {DescribeAll()}");
        }

        /// <summary>
        /// Turns one marked method into a binding, or explains in the console why it cannot be one.
        /// Everything that could go wrong is caught here so <see cref="Update"/> never has to check.
        /// </summary>
        private bool TryCreateBinding(MethodInfo method, DebugHotkeyAttribute attribute, out Binding binding)
        {
            binding = null;
            string name = $"{method.DeclaringType?.Name}.{method.Name}";

            if (attribute.Key == Key.None)
            {
                Debug.LogWarning($"[DebugHotkey] {name} names no key.");
                return false;
            }

            if (method.GetParameters().Length > 0 || method.IsGenericMethodDefinition)
            {
                Debug.LogWarning($"[DebugHotkey] {name} must take no parameters and no type arguments.");
                return false;
            }

            Type targetType = method.IsStatic ? null : method.DeclaringType;

            if (targetType != null && !typeof(Component).IsAssignableFrom(targetType))
            {
                Debug.LogWarning($"[DebugHotkey] {name} is neither static nor on a component, so it cannot be found in the scene.");
                return false;
            }

            binding = new Binding
            {
                Key = attribute.Key,
                Mods = attribute.Mods,
                Method = method,
                TargetType = targetType,
                Label = attribute.Label ?? name
            };

            Binding clash = FindBinding(binding.Key, binding.Mods);

            if (clash != null)
            {
                Debug.LogWarning($"[DebugHotkey] {Describe(binding)} is taken by {clash.Label}, so {binding.Label} stays unbound.");
                binding = null;
                return false;
            }

            return true;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || bindings.Count == 0)
                return;

            HotkeyMods held = HeldMods(keyboard);

            foreach (Binding binding in bindings)
            {
                // Exact match rather than a subset: Alt+R has to stay quiet while Alt+Shift+R is
                // pressed, or binding both would fire both. Only the key itself is an edge; the
                // modifiers are held down across it.
                if (binding.Mods != held)
                    continue;

                if (keyboard[binding.Key].wasPressedThisFrame)
                    Invoke(binding);
            }
        }

        /// <summary>
        /// ctrlKey, altKey and shiftKey already fold the left and right key together. Cmd has no
        /// such shorthand and is called Meta on every platform.
        /// </summary>
        private static HotkeyMods HeldMods(Keyboard keyboard)
        {
            HotkeyMods mods = HotkeyMods.None;

            if (keyboard.ctrlKey.isPressed)
                mods |= HotkeyMods.Ctrl;

            if (keyboard.altKey.isPressed)
                mods |= HotkeyMods.Alt;

            if (keyboard.shiftKey.isPressed)
                mods |= HotkeyMods.Shift;

            if (keyboard[Key.LeftMeta].isPressed || keyboard[Key.RightMeta].isPressed)
                mods |= HotkeyMods.Cmd;

            return mods;
        }

        /// <summary>
        /// The target is resolved per press instead of cached: a restart replaces every unit, so a
        /// reference kept from startup would point at something no longer in the scene.
        /// </summary>
        private static void Invoke(Binding binding)
        {
            object target = null;

            if (binding.TargetType != null)
            {
                target = FindFirstObjectByType(binding.TargetType, FindObjectsInactive.Include);

                if (target == null)
                {
                    Debug.LogWarning($"[DebugHotkey] {Describe(binding)}: no {binding.TargetType.Name} in the scene.");
                    return;
                }
            }

            try
            {
                binding.Method.Invoke(target, null);
            }
            catch (TargetInvocationException e)
            {
                // Unwrapped, or the console shows the reflection call instead of what actually threw.
                Debug.LogException(e.InnerException ?? e, target as UnityEngine.Object);
            }
        }

        private Binding FindBinding(Key key, HotkeyMods mods)
        {
            foreach (Binding binding in bindings)
            {
                if (binding.Key == key && binding.Mods == mods)
                    return binding;
            }

            return null;
        }

        private string DescribeAll()
        {
            StringBuilder text = new StringBuilder();

            foreach (Binding binding in bindings)
            {
                if (text.Length > 0)
                    text.Append(", ");

                text.Append($"{Describe(binding)} {binding.Label}");
            }

            return text.ToString();
        }

        private static string Describe(Binding binding)
        {
            StringBuilder text = new StringBuilder();

            if (binding.Mods.HasFlag(HotkeyMods.Ctrl))
                text.Append("Ctrl+");

            if (binding.Mods.HasFlag(HotkeyMods.Alt))
                text.Append("Alt+");

            if (binding.Mods.HasFlag(HotkeyMods.Shift))
                text.Append("Shift+");

            if (binding.Mods.HasFlag(HotkeyMods.Cmd))
                text.Append("Cmd+");

            return text.Append(binding.Key).ToString();
        }
    }
}
#endif
