using System;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// How a line of the wave moves: how far a letter rides, how fast the wave runs through the
    /// line, and how far one letter lags behind the one before it. A class of its own because there
    /// are two of them on every announcement - the sentence, and the word it ends on, which moves
    /// faster so the news reads as the word rather than as the sentence.
    ///
    /// The height is a share of the letter's own size rather than a number of pixels, so a wave
    /// keeps its proportions whatever the headline is set in.
    /// </summary>
    [Serializable]
    public class WaveSettings
    {
        [Tooltip("How far a letter rides, as a share of its own size.")]
        [Min(0f)] public float Height = 0.14f;

        [Tooltip("How fast the wave runs, in radians a second.")]
        [Min(0f)] public float Speed = 4.5f;

        [Tooltip("How far one letter lags behind the one before it, in radians. Near zero the whole " +
                 "line bobs as one block instead of a wave travelling through it.")]
        public float Lag = 0.45f;
    }

    /// <summary>
    /// What the announcement over the board looks like and how long it stays: the two type sizes,
    /// how far down the screen it hangs, the three parts of its life, and the two waves running
    /// through its headline.
    ///
    /// Its own asset rather than switches on <see cref="GameRules"/>, for the same reason
    /// <see cref="TooltipSettings"/> is: a rule decides what happens on the board, this decides only
    /// how something is said. Loaded from Resources rather than injected, like the tooltip and
    /// effect-animation settings - the screen is built from code, so nothing has to be wired, and a
    /// project with no asset falls back to these defaults instead of losing its announcements.
    ///
    /// The two type sizes, the face and the inset are read when the screen is built; everything
    /// else is read as it is used, so timings and both waves can be tuned while the game runs.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Settings/Announcement Settings")]
    public class AnnouncementSettings : RuntimeSettings
    {
        public const string ResourcePath = "Settings/Default AnnouncementSettings";

        [Header("Layout")]
        [Tooltip("Size of the big line, in panel pixels.")]
        [Min(1f)] public float HeadlineSize = 64f;

        [Tooltip("Size of the smaller line under it.")]
        [Min(1f)] public float DetailSize = 22f;

        [Tooltip("How far down the screen the lines hang, as a percentage of its height. Clear of " +
                 "the top of the HUD and well above the character, so an announcement never covers " +
                 "what it is being said about.")]
        [Range(0f, 100f)] public float TopInsetPercent = 14f;

        [Tooltip("The face both lines are set in. Unset leaves them in the interface's own font, " +
                 "so a project with no display font loses nothing but the look.")]
        public Font Font;

        [Header("How Long It Stays")]
        [Min(0f)] public float FadeInDuration = 0.35f;
        [Min(0f)] public float HoldDuration = 2.2f;
        [Min(0.01f)] public float FadeOutDuration = 0.9f;

        [Header("Wave")]
        [Tooltip("The wave running through the sentence.")]
        public WaveSettings Text = new();

        [Tooltip("The wave running through its last word - the one the line is about, so it should " +
                 "move markedly faster than the rest. With nothing to tell them apart the headline " +
                 "reads as one line rather than as a statement ending in a word.")]
        public WaveSettings LastWord = new() { Height = 0.2f, Speed = 13f, Lag = 0.7f };

        /// <summary>
        /// The authored asset, or a default instance where none is authored yet. Never null, so the
        /// screen needs no branch for a project that has not made one.
        /// </summary>
        public static AnnouncementSettings Load()
        {
            var settings = Resources.Load<AnnouncementSettings>(ResourcePath);

            return settings != null ? settings : CreateInstance<AnnouncementSettings>();
        }
    }
}
