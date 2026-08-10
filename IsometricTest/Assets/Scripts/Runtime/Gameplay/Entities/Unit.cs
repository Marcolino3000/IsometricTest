using Data;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Controls;
using Runtime.Gameplay.Fog;
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
        public int MaxHealth => blueprint.DefaultState.Health;

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
            GameStateManager gameStateManagerArg, FogOfWar fogOfWarArg)
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
            
            healthBar.Setup(blueprint.DefaultState.Health);
            actionExecutor.Setup(this, tileSpawner);
            
            TileHighlighter.Setup(currentState, tileSpawner);
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
        public void RestoreSnapshot(Tile tile, int health, int actionPoints)
        {
            restoringSnapshot = true;

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
                currentState.ActionPoints = blueprint.DefaultState.ActionPoints;
        }
    }

    public enum Team
    {
        Player,
        Opponent
    }
}