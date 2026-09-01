using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// How tooltips are drawn. Its own asset rather than a switch on <see cref="GameRules"/> for the
    /// same reason <see cref="AnimationSettings"/> is: a rule decides what happens on the board, this
    /// decides only how a thing is labelled. Whether a unit is labelled at all stays
    /// <see cref="GameRules.ShowUnitCard"/>, since that switch was already authored.
    ///
    /// Loaded from Resources rather than injected, like the effect animations: the view that reads it
    /// is built from code, so nothing has to be wired, and a missing asset falls back to these
    /// defaults instead of leaving the game without tooltips.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Settings/Tooltip Settings")]
    public class TooltipSettings : RuntimeSettings
    {
        public const string ResourcePath = "Settings/Default TooltipSettings";

        [Tooltip("Seconds the cursor has to rest on a UI element before its tooltip opens. Longer " +
                 "than the one below: the bar is crossed on the way to somewhere else far more often " +
                 "than a unit is.")]
        [Min(0f)] [SerializeField] private float uiDelay = 0.5f;

        [Tooltip("Seconds the cursor has to rest on a unit or a tile before its card opens. Short " +
                 "rather than none, so dragging the cursor over the board does not flash a card per " +
                 "tile.")]
        [Min(0f)] [SerializeField] private float worldDelay = 0.25f;

        [Tooltip("Distance between a tooltip and the thing it describes, in panel pixels.")]
        [Min(0f)] [SerializeField] private float gap = 10f;

        [Tooltip("How wide a tooltip may get before its text wraps, in panel pixels.")]
        [Min(80f)] [SerializeField] private float maxWidth = 340f;

        [Tooltip("Seconds between two readings of what is being described. The numbers are read off " +
                 "the live board - a unit losing health under the cursor updates its card - and this " +
                 "is how often that is asked.")]
        [Min(0.02f)] [SerializeField] private float refreshInterval = 0.2f;

        public float UiDelay => uiDelay;
        public float WorldDelay => worldDelay;
        public float Gap => gap;
        public float MaxWidth => maxWidth;
        public float RefreshInterval => refreshInterval;

        /// <summary>
        /// The authored asset, or a default instance where none is authored yet. Never null, so the
        /// view needs no branch for a project that has not made one.
        /// </summary>
        public static TooltipSettings Load()
        {
            var settings = Resources.Load<TooltipSettings>(ResourcePath);

            return settings != null ? settings : CreateInstance<TooltipSettings>();
        }
    }
}
