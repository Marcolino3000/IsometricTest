using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// Draws a one-shot effect over a unit - the sparkle a drunk potion leaves - and then takes
    /// itself away. Purely presentational like <see cref="UnitAnimator"/>, and rather more so:
    /// nothing waits for it, nothing asks whether it is running, and the board has already moved on
    /// by the time the first frame is shown.
    ///
    /// Spawned from code at the moment something happens, the way <see cref="UI.FloatingText"/> is,
    /// so its frames and its settings are loaded from Resources rather than injected: there is no
    /// scene reference from a rule to the thing that draws it.
    ///
    /// <b>A child of the unit's own sprite rather than a free-standing object</b>, which is what
    /// gives it the isometric rotation and scale the character is drawn with for nothing, carries it
    /// along a walk still catching up with its tile, and hides it with the unit when the fog
    /// deactivates that sprite. It therefore also goes when the unit does - which is right for an
    /// effect that only means anything over somebody.
    /// </summary>
    public class EffectAnimator : MonoBehaviour
    {
        private const string SetResourcePath = "Settings/Animation/Default EffectAnimations";
        private const string SpeedResourcePath = "Settings/Default AnimationSettings";

        private static EffectAnimationSet set;
        private static AnimationSettings animationSettings;

        private EffectAnimationSet.Clip clip;
        private SpriteRenderer spriteRenderer;
        private int frame;
        private float frameTimer;

        /// <summary>
        /// The authored frames. Loaded on first use and kept, the way <see cref="UI.FloatingText"/>
        /// keeps its settings - and falling back to an empty set rather than to null for the same
        /// reason: every row of it is then simply unauthored, so a missing asset costs the effects
        /// and nothing else, and says so once instead of on every use.
        /// </summary>
        private static EffectAnimationSet Set
        {
            get
            {
                if (set == null)
                {
                    set = Resources.Load<EffectAnimationSet>(SetResourcePath);

                    if (set == null)
                    {
                        Debug.LogError($"No EffectAnimationSet asset at Resources/{SetResourcePath}, " +
                                       "nothing is drawn over a unit.");
                        set = ScriptableObject.CreateInstance<EffectAnimationSet>();
                    }
                }

                return set;
            }
        }

        /// <summary>
        /// The global animation speed, so an effect takes as long to watch as everything else does
        /// and follows a speed changed mid-play. Full speed with no asset, since nothing about the
        /// match depends on it - unlike a unit's animation, where a speed of zero would strand the
        /// callback a fall depends on.
        /// </summary>
        private static float Speed
        {
            get
            {
                if (animationSettings == null)
                    animationSettings = Resources.Load<AnimationSettings>(SpeedResourcePath);

                return animationSettings != null ? animationSettings.Speed : 1f;
            }
        }

        /// <summary>
        /// Draws <paramref name="animation"/> over <paramref name="unit"/>, or nothing at all when
        /// no frames are authored for it. Says whether anything was played, for a caller that has
        /// something else to do when nothing was - no such caller yet, and it costs a bool.
        /// </summary>
        public static bool Play(EffectAnimation animation, Unit unit)
        {
            if (unit == null)
                return false;

            var clip = Set.For(animation);

            if (clip == null)
                return false;

            // The unit's own sprite, found the way UnitAnimator.Create finds it. Hung under it
            // rather than under the unit, so the rotation and scale the character is drawn with are
            // inherited rather than copied.
            var unitSprite = unit.GetComponentInChildren<SpriteRenderer>(true);

            if (unitSprite == null)
                return false;

            var effect = new GameObject($"Effect {animation}");
            effect.transform.SetParent(unitSprite.transform, worldPositionStays: false);
            effect.transform.localPosition = clip.Offset;
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one * clip.Scale;

            var renderer = effect.AddComponent<SpriteRenderer>();
            // Sorted against the sprite it is drawn over rather than given a layer of its own: the
            // unit is what it belongs to, so it has to move with it whatever else is on the board.
            renderer.sortingLayerID = unitSprite.sortingLayerID;
            renderer.sortingOrder = unitSprite.sortingOrder + clip.SortingOffset;

            var animator = effect.AddComponent<EffectAnimator>();
            animator.clip = clip;
            animator.spriteRenderer = renderer;
            animator.Draw();

            return true;
        }

        private void Update()
        {
            frameTimer += Time.deltaTime * Speed;

            var secondsPerFrame = 1f / Mathf.Max(clip.FramesPerSecond, 0.01f);

            while (frameTimer >= secondsPerFrame)
            {
                frameTimer -= secondsPerFrame;
                frame++;

                // Run out. An effect is something that happened rather than a state, so it has
                // nothing to rest on and nothing to loop back to: it goes.
                if (frame >= clip.Frames.Count)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            Draw();
        }

        private void Draw()
        {
            var sprite = clip.Frames[Mathf.Clamp(frame, 0, clip.Frames.Count - 1)];

            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
