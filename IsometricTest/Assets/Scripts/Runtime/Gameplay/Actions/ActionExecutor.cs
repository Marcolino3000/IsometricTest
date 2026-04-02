using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using UI;
using UnityEngine;

namespace Runtime.Gameplay.Actions
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

        public bool PlanActionsNew(ExecuteArgs executeArgs)
        {
            var actions = CreateActions(executeArgs);
            
            var totalCost = GetTotalCost(actions);
            
            if (totalCost > unit.CurrentState.ActionPoints)
                return false;

            if (!_moveMoveExecutor.CheckActionValidity(actions, executeArgs.TargetTile))
                return false;
            
            if(!_attackMoveExecutor.CheckActionValidity(actions, executeArgs.TargetUnit))
                return false;
            
            plannedActions = actions;

            actionsPointsBar.SetBlobAmount(totalCost);
            
            return true;
            
        }

        private List<UnitAction> CreateActions(ExecuteArgs executeArgs)
        {
            var actions = new List<UnitAction>();
            
            if(executeArgs.TargetTile != null)
                actions.Add(unit.CurrentState.MoveAction);
            
            else if(executeArgs.TargetUnit != null)
                actions.Add(unit.CurrentState.AttackAction);

            else
                Debug.LogError("either both or none execute args were null");
            
            return actions;
        }

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

        private int GetTotalCost(List<UnitAction> actions)
        {
            var totalCost = actions.Sum(action => action.Cost);
            return totalCost;
        }

        public bool ExecuteActions(ExecuteArgs executeArgs)
        {
            if(!PlanActionsNew(executeArgs))
                return false;
            
            int totalCost = 0;

            foreach (var action in plannedActions)
            {
                totalCost += action.Cost;
                if(executeArgs.TargetTile != null)
                    _moveMoveExecutor.Execute(executeArgs.TargetTile);
                else if(executeArgs.TargetUnit != null)
                    _attackMoveExecutor.Execute(executeArgs.TargetUnit);
                else
                    Debug.LogError("either both or none execute args were null");
            }

            unit.CurrentState.ActionPoints -= totalCost;
            actionsPointsBar.SetBlobAmount(unit.CurrentState.ActionPoints);

            return true;
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