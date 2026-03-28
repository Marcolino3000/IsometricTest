using System;
using UnityEngine;

namespace Runtime
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

                actionPoints = value;
            }
        }
        
        public Tile Position;
        public int Range;
        public Team Team;

        private Action<int> HealthChangedCallback;
        [SerializeField] private int health;
        [SerializeField] private int actionPoints;
        
        public void SetHealthChangedCallback(Action<int> callback)
        {
            HealthChangedCallback = callback;
        }

        public UnitState(UnitState other)
        {
            Health = other.Health;
            Position = null;
            ActionPoints = other.ActionPoints;
            Range = other.Range;
            Team = other.Team;
        }
    }

    
}