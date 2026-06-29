using Runtime.Core.State;
using Runtime.Gameplay.Global;
using UnityEditor;
using UnityEngine;
using GameSelection = Runtime.Gameplay.Global.Selection;

namespace Editor
{
    /// <summary>
    /// While in Play mode, mirrors the unit selected in-game into the Editor selection
    /// (Hierarchy + Inspector), so picking a unit in the game also focuses its live
    /// GameObject for inspection. Lives under Editor/ so it is stripped from builds.
    /// </summary>
    [InitializeOnLoad]
    public static class InGameSelectionMirror
    {
        private static Selector subscribed;

        static InGameSelectionMirror()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                Subscribe();
            else if (change == PlayModeStateChange.ExitingPlayMode)
                Unsubscribe();
        }

        private static void Subscribe()
        {
            // The Selector is a scene object, so it already exists once we enter Play mode.
            subscribed = Object.FindFirstObjectByType<Selector>();
            if (subscribed != null)
                subscribed.OnSelectionChanged += HandleSelectionChanged;
        }

        private static void Unsubscribe()
        {
            if (subscribed != null)
                subscribed.OnSelectionChanged -= HandleSelectionChanged;
            subscribed = null;
        }

        private static void HandleSelectionChanged(ChangeEvent<GameSelection> changeEvent)
        {
            // Only follow actual unit selections; leave the Editor selection alone otherwise
            // (e.g. hovers, deselects) so it isn't constantly cleared while you play.
            var unit = changeEvent.NewValue.SelectedUnit;
            if (unit != null)
                UnityEditor.Selection.activeGameObject = unit.gameObject;
        }
    }
}
