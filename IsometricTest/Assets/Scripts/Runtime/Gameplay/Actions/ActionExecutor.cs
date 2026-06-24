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
        [SerializeField] private AttackActionData attackActionData;
        [SerializeField] private MoveActionData moveActionData;

        [Header("References")]
        [SerializeField] private ActionsPointsBar actionsPointsBar;
        [SerializeField] private TileSpawner tileSpawner;

        private Unit unit;
        private List<IUnitAction> plannedActions = new();

        public bool PlanMoveAction(ExecuteArgs executeArgs)
        {
            PlanMoveActionsFromPath(tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetTile));
            
            PreviewPlannedActions();
            return TestConditionsForPlannedActions();
        }

        public bool PlanAttackAction(ExecuteArgs executeArgs)
        {
            var remainingAP = PlanMoveActionsFromPath(tileSpawner.GetPath(
                unit.CurrentState.Position, executeArgs.TargetUnit.CurrentState.Position,
                ignoreGoalOccupied: true, excludeGoal: true));

            var context = new ActionContext()
            {
                TargetUnit = executeArgs.TargetUnit,
                ActionPoints = remainingAP,
                TargetTile = executeArgs.TargetUnit.CurrentState.Position
            };
            
            plannedActions.Add(attackActionData.CreateAction(context));
            
            PreviewPlannedActions();
            return TestConditionsForPlannedActions();
        }

        private bool TestConditionsForPlannedActions()
        {
            foreach (var action in plannedActions)
            {
                if (!action.TestConditions())
                {
                    Debug.LogWarning("planned action is not valid.");
                    return false;
                }
            }

            return true;
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

                plannedActions.Add(moveActionData.CreateAction(context));

                availableActionPoints -= moveActionData.Condition.Cost;
            }

            return availableActionPoints;
        }


        public void ExecuteMoveActions(ExecuteArgs executeArgs)
        {
            if(!PlanMoveAction(executeArgs))
                return;
            
            ExecutePlannedActions();
        }
        
        public void ExecuteAttackAction(ExecuteArgs executeArgs)
        {
            if(!PlanAttackAction(executeArgs))
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
        
        private void PreviewPlannedActions()
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