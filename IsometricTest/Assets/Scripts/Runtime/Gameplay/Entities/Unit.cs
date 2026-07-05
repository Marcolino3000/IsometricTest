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

            // Popups reuse the health bar's world-space panel settings so they render like the unit bars.
            if (delta < 0)
                FloatingText.ShowDamage(delta, transform.position, healthBar.GetComponent<UIDocument>().panelSettings);
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

        private void PlaceOnTile(Tile selectedTile)
        {
            var currentTile = currentState.Position;
            if(currentTile != null)
                currentTile.SetUnit(null);

            currentState.Position = selectedTile;
            transform.position = unitSpawner.GridToWorldPosition(selectedTile.Position) + Vector3.up * selectedTile.HeightOffset;

            selectedTile.SetUnit(this);

            fogOfWar.Recompute();
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