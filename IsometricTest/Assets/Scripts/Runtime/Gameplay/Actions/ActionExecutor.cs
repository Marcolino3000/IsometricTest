using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using Runtime.Gameplay.History;
using Runtime.Gameplay.Items;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Actions
{
    public class  ActionExecutor : MonoBehaviour
    {
        [Header("Settings")] 
        // [SerializeField] private AttackActionData attackActionData;
        // [SerializeField] private MoveActionData moveActionData;

        [Header("References")]
        [SerializeField] private ActionsPointsBar actionsPointsBar;
        [SerializeField] private TileSpawner tileSpawner;

        [Tooltip("Which icon stands for which kind of action. Left empty, a previewed point falls " +
                 "back to the plain faded blob.")]
        [SerializeField] private ActionIconSet actionIcons;

        private Unit unit;
        private List<IUnitAction> plannedActions = new();

        // Rebuilt on every hovered tile, so it is kept rather than allocated per preview.
        private readonly List<Sprite> previewIcons = new();

        /// <summary>
        /// Plans a move and shows it - the previewed points on the action point bar and the
        /// highlighted path. What a hover asks for.
        /// </summary>
        public ConditionTestResult PlanMoveAction(ExecuteArgs executeArgs)
        {
            // Callers can hold on to a unit past its death (stale selection, an AI loop in the frame
            // its unit died — removed units stay around hidden so undo can restore them).
            // Without this guard a dead unit would still plan and act.
            if (unit == null || !unit.IsAlive)
                return new ConditionTestResult(false, -1);

            // The mover is handed over, so the route is the one this unit is priced for and allowed
            // on - a step MoveAction would refuse is never planned in the first place.
            var path = tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetTile,
                mover: unit.CurrentState);
            
            PlanMoveActionsFromPath(path);
            var result = TestConditionsForPlannedActions();

            SetActionsPointsBar();
            unit.TileHighlighter.HighlightTilesAlongPath(path, result.FailedConditionIndex);

            return result;
        }

        /// <summary>
        /// Plans a strike and shows what it would cost. See <see cref="PlanMoveAction"/>.
        /// </summary>
        public ConditionTestResult PlanAttackAction(ExecuteArgs executeArgs) =>
            PlanAttack(executeArgs, preview: true);

        /// <summary>
        /// Whether this unit could reach the target and strike it with the points it has - the same
        /// plan, asked as a question rather than shown.
        ///
        /// Apart from <see cref="PlanAttackAction"/> because a preview is an answer to a hover, and
        /// only the player hovers: the AI weighs every candidate target through here, and each plan
        /// it turned down used to be left standing on the bar over that unit's own head, showing
        /// fewer points than the unit actually had for the rest of the match. Nothing cleared it -
        /// <see cref="ClearPreview"/> only ever reaches the selected unit, which is always the
        /// player's.
        /// </summary>
        public ConditionTestResult TestAttackAction(ExecuteArgs executeArgs) =>
            PlanAttack(executeArgs, preview: false);

        private ConditionTestResult PlanAttack(ExecuteArgs executeArgs, bool preview)
        {
            // Same guard as PlanMoveAction: a dead attacker (or target) must not produce a plan.
            if (unit == null || !unit.IsAlive || executeArgs.TargetUnit == null || !executeArgs.TargetUnit.IsAlive)
                return new ConditionTestResult(false, -1);

            var targetTile = executeArgs.TargetUnit.CurrentState.Position;

            // Only move close enough that the target lands within attack range and in plain sight,
            // so ranged units stop short instead of walking right up to it - and walk on around a
            // mountain that stands in the way instead of stopping behind it.
            var pathIntoRange = tileSpawner.GetAttackApproachPath(unit, targetTile);

            var remainingAP = PlanMoveActionsFromPath(pathIntoRange);

            var attackFromTile = pathIntoRange.LastOrDefault() ?? unit.CurrentState.Position;

            var context = new ActionContext()
            {
                Unit = unit,
                TargetUnit = executeArgs.TargetUnit,
                ActionPoints = remainingAP,
                TargetTile = targetTile,
                FromTile = attackFromTile
            };

            plannedActions.Add(unit.CurrentState.AttackAction.CreateAction(context));

            if (preview)
                SetActionsPointsBar();

            return TestConditionsForPlannedActions();
        }

        private ConditionTestResult TestConditionsForPlannedActions()
        {
            int failedConditionIndex = -1;
            
            for (int i = 0; i < plannedActions.Count; i++)
            {
                if (!plannedActions[i].TestConditions())
                {
                    Debug.LogWarning("planned action is not valid.");
                    failedConditionIndex = i;
                    return new ConditionTestResult(false, failedConditionIndex);
                }
            }

            return new ConditionTestResult(true, failedConditionIndex);
        }

        private int PlanMoveActionsFromPath(List<Tile> path)
        {
            plannedActions.Clear();

            var availableActionPoints = unit.CurrentState.ActionPoints;

            foreach (var tile in path)
            {
                if(tile == path.First())
                    continue;

                var context = new ActionContext()
                {
                    TargetUnit = unit,
                    ActionPoints = availableActionPoints,
                    TargetTile = tile
                };

                var action = unit.CurrentState.MoveAction.CreateAction(context);
                plannedActions.Add(action);

                // use the action's own cost so difficult terrain (hills) is accounted for per step
                availableActionPoints -= action.Cost;
            }

            return availableActionPoints;
        }


        public void ExecuteMoveActions(ExecuteArgs executeArgs)
        {
            if(!PlanMoveAction(executeArgs).IsValid)
            {
                // A plan that is not run must not stay on the bar - nothing else would take it off.
                // Executing puts the bar right by itself, since spending the points redraws it.
                ClearPreview();
                return;
            }

            ExecutePlannedActions();

            ActionReporter.Report(ActionReport.Move(unit));
        }

        public void ExecuteAttackAction(ExecuteArgs executeArgs)
        {
            if(!PlanAttackAction(executeArgs).IsValid)
            {
                // See ExecuteMoveActions.
                ClearPreview();
                return;
            }

            var target = executeArgs.TargetUnit;

            ExecutePlannedActions();

            // Reported after the fact, and with the participants only: the history reads what actually
            // happened (damage, kills, points spent) off the state around the action.
            ActionReporter.Report(ActionReport.Attack(unit, target));
        }

        /// <summary>
        /// Plans one use of a self-targeted active item and shows what it would cost, without using
        /// it. What a hovered item slot answers with - the item bar's equivalent of hovering a tile,
        /// which is the only hover an item has: it is aimed at the character, so there is nothing on
        /// the map to highlight. Deliberately untested, like a move plan reaching further than the
        /// turn does: an item that cannot be afforded still shows what it wants.
        /// </summary>
        public bool PlanItemAction(ActiveItemData item)
        {
            // Same guard as the other plan methods: a dead unit must not plan.
            if (unit == null || !unit.IsAlive || item == null)
                return false;

            var context = new ActionContext()
            {
                Unit = unit,
                TargetUnit = unit,
                TargetTile = unit.CurrentState.Position,
                ActionPoints = unit.CurrentState.ActionPoints
            };

            plannedActions.Clear();
            plannedActions.Add(item.CreateAction(context));

            SetActionsPointsBar();

            return true;
        }

        /// <summary>
        /// Uses a self-targeted active item, and says whether it was actually used. Runs the same
        /// plan / test / execute path an attack does, so the cost is tested before anything happens,
        /// spent afterwards and announced once - which is what makes an item undoable without any
        /// history code of its own.
        /// </summary>
        public bool ExecuteItemAction(ActiveItemData item)
        {
            // Nothing planned means nothing to run: what the previous hover left behind must not be
            // executed in its place.
            if (!PlanItemAction(item))
                return false;

            if (!TestConditionsForPlannedActions().IsValid)
                return false;

            ExecutePlannedActions();

            // Said here rather than heard off the report, the way a strike and its flinch are said
            // from CombatRunner.ApplyDamage: this is the one place an item is spent, and only a use
            // that actually happened reaches it. A general presentation channel would subscribe to
            // the report instead - see the open decision on sound and VFX in CLAUDE.md.
            EffectAnimator.Play(EffectAnimation.ItemUsed, unit);

            ActionReporter.Report(ActionReport.UseItem(unit));

            return true;
        }

        private void ExecutePlannedActions()
        {
            int totalCost = 0;

            foreach (var action in plannedActions)
            {
                totalCost += action.Cost;
                action.ExecuteEffects();
            }

            unit.CurrentState.ActionPoints -= totalCost;
        }


        /// <summary>
        /// <paramref name="vitals"/> is the HUD row over the item slots, handed in for the player's
        /// character alone: its points are drawn there rather than over its head. Everything else
        /// about the bar is unchanged, since only where it hangs differs.
        /// </summary>
        public void Setup(Unit unit, TileSpawner tileSpawner, PlayerVitals vitals = null)
        {
            this.unit = unit;
            this.tileSpawner = tileSpawner;
            // The points a turn hands out, not what is left of one: that is how many blobs the row
            // needs, and SetBlobAmount clamps to them.
            if (vitals != null)
                actionsPointsBar.SetupIn(vitals.ActionPointRow, vitals.ActionPointBlob,
                    unit.MaxActionPoints);
            else
                actionsPointsBar.Setup(unit.MaxActionPoints);
        }

        /// <summary>
        /// Rebuilds the bar for a maximum that has moved - an item raising the unit's action points
        /// for good, or an undo taking that back. Here rather than on the unit because the bar is the
        /// executor's, like every other thing it shows about the points.
        /// </summary>
        public void RefreshActionPointsBar()
        {
            actionsPointsBar.SetMaxBlobs(unit.MaxActionPoints);
            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);
        }

        private int PlannedCost => plannedActions.Sum(action => action.Cost);
        
        private void SetActionsPointsBar()
        {
            var committed = unit.CurrentState.ActionPoints;
            var previewCost = Mathf.Min(PlannedCost, committed);
            actionsPointsBar.SetBlobAmount(committed - previewCost, PreviewIcons(previewCost));
        }

        /// <summary>
        /// One icon per action point the plan would spend - an action costing two points therefore
        /// claims two blobs. Cut off at <paramref name="affordableCost"/>, so a plan reaching further
        /// than the turn does shows only the part of it that can be paid for.
        ///
        /// Handed over **in reverse**, because the bar is spent from the right: the rightmost blob is
        /// the next point to go, so the plan's first step belongs there and the strike it walks up to
        /// ends up on the left, beside the points that survive it. Reading the row right to left is
        /// therefore reading the plan in order, and a blob vanishing is the step it stood for being
        /// taken.
        ///
        /// Here rather than in the bar because the plan is the executor's: the bar is handed sprites
        /// and never learns that actions exist.
        /// </summary>
        private IReadOnlyList<Sprite> PreviewIcons(int affordableCost)
        {
            previewIcons.Clear();

            foreach (var action in plannedActions)
            {
                var icon = actionIcons != null ? actionIcons.For(action.Kind) : null;

                for (int i = 0; i < action.Cost && previewIcons.Count < affordableCost; i++)
                    previewIcons.Add(icon);

                if (previewIcons.Count >= affordableCost)
                    break;
            }

            // Collected in the order the points are spent, drawn in the order they are shown - and the
            // two run opposite ways. Reversed here rather than read backwards by the bar, which is
            // handed a plain left-to-right list and has no idea which end of it is spent first.
            previewIcons.Reverse();

            return previewIcons;
        }

        public void ClearPreview()
        {
            // Reached from a plan that failed, and a plan can fail because the unit is gone.
            if (unit == null)
                return;

            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);
        }

        public void HandleActionPointsChanged(int newAmount)
        {
            actionsPointsBar.SetBlobAmount(newAmount);
        }

        public struct ConditionTestResult
        {
            public readonly bool IsValid;
            public readonly int FailedConditionIndex;

            public ConditionTestResult(bool isValid, int failedConditionIndex)
            {
                IsValid = isValid;
                FailedConditionIndex = failedConditionIndex;
            }
        }
    }
    
    public class ExecuteArgs
    {
        public readonly Tile TargetTile;
        public readonly Unit TargetUnit;

        public ExecuteArgs(Tile targetTile = null, Unit targetUnit = null)
        {
            TargetTile = targetTile;
            TargetUnit = targetUnit;
        }
        
        public Vector2Int TargetPosition
        {
            get
            {
                if (TargetTile != null)
                    return TargetTile.Position;

                if (TargetUnit != null)
                    return TargetUnit.CurrentState.Position.Position;

                throw new InvalidOperationException("Both TargetTile and TargetUnit are null.");
            }
        }
    }
}