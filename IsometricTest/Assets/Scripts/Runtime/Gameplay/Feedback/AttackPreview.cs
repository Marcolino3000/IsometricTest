using System.Linq;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    /// <summary>
    /// Preview shown while a friendly unit is selected and an enemy unit is hovered: a
    /// half-transparent ghost of the attacker on the tile it would attack from (only when it
    /// would have to move first) and a red line from the attack position to the target.
    /// Purely visual - planning and validation stay in <see cref="Runtime.Gameplay.Actions.ActionExecutor"/>.
    /// Created at runtime by the Initiator, so it needs no scene object.
    /// </summary>
    public class AttackPreview : MonoBehaviour
    {
        [SerializeField] private Color lineColor = new(1f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private float ghostAlpha = 0.5f;

        private TileSpawner tileSpawner;
        private SpriteRenderer ghostRenderer;
        private LineRenderer lineRenderer;

        public void Setup(Selector selector, TileSpawner tileSpawnerArg)
        {
            tileSpawner = tileSpawnerArg;
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void Awake()
        {
            CreateGhost();
            CreateLine();
            Hide();
        }

        // Reacting to every selection change (instead of being driven by the action planner) means
        // the preview also disappears on deselection, turn reset and other paths that never
        // explicitly clear an attack plan.
        private void HandleSelectionChanged(ChangeEvent<Selection> changeEvent)
        {
            var selection = changeEvent.NewValue;

            if (selection.Status == SelectionStatus.SelectionEnemyHover)
                Show(selection.SelectedUnit, selection.HoveredUnit);
            else
                Hide();
        }

        private void Show(Unit attacker, Unit target)
        {
            if (attacker == null || target == null)
            {
                Hide();
                return;
            }

            var attackFromTile = GetAttackFromTile(attacker, target);
            var attackerSprite = attacker.GetComponentInChildren<SpriteRenderer>();
            var targetSprite = target.GetComponentInChildren<SpriteRenderer>();

            Vector3 lineStart;
            if (attackFromTile != attacker.CurrentState.Position)
            {
                PlaceGhost(attacker, attackerSprite, attackFromTile, targetSprite);
                lineStart = ghostRenderer.bounds.center;
            }
            else
            {
                // already in range - the attack comes from where the unit stands, no ghost needed
                ghostRenderer.enabled = false;
                lineStart = attackerSprite.bounds.center;
            }

            DrawLine(lineStart, targetSprite.bounds.center, attackerSprite);
        }

        private Tile GetAttackFromTile(Unit attacker, Unit target)
        {
            var path = tileSpawner.GetAttackApproachPath(attacker, target.CurrentState.Position);
            return path.LastOrDefault() ?? attacker.CurrentState.Position;
        }

        private void PlaceGhost(Unit attacker, SpriteRenderer attackerSprite, Tile attackFromTile, SpriteRenderer targetSprite)
        {
            // Stand the ghost on the tile the same way units are placed, keeping the sprite child's
            // local offset so it lines up like a real unit.
            var rootPosition = tileSpawner.GridIndexToWorldPosition(attackFromTile.Position)
                               + Vector3.up * attackFromTile.HeightOffset;
            var spriteOffset = attackerSprite.transform.position - attacker.transform.position;

            ghostRenderer.transform.position = rootPosition + spriteOffset;
            ghostRenderer.transform.localScale = attackerSprite.transform.lossyScale;

            ghostRenderer.sprite = attackerSprite.sprite;
            ghostRenderer.sortingLayerID = attackerSprite.sortingLayerID;
            ghostRenderer.sortingOrder = attackerSprite.sortingOrder;

            var ghostColor = attackerSprite.color;
            ghostColor.a = ghostAlpha;
            ghostRenderer.color = ghostColor;

            // face the target like an attacker would (unit sprites face right by default)
            ghostRenderer.flipX = targetSprite.bounds.center.x < ghostRenderer.transform.position.x;

            ghostRenderer.enabled = true;
        }

        private void DrawLine(Vector3 start, Vector3 end, SpriteRenderer attackerSprite)
        {
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            // one above the unit sprites so the attack reads clearly even across other units
            lineRenderer.sortingLayerID = attackerSprite.sortingLayerID;
            lineRenderer.sortingOrder = attackerSprite.sortingOrder + 1;

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            lineRenderer.enabled = true;
        }

        private void Hide()
        {
            ghostRenderer.enabled = false;
            lineRenderer.enabled = false;
        }

        private void CreateGhost()
        {
            var ghostObject = new GameObject("Ghost");
            ghostObject.transform.SetParent(transform, false);
            ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        }

        private void CreateLine()
        {
            var lineObject = new GameObject("Line");
            lineObject.transform.SetParent(transform, false);

            lineRenderer = lineObject.AddComponent<LineRenderer>();
            // Same shader as the default sprite material the tiles use, so it works with URP and vertex colors.
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 4;
        }
    }
}
