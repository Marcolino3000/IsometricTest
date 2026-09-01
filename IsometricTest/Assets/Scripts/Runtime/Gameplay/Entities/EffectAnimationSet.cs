using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// Something that happened to a unit and is worth drawing over it, in the vocabulary its frames
    /// are authored in. Named by the event rather than by the art, the way <see cref="UnitAnimation"/>
    /// is: what a use of an item looks like is the asset's business, not the caller's. A further one
    /// is a further entry here and a further row in the set - never a branch where an effect is
    /// chosen.
    /// </summary>
    public enum EffectAnimation
    {
        /// <summary>An active item used up - see <c>ActionExecutor.ExecuteItemAction</c>.</summary>
        ItemUsed
    }

    /// <summary>
    /// Which frames are drawn over a unit for each of those. One asset for the whole game rather than
    /// one per unit, which is what separates it from <see cref="UnitAnimationSet"/>: an effect is
    /// drawn on top of whoever it happened to, so it is the same frames whichever unit that is, and
    /// there is no blueprint to hang it on.
    ///
    /// Loaded from Resources by <see cref="EffectAnimator"/> rather than injected, like
    /// <see cref="UI.FloatingText"/>'s settings are and for the same reason: an effect is spawned
    /// from code at the moment something happens, so there is no scene reference to reach it through.
    ///
    /// A row with no frames is simply not played, which is what leaves an event silent until somebody
    /// authors it.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Data/Effect Animation Set")]
    public class EffectAnimationSet : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            [Tooltip("What these frames show. An effect listed twice takes its first entry.")]
            public EffectAnimation Animation;

            [Tooltip("The frames, in the order they are drawn. Played once and then gone - an effect " +
                     "is something that happened, never a state a unit is in, so there is no loop " +
                     "switch here the way there is on a unit's clips.")]
            public List<Sprite> Frames = new();

            [Tooltip("How fast the frames are drawn. The effect therefore lasts frames over this, " +
                     "scaled by the global animation speed like every other animation.")]
            public float FramesPerSecond = 24f;

            [Tooltip("Size against the unit's own sprite, which the effect is drawn as a child of - " +
                     "1 draws it at the same pixel density the character is drawn at.")]
            public float Scale = 1f;

            [Tooltip("Where it sits over the unit, in the unit sprite's own space. Zero is centred " +
                     "on the character.")]
            public Vector3 Offset = Vector3.zero;

            [Tooltip("How far in front of the unit's sprite it is drawn. 1 puts it just over the " +
                     "character; 0 would let the sort order decide, which changes with the camera.")]
            public int SortingOffset = 1;

            public bool IsEmpty => Frames == null || Frames.Count == 0;
        }

        [SerializeField] private List<Clip> clips = new();

        /// <summary>
        /// The frames for <paramref name="animation"/>, or null when none are authored - which is
        /// what leaves an event with nothing drawn for it rather than with an empty object over the
        /// unit.
        /// </summary>
        public Clip For(EffectAnimation animation)
        {
            foreach (var clip in clips)
                if (clip != null && clip.Animation == animation && !clip.IsEmpty)
                    return clip;

            return null;
        }
    }
}
