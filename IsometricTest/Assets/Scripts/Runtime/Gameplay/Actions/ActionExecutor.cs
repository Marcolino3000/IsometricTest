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

        public bool PlanActions(ExecuteArgs executeArgs)
        {
            if (executeArgs.TargetTile == null)
            {
                Debug.Log("tile was null");
                return false;
            }

            Debug.Log("tile was not null");
            
            PlanActionsFromPath(tileSpawner.GetPath(unit.CurrentState.Position, executeArgs.TargetTile));
            
            if (!TestConditionsForPlannedActions())
                return false;

            return true;

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

            var context = new ActionContext()
            {
                ActionPoints = unit.CurrentState.ActionPoints,
            };

            foreach (var tile in path)
            {
                if(tile == path.First())
                    continue;

                context = new ActionContext()
                {
                    TargetUnit = unit,
                    ActionPoints = context.ActionPoints - moveActionData.Condition.Cost,
                    TargetTile = tile
                };
                
                Debug.Log("Action points: " + context.ActionPoints + " Distance: " + context.Distance + " Target tile: " + context.TargetTile);

                plannedActions.Add(moveActionData.CreateAction(context));
            }
        }
        

        public bool ExecuteActions(ExecuteArgs executeArgs)
        {
            if(!PlanActions(executeArgs))
                return false;
            
            int totalCost = 0;

            foreach (var action in plannedActions)
            {
                // totalCost += action.;
                action.ExecuteEffects();
            }

            unit.CurrentState.ActionPoints -= totalCost;
            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);

            return true;
        }
        

        public void Setup(Unit unit, TileSpawner tileSpawner)
        {
            this.unit = unit;
            this.tileSpawner = tileSpawner;
            actionsPointsBar.Setup(unit.CurrentState.ActionPoints); //todo: add max action  points to blueprint
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