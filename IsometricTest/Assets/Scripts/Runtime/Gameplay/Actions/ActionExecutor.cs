using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Runtime.Core.Spawning;
using Runtime.Gameplay.Entities;
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

        private Unit unit;
        private List<IUnitAction> plannedActions = new();

        public ConditionTestResult PlanMoveAction(ExecuteArgs executeArgs)
        {
            var path = tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetTile);
            
            PlanMoveActionsFromPath(path);
            var result = TestConditionsForPlannedActions();
            
            SetActionsPointsBar();
            unit.TileHighlighter.HighlightTilesAlongPath(path, result.FailedConditionIndex);

            return result;
        }

        public ConditionTestResult PlanAttackAction(ExecuteArgs executeArgs)
        {
            var targetTile = executeArgs.TargetUnit.CurrentState.Position;
            var range = unit.CurrentState.AttackAction.Condition.Range;

            // Only move close enough that the target lands within attack range,
            // so ranged units stop short instead of walking right up to it.
            var pathIntoRange = tileSpawner.GetPathWithinRange(unit.CurrentState.Position, targetTile, range);

            var remainingAP = PlanMoveActionsFromPath(pathIntoRange);

            var attackFromTile = pathIntoRange.LastOrDefault() ?? unit.CurrentState.Position;

            var context = new ActionContext()
            {
                Unit = unit,
                TargetUnit = executeArgs.TargetUnit,
                ActionPoints = remainingAP,
                TargetTile = targetTile,
                Distance = tileSpawner.GetDistanceBetweenTiles(attackFromTile, targetTile)
            };

            plannedActions.Add(unit.CurrentState.AttackAction.CreateAction(context));

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

                // Debug.Log("Action points: " + context.ActionPoints + " Distance: " + context.Distance + " Target tile: " + context.TargetTile);

                plannedActions.Add(unit.CurrentState.MoveAction.CreateAction(context));

                availableActionPoints -= unit.CurrentState.MoveAction.Condition.Cost;
            }

            return availableActionPoints;
        }


        public void ExecuteMoveActions(ExecuteArgs executeArgs)
        {
            if(!PlanMoveAction(executeArgs).IsValid)
                return;
            
            ExecutePlannedActions();
        }
        
        public void ExecuteAttackAction(ExecuteArgs executeArgs)
        {
            if(!PlanAttackAction(executeArgs).IsValid)
                return;

            ExecutePlannedActions();
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


        public void Setup(Unit unit, TileSpawner tileSpawner)
        {
            this.unit = unit;
            this.tileSpawner = tileSpawner;
            actionsPointsBar.Setup(unit.CurrentState.ActionPoints); //todo: add max action  points to blueprint
        }

        private int PlannedCost => plannedActions.Sum(action => action.Cost);
        
        private void SetActionsPointsBar()
        {
            var committed = unit.CurrentState.ActionPoints;
            var previewCost = Mathf.Min(PlannedCost, committed);
            actionsPointsBar.SetBlobAmount(committed - previewCost, previewCost);
        }

        public void ClearPreview()
        {
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