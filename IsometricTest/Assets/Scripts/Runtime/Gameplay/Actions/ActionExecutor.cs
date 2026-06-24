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
            PlanActionsFromPath(tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetTile));
            
            PreviewPlannedActions();
            return TestConditionsForPlannedActions();
        }

        public void PlanAttackAction(ExecuteArgs executeArgs)
        {
            PlanActionsFromPath(tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetUnit.CurrentState.Position));
            
            PreviewPlannedActions();
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

        private void PlanActionsFromPath(List<Tile> path)
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

                Debug.Log("Action points: " + context.ActionPoints + " Distance: " + context.Distance + " Target tile: " + context.TargetTile);

                plannedActions.Add(moveActionData.CreateAction(context));

                availableActionPoints -= moveActionData.Condition.Cost;
            }
        }


        public bool ExecuteActions(ExecuteArgs executeArgs)
        {
            if(!PlanMoveAction(executeArgs))
                return false;
            
            int totalCost = 0;

            foreach (var action in plannedActions)
            {
                totalCost += action.Cost;
                action.ExecuteEffects();
            }

            unit.CurrentState.ActionPoints -= totalCost;

            return true;
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