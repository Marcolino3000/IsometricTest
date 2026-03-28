using System;
using System.Collections.Generic;
using System.Linq;
using UI;
using UnityEngine;

namespace Runtime.Actions
{
    public class  ActionExecutor : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private Unit unit;
        [SerializeField] private List<UnitAction> plannedActions = new();

        [Header("Settings")]
        [SerializeField] private Move moveActionData;
        
        [Header("References")]
        [SerializeField] private ActionsPointsBar actionsPointsBar;

        private Func<Tile, bool> moveActionTest;
        private Func<Tile, bool> moveAction;
        

        public bool PlanActions(List<UnitAction> actions, Tile target)
        {
            plannedActions = actions;

            var totalCost = GetTotalCost(actions);
            
            if (totalCost > unit.CurrentState.ActionPoints)
                return false;

            if (!CheckActionValidity(actions, target))
                return false;

            actionsPointsBar.SetBlobAmount(totalCost);
            
            return true;
        }

        private bool CheckActionValidity(List<UnitAction> actions, Tile target)
        {
            foreach (var action in actions)
            {
                if (action is Move)
                {
                    if(!moveActionTest.Invoke(target))
                        return false;
                }
            }

            return true;
        }

        private int GetTotalCost(List<UnitAction> actions)
        {
            var totalCost = actions.Sum(action => action.Cost);
            return totalCost;
        }

        public void ExecuteActions(ExecuteArgs args)
        {
            int totalCost = 0;

            foreach (var action in plannedActions)
            {
                totalCost += action.Cost;
                moveAction.Invoke(args.Target);
            }

            unit.CurrentState.ActionPoints -= totalCost;
            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);
        }
        
        
        // private bool CheckForMovement(Tile tile)
        // {
        //     if (selectedUnit != null)
        //     {
        //         if (selectedUnit.TryMoveToTile(tile))
        //         {
        //             selectedUnit = null;
        //             return true;
        //         }
        //     }
        //     
        //     return false;
        // }

        public void Setup(Unit unit, Func<Tile, bool> moveActionTestArg, Func<Tile, bool> moveActionArg)
        {
            this.unit = unit;
            moveActionTest = moveActionTestArg;
            moveAction = moveActionArg;
            actionsPointsBar.Setup(unit.CurrentState.ActionPoints); //todo: add max action  points to blueprint
        }
    }
    
    public class ExecuteArgs
    {
        public ExecuteArgs(Tile target)
        {
            Target = target;
        }

        public readonly Tile Target;
    }
}