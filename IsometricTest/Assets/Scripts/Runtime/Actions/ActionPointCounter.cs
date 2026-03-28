using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Runtime.Actions
{
    public class ActionPointCounter : MonoBehaviour
    {
        private List<Action> plannedActions = new();
        
        public bool PlanActions(List<Action> actions, UnitState unitState)
        {
            plannedActions = actions;
            
            var totalCost = actions.Sum(action => action.Cost);

            return totalCost <= unitState.ActionPoints;
        }

        public void ExecuteActions()
        {
            
        }
    }
}