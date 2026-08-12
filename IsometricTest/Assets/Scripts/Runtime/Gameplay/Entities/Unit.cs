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

        private int lastHealth;

        // Set while undo/redo puts recorded values back, so replaying a hit does not replay its
        // damage popup.
        private bool restoringSnapshot;

        private void OnDestroy()
        {
            if (gameStateManager != null)
                gameStateManager.TurnReset -= HandleTurnReset;
        }

        public void Init(TileSpawner tileSpawnerArg, UnitSpawner unitSpawnerArg, Team team,
            GameStateManager gameStateManagerArg, FogOfWar fogOfWarArg, GameRules gameRules)
        {
            currentState = blueprint.DefaultState;
            currentState.Team = team;
            lastHealth = currentState.Health;
            IsAlive = true;
            currentState.SetValueChangedCallbacks(HealthChangedCallback, ActionPointsChangedCallback);

            tileSpawner = tileSpawnerArg;
            unitSpawner = unitSpawnerArg;
            fogOfWar = fogOfWarArg;

            gameStateManager = gameStateManagerArg;
            gameStateManager.TurnReset += HandleTurnReset;

            healthBar.Setup(MaxHealth);
            actionExecutor.Setup(this, tileSpawner);

            TileHighlighter.Setup(this, tileSpawner, gameRules);

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

        private void HealthChangedCallback(int amount)
        {
            healthBar.SetBlobAmount(amount);

            int delta = amount - lastHealth;
            lastHealth = amount;

            if (delta == 0 || restoringSnapshot)
                return;

            // Popups reuse the health bar's world-space panel settings so they render like the unit bars.
            var panelSettings = healthBar.GetComponent<UIDocument>().panelSettings;

            if (delta < 0)
                FloatingText.ShowDamage(delta, transform.position, panelSettings);
            else
                FloatingText.ShowHeal(delta, transform.position, panelSettings);
        }
        
        private void ActionPointsChangedCallback(int amount)
        {
            actionExecutor.HandleActionPointsChanged(amount);
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
            currentState.Position.SetUnit(null);
            unitSpawner.RemoveUnit(this);
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
            SetRevealed(inPlay);
        }

        /// <summary>
        /// Puts the unit back into a state undo/redo recorded earlier. Deliberately skips everything a
        /// real action would trigger around it: no damage popup, and no fog recompute per unit - the
        /// caller does a single pass once every unit is back in place.
        /// </summary>
        public void RestoreSnapshot(Tile tile, int health, int actionPoints, int[] statBonuses)
        {
            restoringSnapshot = true;

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
            lastHealth = health;
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

        private void MoveTransformToTile(Tile tile)
        {
            transform.position = unitSpawner.GridToWorldPosition(tile.Position) + Vector3.up * tile.HeightOffset;
        }

        /// <summary>
        /// Shows or hides the unit's visuals for fog of war. Friendly units are always revealed;
        /// enemy units are hidden unless they stand on a tile the viewing team can currently see.
        /// Toggling the sprite object also disables its collider, so hidden units can't be hovered or clicked.
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            // Outline lives on the "Sprite" child alongside the SpriteRenderer, collider and Clickable.
            if (Outline != null)
                Outline.gameObject.SetActive(revealed);

            // Health bar and action-point bar are UIDocuments; hide them so an out-of-sight enemy
            // isn't given away by floating UI. Toggle the root element's display rather than the
            // GameObject: disabling a UIDocument rebuilds its visual tree from the source asset,
            // which would wipe the blobs HealthBar/ActionsPointsBar add once at Setup.
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