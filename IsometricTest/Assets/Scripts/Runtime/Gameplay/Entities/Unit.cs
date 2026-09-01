using Data;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Fog;
using Runtime.Gameplay.Global;
using UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.Gameplay.Entities
{
    public class Unit : MonoBehaviour, IClickable
    {
        public UnitState CurrentState => currentState;
        public UnitBlueprint Blueprint => blueprint;
        public ActionExecutor ActionExecutor => actionExecutor;

        /// <summary>
        /// False once the unit has been removed from play. Removed units are kept around hidden
        /// instead of destroyed so undo can put them back (see <see cref="SetInPlay"/>), which is why
        /// callers have to ask this rather than null-check the reference.
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        /// <summary>
        /// The health the blueprint starts the unit with, which is also its ceiling: there is no
        /// separate maximum on <see cref="UnitState"/>, and the health bar is built from this very
        /// value. What healing clamps against.
        /// </summary>
        public int MaxHealth => blueprint.DefaultState.Health + currentState.GetBonus(UnitStat.Health);

        /// <summary>
        /// The action points the unit is given at the start of each of its turns - the counterpart to
        /// <see cref="MaxHealth"/>, read off the blueprint the same way. What anything asking "what
        /// could this unit do on its turn" has to budget with: <see cref="UnitState.ActionPoints"/> is
        /// what is left of them, which for an enemy on the player's turn is usually nothing.
        /// </summary>
        public int MaxActionPoints => blueprint.DefaultState.ActionPoints + currentState.GetBonus(UnitStat.ActionPoints);

        /// <summary>
        /// How far the unit sees before the ground it stands on has its say. The third of the queries
        /// that fold a permanent bonus into a base; unlike the other two the base is on the state
        /// rather than the blueprint, which is why nobody asks the state directly.
        ///
        /// The base of a base: what the fog is drawn from and what the AI weighs a step by is
        /// <see cref="Global.SightRules.GetSightRange(Unit)"/>, which folds the terrain's traits into
        /// this the way <c>CombatRules.GetEffectiveAttackRange</c> folds them into a weapon's range.
        /// </summary>
        public int SightRange => currentState.SightRange + currentState.GetBonus(UnitStat.SightRange);

        [Header("Debug")]
        [SerializeField] private UnitState currentState;

        [Header("References")]
        public UnitTileHighlighter TileHighlighter;
        public UnitOutline Outline;

        [SerializeField] private UnitBlueprint blueprint;
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private UnitSpawner unitSpawner;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private FogOfWar fogOfWar;
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private ActionExecutor actionExecutor;

        // Built in Init off the blueprint, and null for a unit whose blueprint authors no frames -
        // which is what leaves that unit standing still and stepping onto its tiles at once.
        private UnitAnimator animator;

        // What the popup for the next damage taken says beside the number, e.g. "Crit!". Handed over
        // by whoever resolved the strike rather than looked up here, since the popup is raised by the
        // health setter and that knows only how much changed.
        private string damageNote;

        // Set while undo/redo puts recorded values back, so the step onto a tile is put back rather
        // than walked. Only the movement needs it: a value change says for itself why it moved, and
        // whoever reacts to one reads the reason off the event instead of a flag kept out here.
        private bool restoringSnapshot;

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnReset -= HandleTurnReset;

            if (currentState != null)
            {
                currentState.HealthChanged -= HandleHealthChanged;
                currentState.ActionPointsChanged -= HandleActionPointsChanged;
            }
        }

        /// <summary>
        /// Builds the unit from its blueprint. The blueprint is handed in rather than authored on the
        /// prefab: a kind of unit is one asset, and the prefab is the shared body every kind is drawn
        /// with. The serialized field is only a fallback, for a unit placed in the scene by hand.
        /// </summary>
        public void Init(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, Team team,
            GameStateManager gameStateManagerArg, FogOfWar fogOfWarArg, GameRules gameRules,
            AnimationSettings animationSettings, UnitBlueprint unitBlueprint = null)
        {
            if (unitBlueprint != null)
                blueprint = unitBlueprint;

            currentState = blueprint.DefaultState;
            currentState.Team = team;
            IsAlive = true;
            currentState.HealthChanged += HandleHealthChanged;
            currentState.ActionPointsChanged += HandleActionPointsChanged;

            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            fogOfWar = fogOfWarArg;

            gameStateManager = gameStateManagerArg;
            gameStateManager.TurnReset += HandleTurnReset;

            healthBar.Setup(MaxHealth);
            actionExecutor.Setup(this, tileSpawner);

            TileHighlighter.Setup(this, tileSpawner, gameRules);

            // Before the badges only because both are built here: what the unit is drawn with is its
            // blueprint's business, so there is nothing to author on the prefab either way.
            animator = UnitAnimator.Create(this, blueprint.Animations, animationSettings);

            CreateBadges(gameRules);
        }

        /// <summary>
        /// Hangs the capability badges over the unit. Built at runtime rather than placed on the
        /// prefab, like the floating text is: how many badges there are depends on what the unit
        /// carries, and what it carries changes while the match runs. They borrow the health bar's
        /// world-space panel, so they scale and sort with the bars, and being a child of the unit is
        /// what puts them under <see cref="SetRevealed"/> - an enemy in the fog gives nothing away.
        /// </summary>
        private void CreateBadges(GameRules gameRules)
        {
            if (gameRules == null || !gameRules.ShowUnitBadges)
                return;

            // Nothing is held on to: the row keeps itself in step with what the unit carries, so
            // there is nothing here that would have to tell it when that changes.
            UnitBadges.Create(this, healthBar.GetComponent<UIDocument>().panelSettings);
        }

        /// <summary>
        /// Puts a short message over this unit - why something it was told to do did not happen. Here
        /// rather than at the caller because the popups borrow the health bar's world-space panel,
        /// which is the unit's own business.
        /// </summary>
        public void ShowNotice(string text)
        {
            FloatingText.ShowNotice(text, transform.position, healthBar.GetComponent<UIDocument>().panelSettings);
        }

        /// <summary>
        /// What the popup for the damage this unit is about to take says beside the number - "Crit!"
        /// and whatever else a trait had to say about the strike (see
        /// <see cref="Global.StrikeNotes"/>). Said just before the health is reduced, and spent on
        /// that one popup: a hit that has nothing to say leaves the previous hit's word behind.
        /// </summary>
        public void NoteNextDamage(string note)
        {
            damageNote = note;
        }

        /// <summary>
        /// Raises one of the unit's stats for the rest of the match - what an item that improves the
        /// character rather than spending itself on the moment does. The number goes on
        /// <see cref="UnitState"/>, so it travels with the history snapshot and an undo takes it back
        /// along with the item that granted it.
        ///
        /// What it takes for a raise to be *felt* differs per stat, and this is the one place that
        /// knows: sight has to be recomputed or the ground it uncovers stays dark until the next step;
        /// action points are handed out at the start of a turn, so the same amount is added to what is
        /// left of this one, and the drink is worth something on the turn it is drunk; health is a row
        /// of blobs built once, so the row is rebuilt and the new room filled - a ceiling with nothing
        /// under it would be no gift.
        /// </summary>
        public void GrantStatBonus(UnitStat stat, int amount)
        {
            if (amount == 0)
                return;

            currentState.AddBonus(stat, amount);

            switch (stat)
            {
                case UnitStat.SightRange:
                    fogOfWar.Recompute();
                    break;

                case UnitStat.ActionPoints:
                    actionExecutor.RefreshActionPointsBar();
                    currentState.ActionPoints += amount;
                    break;

                case UnitStat.Health:
                    RefreshHealthBar();
                    currentState.Health += amount;
                    break;
            }
        }

        /// <summary>
        /// Brings the health bar in line with the unit's maximum, which an item can move. Sets the
        /// blobs shown as well as how many there are: the row is rebuilt from nothing, and a restore
        /// that changes the maximum without changing the health would otherwise leave it blank.
        /// </summary>
        private void RefreshHealthBar()
        {
            healthBar.SetMaxBlobs(MaxHealth);
            healthBar.SetBlobAmount(currentState.Health);
        }

        /// <summary>
        /// The bar follows every change whatever caused it; the popup answers gameplay only. The
        /// reason comes off the event rather than a flag set around the restore, so anything else
        /// that reacts to a hit later - a sound, a flash, a shake - can tell them apart the same way
        /// without a guard of its own.
        /// </summary>
        private void HandleHealthChanged(Runtime.Core.State.ChangeEvent<int> changeEvent)
        {
            healthBar.SetBlobAmount(changeEvent.NewValue);

            var delta = changeEvent.NewValue - changeEvent.PreviousValue;

            if (delta == 0 || changeEvent.Reason != ChangeReason.Gameplay)
            {
                // A hit taken back by an undo says nothing, and its word must not carry to the next one.
                damageNote = null;
                return;
            }

            ShowHealthPopup(delta);
        }

        /// <summary>
        /// A hit that took nothing off - fully absorbed. Said by whoever resolved the strike, since
        /// <see cref="UnitState.Health"/> raises nothing when the number does not move, and a hit
        /// that is shrugged off has to read as a hit rather than as nothing having happened.
        /// </summary>
        public void ShowAbsorbedHit()
        {
            ShowHealthPopup(0);
        }

        /// <summary>
        /// The number over the unit's head, with whatever the strike had to say beside it. The note
        /// is spent here, so a hit that has nothing to say cannot inherit the last one's word.
        /// </summary>
        private void ShowHealthPopup(int delta)
        {
            // Popups reuse the health bar's world-space panel settings so they render like the unit bars.
            var panelSettings = healthBar.GetComponent<UIDocument>().panelSettings;

            var note = damageNote;
            damageNote = null;

            if (delta > 0)
                FloatingText.ShowHeal(delta, transform.position, panelSettings);
            else
                FloatingText.ShowDamage(delta, transform.position, panelSettings, note);
        }
        
        private void HandleActionPointsChanged(Runtime.Core.State.ChangeEvent<int> changeEvent)
        {
            actionExecutor.HandleActionPointsChanged(changeEvent.NewValue);
        }

        public bool TryPlaceAtTile(Tile selectedTile)
        {
            if (selectedTile == null)
            {
                Debug.LogWarning("Selected tile is null");
                return false;
            }

            if (selectedTile.IsOccupied || !selectedTile.IsPassable)
                return false;

            PlaceOnTile(selectedTile);
            return true;
        }

        public bool TryMoveToTile(Tile selectedTile)
        {
            PlaceOnTile(selectedTile);
            return true;
        }

        public void Remove()
        {
            // A unit falls once. Removing an already removed one does nothing to the board, but it
            // would announce a second fall - and the loot would leave a second lot of spoils for it.
            if (!IsAlive)
                return;

            currentState.Position.SetUnit(null);
            unitSpawner.RemoveUnit(this);

            // Last, with the unit off the board and the tile it stood on free: what a fall leaves
            // behind is nobody's business here, the way what lies on a tile it walks onto is not.
            unitSpawner.NotifyRemoved(this);
        }

        /// <summary>
        /// Takes the unit off the board or puts it back. A removed unit is kept around - hidden and
        /// unclickable - rather than destroyed, so undo can bring it back with every reference to it
        /// still intact. It is hidden through <see cref="SetRevealed"/> instead of by deactivating the
        /// GameObject on purpose: disabling a UIDocument rebuilds its visual tree from the source
        /// asset, which would wipe the blobs the health and action point bars build once at setup.
        /// </summary>
        public void SetInPlay(bool inPlay)
        {
            IsAlive = inPlay;

            if (inPlay)
            {
                // A fall taken back is a fall that never happened: it must not go on to hide the
                // unit it has just been taken back from.
                animator?.Cancel();
                SetPickable(true);
                SetRevealed(true);

                return;
            }

            // The unit is off the board already - its tile is free and nothing can reach it. Only
            // the sprite lingers, long enough for the fall to be seen, and takes itself away when it
            // has been. The bars and badges go at once: they say what a unit still in play has left,
            // and it has nothing left.
            SetOverlaysRevealed(false);
            SetPickable(false);

            if (animator == null || !animator.PlayDeath(() => SetSpriteRevealed(false)))
                SetSpriteRevealed(false);
        }

        /// <summary>
        /// Puts the unit back into a state undo/redo recorded earlier. Deliberately skips everything a
        /// real action would trigger around it: no damage popup, and no fog recompute per unit - the
        /// caller does a single pass once every unit is back in place.
        /// </summary>
        public void RestoreSnapshot(Tile tile, int health, int actionPoints, int[] statBonuses)
        {
            restoringSnapshot = true;

            // Everything written from here says so: the values put back raise their events with
            // Restore on them, so a bar redraws and a damage popup stays away without being told.
            using var _ = currentState.Changing(ChangeReason.Restore);

            // A board is put back rather than played out again, the way the step onto a tile is put
            // back rather than walked. A unit that should be down is simply down - a redone kill has
            // queued its fall a moment ago, and this is what finishes it without it being watched.
            if (!IsAlive)
            {
                animator?.Cancel();
                SetRevealed(false);
            }

            // Before the vitals: both bars have to have room for the recorded values, and both
            // maxima are things an item moves. The wider sight a bonus may bring back needs no help -
            // the caller recomputes the fog once every unit is back in place.
            currentState.RestoreBonuses(statBonuses);
            RefreshHealthBar();
            actionExecutor.RefreshActionPointsBar();

            if (tile != null)
            {
                currentState.Position = tile;
                MoveTransformToTile(tile);

                // A removed unit keeps its recorded tile but must not occupy it.
                if (IsAlive)
                    tile.SetUnit(this);
            }

            currentState.Health = health;
            currentState.ActionPoints = actionPoints;

            // The action point bar may still be showing the cost of a plan that belongs to the state
            // we left; the restored points are what counts now.
            actionExecutor.ClearPreview();

            restoringSnapshot = false;
        }

        private void PlaceOnTile(Tile selectedTile)
        {
            var currentTile = currentState.Position;
            if(currentTile != null)
                currentTile.SetUnit(null);

            currentState.Position = selectedTile;
            MoveTransformToTile(selectedTile);

            selectedTile.SetUnit(this);

            fogOfWar.Recompute();

            // Last, with the unit standing there and the fog caught up: what lies on the ground is
            // nobody's business here, so the arrival is announced and whoever cares reads the tile.
            unitSpawner.NotifyEnteredTile(this);
        }

        /// <summary>
        /// Where the unit stands on the board - the tile's own place, which is what the transform is
        /// put at whether the sprite walks there or appears there.
        /// </summary>
        private Vector3 WorldPositionOf(Tile tile)
        {
            return unitSpawner.GridToWorldPosition(tile.Position) + Vector3.up * tile.HeightOffset;
        }

        /// <summary>
        /// Puts the unit on its tile at once, with nothing left to walk. What a spawn does: the unit
        /// is placed rather than moved, and a fresh one would otherwise walk in from wherever the
        /// prefab was instantiated.
        /// </summary>
        public void SnapToCurrentTile()
        {
            var tile = currentState.Position;

            if (tile == null)
                return;

            var position = WorldPositionOf(tile);

            transform.position = position;
            animator?.SnapTo(position);
        }

        /// <summary>
        /// Draws the unit striking - said once per blow by whoever resolves it, so a retaliation
        /// animates the unit that answers as surely as the first swing animates the one that started
        /// it. Which swing is drawn follows the weapon in hand, like everything else about a strike.
        /// </summary>
        public void PlayAttackAnimation()
        {
            if (animator == null || currentState.AttackAction == null)
                return;

            animator.PlayAttack(currentState.AttackAction.Kind);
        }

        /// <summary>
        /// Draws the unit taking a blow - said by whoever resolves the strike, like the swing that
        /// caused it, and for an absorbed hit too: a hit shrugged off still has to read as a hit.
        /// </summary>
        public void PlayHitAnimation()
        {
            animator?.PlayHit();
        }

        private void MoveTransformToTile(Tile tile)
        {
            var position = WorldPositionOf(tile);

            // The rules have already moved on - the tile is claimed and the fog recomputed in this
            // same frame. All the animator does is take its time getting there, and a restore is the
            // one arrival that must not: undo puts a board back, it does not walk into it.
            if (animator == null || restoringSnapshot)
            {
                transform.position = position;
                animator?.SnapTo(position);
                return;
            }

            animator.StepTo(position);
        }

        /// <summary>
        /// Shows or hides the unit's visuals for fog of war. Friendly units are always revealed;
        /// enemy units are hidden unless they stand on a tile the viewing team can currently see.
        /// Toggling the sprite object also disables its collider, so hidden units can't be hovered or clicked.
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            SetSpriteRevealed(revealed);
            SetOverlaysRevealed(revealed);
        }

        /// <summary>
        /// The unit itself. Split from the overlays for the one moment the two part company: a unit
        /// that has fallen keeps its sprite until the fall has been seen, while everything saying
        /// what it has left goes at once.
        /// </summary>
        private void SetSpriteRevealed(bool revealed)
        {
            // Outline lives on the "Sprite" child alongside the SpriteRenderer, collider and Clickable.
            if (Outline != null)
                Outline.gameObject.SetActive(revealed);
        }

        /// <summary>
        /// Whether the unit can be picked out of the world by <see cref="Global.Raycaster"/>. Only
        /// ever off for the moment a fallen unit's sprite outlives it: it is not on the board any
        /// more and must not be hovered or clicked, and deactivating the object - which is what
        /// normally takes its collider with it - is the very thing being waited on.
        /// </summary>
        private void SetPickable(bool pickable)
        {
            foreach (var unitCollider in GetComponentsInChildren<Collider2D>(true))
                unitCollider.enabled = pickable;
        }

        /// <summary>
        /// Health bar, action-point bar and badges - the UIDocuments hung over the unit, hidden so an
        /// out-of-sight enemy isn't given away by floating UI. Toggles the root element's display
        /// rather than the GameObject: disabling a UIDocument rebuilds its visual tree from the
        /// source asset, which would wipe the blobs HealthBar/ActionsPointsBar add once at Setup.
        /// </summary>
        private void SetOverlaysRevealed(bool revealed)
        {
            foreach (var document in GetComponentsInChildren<UIDocument>(true))
            {
                if (document.rootVisualElement != null)
                    document.rootVisualElement.style.display = revealed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private void HandleTurnReset(Runtime.Core.State.ChangeEvent<State> changeEvent)
        {
            // Removed units stay subscribed (they are only hidden, so undo can bring them back) but
            // must keep the action points they were recorded with.
            if (!IsAlive)
                return;

            if (changeEvent.NewValue.Team == currentState.Team)
                currentState.ActionPoints = MaxActionPoints;
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}