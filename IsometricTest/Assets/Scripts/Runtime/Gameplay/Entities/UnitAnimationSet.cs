using System;
using System.Collections.Generic;
using Actions;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// What a unit is doing, in the vocabulary its frames are authored in. A further one is a further
    /// entry here and a further row in the set - never a branch wherever an animation is chosen.
    /// </summary>
    public enum UnitAnimation
    {
        Idle,
        Move,
        MeleeAttack,
        RangedAttack,
        Hit,
        Death
    }

    /// <summary>
    /// Which frames a unit is drawn with while it stands, walks and strikes. One asset per sprite
    /// sheet, named by the blueprint that spawns from it, so a unit type is authored in the two
    /// places it already is rather than a third.
    ///
    /// A table rather than one field per animation, the way <see cref="UI.ActionIconSet"/> is: the
    /// three sheets in play slice their rows to different indices, so what an animation is made of
    /// belongs beside its name. An animation with no row here is simply not played, which is what
    /// leaves a unit with the still sprite its blueprint gives it.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Data/Unit Animation Set")]
    public class UnitAnimationSet : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            [Tooltip("What these frames show. An animation listed twice takes its first entry.")]
            public UnitAnimation Animation;

            [Tooltip("The frames, in the order they are drawn. Sliced by the importer rather than cut " +
                     "here, so the pivots the sprite editor authored are the ones used.")]
            public List<Sprite> Frames = new();

            [Tooltip("How fast the frames are drawn. The whole animation therefore lasts frames over " +
                     "this, which is what a one-shot such as a strike is timed by.")]
            public float FramesPerSecond = 10f;

            [Tooltip("Whether the animation starts over when it runs out. A state the unit is in " +
                     "loops; something that happened to it plays once. Ignored for a strike, which " +
                     "is always played once.")]
            public bool Loop = true;

            public bool IsEmpty => Frames == null || Frames.Count == 0;
        }

        [Tooltip("How long one step onto the next tile takes to walk. The unit's tile is claimed at " +
                 "once either way - this is only how long its sprite takes to catch up, and how long " +
                 "the walk cycle therefore runs for.")]
        [SerializeField] private float secondsPerStep = 0.14f;

        [SerializeField] private List<Clip> clips = new();

        /// <summary>How long a single step is drawn for. Never zero, so a step cannot be free.</summary>
        public float SecondsPerStep => Mathf.Max(secondsPerStep, 0.01f);

        /// <summary>
        /// The frames for <paramref name="animation"/>, or null when none are authored - which is
        /// what leaves whoever asked with the sprite that was already showing.
        /// </summary>
        public Clip For(UnitAnimation animation)
        {
            foreach (var clip in clips)
                if (clip != null && clip.Animation == animation && !clip.IsEmpty)
                    return clip;

            return null;
        }

        /// <summary>
        /// The strike for the weapon in hand. Asked of <see cref="AttackActionData.Kind"/> rather
        /// than of the unit, so a swapped weapon changes how the swing is drawn for free, the way it
        /// changes what the swing is worth - and a sheet with only one of the two rows falls back to
        /// the row it has rather than to no animation at all.
        /// </summary>
        public Clip AttackFor(WeaponKind kind)
        {
            var wanted = kind == WeaponKind.Ranged ? UnitAnimation.RangedAttack : UnitAnimation.MeleeAttack;
            var other = kind == WeaponKind.Ranged ? UnitAnimation.MeleeAttack : UnitAnimation.RangedAttack;

            return For(wanted) ?? For(other);
        }
    }
}
