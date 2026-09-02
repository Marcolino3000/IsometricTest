using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// How fast what happens on the board is drawn. Its own asset rather than a switch on
    /// <see cref="GameRules"/>, because it decides nothing about the match: a rule says what happens,
    /// this says only how long it takes to watch, and everything reading it is purely presentational.
    ///
    /// A <see cref="RuntimeSettings"/> like the rules, so an edit announces itself - though the one
    /// consumer so far reads the live asset per frame and needs no subscription.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Settings/Animation Settings")]
    public class AnimationSettings : RuntimeSettings
    {
        [Tooltip("How fast every unit animation is drawn - the walk between two tiles and the frames " +
                 "of a strike, a flinch or a fall alike, so a doubled speed halves the whole thing " +
                 "rather than only its frames. The board has already moved on by the time any of it " +
                 "is shown, so nothing about the match changes with it. Takes effect while playing.")]
        [Range(0.1f, 5f)] [SerializeField] private float speed = 1f;

        [Tooltip("How long a flinch is held back before it is drawn. A blow and the flinch it causes " +
                 "are said in the same frame, and the two units are drawn by animators of their own, " +
                 "so without a beat the flinch is over before the swing has begun. Counted at the " +
                 "speed above, so it keeps its place in the swing however fast both are drawn. " +
                 "Takes effect while playing.")]
        [Range(0f, 2f)] [SerializeField] private float hitDelay = 0.15f;

        /// <summary>
        /// The multiplier every animation is drawn at. Never zero: an animation at no speed would
        /// leave a unit walking forever and a one-shot would never run out, so the callback a fall
        /// depends on - hiding the unit that fell - would never come.
        /// </summary>
        public float Speed => Mathf.Max(speed, 0.01f);

        /// <summary>
        /// How long the flinch waits after it is said, in the seconds an animation is measured in
        /// rather than in real ones - <see cref="Speed"/> is folded in where it is spent, like every
        /// other duration here. Zero draws it in the frame the blow lands, as before.
        /// </summary>
        public float HitDelay => Mathf.Max(hitDelay, 0f);
    }
}
