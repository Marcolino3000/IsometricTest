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

        MoveExecutor _moveMoveExecutor;
        AttackExecutor _attackMoveExecutor;
        
        // private Func<Tile, bool> moveActionTest;
        // private Func<Tile, bool> moveAction;
        // private Func<Tile, bool> attackActionTest;
        // private Func<Tile, bool> attackAction;
        

        public bool PlanActions(List<UnitAction> actions, ExecuteArgs executeArgs)
        {
            plannedActions = actions;

            var totalCost = GetTotalCost(actions);
            
            if (totalCost > unit.CurrentState.ActionPoints)
                return false;

            if (!_moveMoveExecutor.CheckActionValidity(actions, executeArgs.TargetTile))
                return false;
            
            if(!_attackMoveExecutor.CheckActionValidity(actions, executeArgs.TargetUnit))
                return false;

            actionsPointsBar.SetBlobAmount(totalCost);
            
            return true;
        }

        // private bool CheckActionValidity(List<UnitAction> actions, Tile target)
        // {
        //     foreach (var action in actions)
        //     {
        //         if (action is Move)
        //         {
        //             if(!moveActionTest.Invoke(target))
        //                 return false;
        //         }
        //     }
        //
        //     return true;
        // }

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
                // moveAction.Invoke(args.Target);
                _moveMoveExecutor.Execute(args.TargetTile);
            }

            unit.CurrentState.ActionPoints -= totalCost;
            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);
        }
        

        public void Setup(Unit unit, 
            Func<Tile, bool> moveActionTestArg, Func<Tile, bool> moveActionArg,
            Func<Unit, bool> attackActionTestArg, Func<Unit, bool> attackActionArg)
        {
            this.unit = unit;
            
            _moveMoveExecutor = new MoveExecutor(moveActionTestArg, moveActionArg);
            _attackMoveExecutor = new AttackExecutor(attackActionTestArg, attackActionArg);
            
            // moveActionTest = moveActionTestArg;
            // moveAction = moveActionArg;
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