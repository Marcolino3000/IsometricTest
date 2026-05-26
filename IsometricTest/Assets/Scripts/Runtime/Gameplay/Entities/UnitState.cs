using System;
using Actions;
using Runtime.Gameplay.Actions;
using UnityEngine;

namespace Runtime.Gameplay.Entities
{
    [Serializable]
    public class UnitState
    {
        public event Action OnNoActionsLeft; 
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

                actionPoints = value;
                
                hasActionsLeft = 
                    actionPoints >= MoveAction.Condition.Cost || 
                    actionPoints >= AttackAction.Condition.Cost;
                if(!hasActionsLeft)
                    OnNoActionsLeft?.Invoke();
                    
            }
        }
        
        public Tile Position;
        public int Range;
        public Team Team;
        public MoveActionData MoveAction;
        public AttackActionData AttackAction;
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