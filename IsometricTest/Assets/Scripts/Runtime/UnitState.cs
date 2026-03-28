using System;
using UnityEngine;

namespace Runtime
{
    [Serializable]
    public class UnitState
    { public int Health
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
        public int ActionPoints;
        public Tile Position;
        public int Range;
        public Team Team;

        private Action<int> HealthChangedCallback;
        [SerializeField] private int health;
        
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