using System;
using System.Collections.Generic;
using System.Linq;
using UI;
using UnityEngine;

namespace Runtime.Actions
{
    public class  ActionPointCounter : MonoBehaviour
    {
        public event Action<int> ActionPointCountChanged;
        
        [Header("Debug")]
        [SerializeField] private UnitState unitState;
        
        [SerializeField] private ActionsPointsBar actionsPointsBar;
        private List<UnitAction> plannedActions = new();
        
        public bool PlanActions(List<UnitAction> actions, UnitState unitState)
        {
            plannedActions = actions;
            
            var totalCost = actions.Sum(action => action.Cost);

            return totalCost <= unitState.ActionPoints;
        }

        public void ExecuteActions()
        {
            
        }

        public void Setup(UnitState unitStateArg)
        {
            unitState = unitStateArg;
            actionsPointsBar.Setup(unitStateArg.ActionPoints); //todo: add max action  points to blueprint
        }
    }
}