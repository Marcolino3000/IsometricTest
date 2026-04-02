using System;
using Runtime.Actions;
using UnityEngine;

namespace Runtime.Entities
{
    [Serializable]
    public class UnitState
    { 
        public int Health
        {
            get => health;
            set
            {
                if (health == value)
                    return;

                health = value;
                HealthChangedCallback?.Invoke(health);
            }
        }
        
        public int ActionPoints
        {
            get => actionPoints;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ActionPoints), "ActionPoints cannot be negative.");

                hasActionsLeft = actionPoints >= MoveAction.Cost || actionPoints >= AttackAction.Cost;
                
                actionPoints = value;
            }
        }
        
        public Tile Position;
        public int Range;
        public Team Team;
        public Move MoveAction;
        public Attack AttackAction;
        public bool HasActionsLeft => hasActionsLeft; 

        [SerializeField] private int health;
        [SerializeField] private int actionPoints;
        [SerializeField] private bool hasActionsLeft;

        private Action<int> HealthChangedCallback;

        public void SetHealthChangedCallback(Action<int> callback)
        {
            HealthChangedCallback = callback;
        }

        public UnitState(UnitState other)
        {
            MoveAction = other.MoveAction;
            AttackAction = other.AttackAction;
            Health = other.Health;
            Position = null;
            ActionPoints = other.ActionPoints;
            Range = other.Range;
            Team = other.Team;
        }
    }

    
}