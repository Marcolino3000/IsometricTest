using System;
using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// Draws a unit: the walk between two tiles, which frames are showing while it walks, stands or
    /// strikes, and which way it is turned while doing so. Purely presentational - the board has
    /// already moved on by the time any of this is shown, so nothing here is ever asked a question
    /// about the game.
    ///
    /// It owns the facing for the same reason it owns the walk: turning has to be ordered against
    /// both queues rather than done when a thing is said. A unit that walks into range says its
    /// swing before it sets off, so a turn taken then would turn it out of the walk - the step is
    /// turned into as it begins, and the strike as it begins.
    ///
    /// It owns the walk as well as the frames on purpose. A move is instant to the rules
    /// (<see cref="Unit.TryMoveToTile"/> claims the tile and the fog is recomputed in the same
    /// frame), so a walk cycle has nothing to run for unless something takes time; the sprite
    /// catching up with the tile is that something, and having both here means "is it walking" is
    /// one question with one answer rather than a timer kept in step with a motion.
    ///
    /// Built at runtime by <see cref="Unit.Init"/> rather than placed on the prefab, like the
    /// capability badges are: what a unit is drawn with comes off its blueprint, so there is nothing
    /// to author per prefab.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        private UnitAnimationSet set;
        private SpriteRenderer spriteRenderer;

        // How fast all of this is drawn, held as the live asset rather than as a number copied off
        // it: the multiplier is applied per frame, so a speed changed mid-play - mid-step, even -
        // applies at once and nothing has to be told about it.
        private AnimationSettings animationSettings;

        // Where the sprite still has to walk to. A move is one action per tile stepped on, so the
        // steps arrive one at a time and are walked in order - the corner around a mountain is walked
        // rather than cut, without anything here knowing what a path is.
        private readonly Queue<Vector3> steps = new();

        // The step being walked right now, and how fast it is being walked. The speed is worked out
        // once when the step begins rather than per frame: taken from the gap as it closes it would
        // shrink with it, so the unit would creep in ever more slowly and never quite arrive.
        private Vector3 activeStep;
        private float stepSpeed;
        private bool stepping;

        private UnitAnimationSet.Clip current;
        private int frame;
        private float frameTimer;

        // A blow, a flinch or a fall is played to its end before anything else is chosen: each is
        // something that happened, not a state the unit is in.
        private bool playingOnce;

        // What the one being played now has to say when it runs out - hiding the unit, for a fall.
        private Action currentFinished;

        // What has happened to the unit and is still waiting to be shown. A queue rather than one
        // slot because a single exchange gives a unit two of them: the defender is struck and then
        // strikes back, and both are said in the same frame. Played in the order they were said, so
        // an exchange reads as one - a swing, a flinch, the answer, a flinch - rather than as
        // whichever half arrived last.
        private readonly Queue<OneShot> oneShots = new();

        // How long the one at the head of that queue has been held back. A flinch is said in the
        // same frame as the swing that caused it and is drawn by an animator of its own, so it waits
        // out its beat here rather than being said late by whoever struck.
        private float waited;

        // The flinch queued and not yet given the number that belongs to it. A strike says the
        // flinch and moves the health in the same frame, so the number is known a moment after the
        // flinch is queued and has to be hung on it rather than passed in with it.
        private OneShot pendingHit;

        private sealed class OneShot
        {
            public readonly UnitAnimationSet.Clip Clip;
            public readonly Action Finished;

            // What this has to say the moment it begins to be drawn - the damage number, which
            // belongs to the blow rather than to the health that moved a beat earlier. Settable
            // after the fact, which is the whole reason a one-shot is an object and not a value.
            public Action Drawn;

            // Which way the unit is turned before this is drawn, as a world x to turn towards, and
            // null for something that has no side to it. Carried with the clip rather than applied
            // when the strike is said, for the same reason the clip is: a unit that walks into range
            // says its swing before it sets off, and turning then would turn it out of the walk.
            public readonly float? FaceTowardsX;

            // How long this is held back once it comes to the head of the queue, in the seconds
            // animations are measured in. Zero for everything that is drawn the moment it is said.
            public readonly float Delay;

            public OneShot(UnitAnimationSet.Clip clip, Action finished, float? faceTowardsX, float delay)
            {
                Clip = clip;
                Finished = finished;
                FaceTowardsX = faceTowardsX;
                Delay = delay;
            }
        }

        private bool IsWalking => stepping || steps.Count > 0;

        /// <summary>
        /// The global animation speed, as a multiplier on both halves of what is drawn - the walk
        /// and the frames - so doubling it halves how long the whole thing takes rather than only
        /// its frames. Full speed with no asset injected, so a unit still animates.
        /// </summary>
        private float Speed => animationSettings != null ? animationSettings.Speed : 1f;

        /// <summary>
        /// The beat a flinch waits out before it is drawn. None with no asset injected, which is the
        /// unit being struck flinching in the frame it is struck in.
        /// </summary>
        private float HitDelay => animationSettings != null ? animationSettings.HitDelay : 0f;

        /// <summary>
        /// Whether the unit can be seen at all. The fog hides a unit by deactivating the object the
        /// sprite is on, so a unit behind it has nothing to show and walks its steps at once - which
        /// is what keeps one from being revealed halfway between two tiles.
        /// </summary>
        private bool IsVisible => spriteRenderer != null && spriteRenderer.gameObject.activeInHierarchy;

        /// <summary>
        /// Hangs an animator on <paramref name="unit"/>, or nothing at all when its blueprint
        /// authors no frames - a unit with no set keeps the still sprite the spawner gave it and
        /// arrives on its tiles the moment the rules say it does.
        /// </summary>
        public static UnitAnimator Create(Unit unit, UnitAnimationSet set, AnimationSettings animationSettings)
        {
            if (unit == null || set == null)
                return null;

            var spriteRenderer = unit.GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer == null)
                return null;

            var animator = unit.gameObject.AddComponent<UnitAnimator>();
            animator.set = set;
            animator.spriteRenderer = spriteRenderer;
            animator.animationSettings = animationSettings;

            return animator;
        }

        /// <summary>
        /// Walks to <paramref name="position"/> rather than appearing there. Queued, so the steps of
        /// one move - which are all taken in the frame the move is executed - are walked one after
        /// the other.
        /// </summary>
        public void StepTo(Vector3 position)
        {
            steps.Enqueue(position);
        }

        /// <summary>
        /// Puts the unit at <paramref name="position"/> at once and drops whatever it had left to
        /// walk. What a spawn and an undo do: neither is a step being taken, and a restored board
        /// must not be walked into.
        /// </summary>
        public void SnapTo(Vector3 position)
        {
            steps.Clear();
            stepping = false;
            transform.position = position;
        }

        /// <summary>
        /// Drops whatever has happened to the unit and not yet been shown, without letting any of it
        /// finish - a fall taken back by an undo must not go on to hide the unit it was taken back
        /// from. What a restored unit is given; the walk it is put back on is dropped by
        /// <see cref="SnapTo"/>, which is a separate thing to forget.
        /// </summary>
        public void Cancel()
        {
            oneShots.Clear();
            currentFinished = null;
            playingOnce = false;
            waited = 0f;
            pendingHit = null;
        }

        /// <summary>
        /// Draws a strike with <paramref name="kind"/>. Said once per blow, so a retaliation animates
        /// the unit that answers as surely as the first swing animates the one that started it.
        ///
        /// <paramref name="faceTowardsX"/> is where the blow is aimed - the unit is turned towards it
        /// as the swing begins, which for a unit that walked into range is after it has arrived.
        /// </summary>
        public void PlayAttack(WeaponKind kind, float? faceTowardsX = null)
        {
            Enqueue(set.AttackFor(kind), faceTowardsX: faceTowardsX);
        }

        /// <summary>
        /// Draws the unit taking a blow. Said for every strike that lands on it, an absorbed one
        /// included: a hit shrugged off still has to read as a hit, which is the same reason
        /// <see cref="Unit.ShowAbsorbedHit"/> exists.
        ///
        /// The one animation that is not drawn the moment it is said: the swing that caused it is
        /// announced in the same frame and drawn by another animator, so the flinch waits out
        /// <see cref="AnimationSettings.HitDelay"/> and lands during the blow rather than before it.
        /// <paramref name="afterASwing"/> is what that beat is for, so anything hurting a unit
        /// without one - a bleed ticking over - passes false and is drawn at once.
        /// </summary>
        public void PlayHit(bool afterASwing = true)
        {
            pendingHit = Enqueue(set.For(UnitAnimation.Hit), delay: afterASwing ? HitDelay : 0f);
        }

        /// <summary>
        /// Says <paramref name="said"/> when the flinch just queued is drawn rather than now - what
        /// the damage number is handed over with, so it appears with the blow instead of when the
        /// rules resolved it, which is a beat earlier and a whole walk earlier for a unit that
        /// stepped into range.
        ///
        /// False when there is no flinch to hang it on - no frames authored, or nothing struck - and
        /// then the caller says it at once. Spent on the one flinch, so the next number said without
        /// a blow behind it is not hung on the same one.
        /// </summary>
        public bool SayWithHit(Action said)
        {
            if (pendingHit == null || said == null)
                return false;

            pendingHit.Drawn += said;
            pendingHit = null;

            return true;
        }

        /// <summary>
        /// Draws the unit falling, and says whether it will. <paramref name="finished"/> is called
        /// once the fall has been seen - the unit is off the board the moment it dies, so what
        /// lingers is only its sprite, and this is what takes that away.
        ///
        /// False when no fall is authored, and then nothing is queued: the caller hides the unit at
        /// once rather than waiting for something that will never come.
        /// </summary>
        public bool PlayDeath(Action finished)
        {
            return Enqueue(set.For(UnitAnimation.Death), finished) != null;
        }

        /// <summary>
        /// Queues <paramref name="clip"/>, and hands it back so a word that is only known afterwards
        /// can still be hung on it. Null when nothing is authored - and then nothing is queued
        /// either, so nothing holds a callback that would never be called, which is what a fall
        /// depends on being able to trust.
        /// </summary>
        private OneShot Enqueue(UnitAnimationSet.Clip clip, Action finished = null, float? faceTowardsX = null,
            float delay = 0f)
        {
            if (clip == null)
                return null;

            var oneShot = new OneShot(clip, finished, faceTowardsX, delay);
            oneShots.Enqueue(oneShot);

            return oneShot;
        }

        private void Update()
        {
            Walk();

            if (!playingOnce)
                Choose();

            Advance();
        }

        private void Walk()
        {
            if (!stepping)
            {
                if (steps.Count == 0)
                    return;

                BeginStep(steps.Dequeue());
            }

            // Nothing to show, so nothing to wait for: the whole remaining path is spent at once.
            // Still one step at a time rather than straight to the last of them, so the unit is left
            // facing the way its final step went rather than the way the whole path led.
            if (!IsVisible)
            {
                while (steps.Count > 0)
                {
                    transform.position = activeStep;
                    BeginStep(steps.Dequeue());
                }

                transform.position = activeStep;
                stepping = false;
                return;
            }

            // The global speed is folded in here rather than into stepSpeed, so a speed changed
            // while a unit is walking applies to the step it is on.
            transform.position = Vector3.MoveTowards(transform.position, activeStep,
                stepSpeed * Speed * Time.deltaTime);

            if (transform.position == activeStep)
                stepping = false;
        }

        /// <summary>
        /// Takes up <paramref name="step"/> as the one being walked now. Turned as the step is begun
        /// rather than as the move is planned: a move is one action per tile stepped on, so a path
        /// around a corner turns into each of its tiles instead of pointing at where it ends up.
        /// </summary>
        private void BeginStep(Vector3 step)
        {
            FaceTowards(step.x);

            // Timed per step rather than given a speed, so a step onto a hill - which is longer,
            // being also a step upwards - takes as long as a step across flat ground.
            stepSpeed = Vector3.Distance(transform.position, step) / set.SecondsPerStep;
            activeStep = step;
            stepping = true;
        }

        /// <summary>
        /// Turns the unit towards <paramref name="worldX"/>. Sprites are drawn facing right, so a
        /// target to the left is the mirrored one - the same convention
        /// <see cref="Feedback.AttackPreview"/> stands its ghost with.
        ///
        /// Only the width is asked, since that is all a mirrored sprite can say: on the isometric
        /// grid every step changes it, so a walk always turns. Two tiles of equal width - straight up
        /// or down the screen, which only a reach of more than one can span - have no side to them,
        /// and the unit is left facing the way it already was.
        /// </summary>
        private void FaceTowards(float worldX)
        {
            if (spriteRenderer == null)
                return;

            var delta = worldX - transform.position.x;

            if (Mathf.Approximately(delta, 0f))
                return;

            spriteRenderer.flipX = delta < 0f;
        }

        private void Choose()
        {
            // What happened is shown once the walk is over. Held until then rather than dropped: a
            // unit that walked into range still has to be seen to strike.
            if (oneShots.Count > 0 && !IsWalking)
            {
                var next = oneShots.Peek();

                // Its beat is counted at the speed everything else is drawn at, so a flinch keeps
                // its place in the swing rather than drifting out of it as the speed is changed.
                // The wait runs from where the queue got to, not from when the blow was said: an
                // exchange is a swing, a flinch, the answer and its flinch, each after the last.
                if (waited < next.Delay)
                {
                    waited += Time.deltaTime * Speed;
                }
                else
                {
                    oneShots.Dequeue();
                    waited = 0f;

                    // Its number is no longer waiting for it, whether one was ever hung on it.
                    if (next == pendingHit)
                        pendingHit = null;

                    if (next.FaceTowardsX.HasValue)
                        FaceTowards(next.FaceTowardsX.Value);

                    currentFinished = next.Finished;
                    Play(next.Clip, once: true);

                    // Last, so what it says is said over the frame it is said with - the damage
                    // number over the flinch it belongs to rather than over the frame before it.
                    next.Drawn?.Invoke();

                    return;
                }
            }

            Play(set.For(IsWalking ? UnitAnimation.Move : UnitAnimation.Idle), once: false);
        }

        private void Play(UnitAnimationSet.Clip clip, bool once)
        {
            if (clip == null)
                return;

            // A looping animation that is already running is left where it is, or it would never
            // leave its first frame.
            if (clip == current && !once)
                return;

            current = clip;
            frame = 0;
            frameTimer = 0f;
            playingOnce = once;

            Draw();
        }

        private void Advance()
        {
            if (current == null)
                return;

            frameTimer += Time.deltaTime * Speed;

            var secondsPerFrame = 1f / Mathf.Max(current.FramesPerSecond, 0.01f);

            while (frameTimer >= secondsPerFrame)
            {
                frameTimer -= secondsPerFrame;
                frame++;

                if (frame < current.Frames.Count)
                    continue;

                // Run out. A one-shot rests on its last frame until the next Update chooses what the
                // unit is doing now; anything else starts over.
                if (playingOnce || !current.Loop)
                {
                    playingOnce = false;
                    frame = current.Frames.Count - 1;

                    // Spent on this one showing: taken before it is called, so whatever it does -
                    // hiding the unit, for a fall - cannot be done twice.
                    var finished = currentFinished;
                    currentFinished = null;
                    finished?.Invoke();

                    break;
                }

                frame = 0;
            }

            Draw();
        }

        private void Draw()
        {
            if (spriteRenderer == null || current == null || current.IsEmpty)
                return;

            var sprite = current.Frames[Mathf.Clamp(frame, 0, current.Frames.Count - 1)];

            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
