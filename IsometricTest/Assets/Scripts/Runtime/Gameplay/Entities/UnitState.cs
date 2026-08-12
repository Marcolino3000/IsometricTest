using System;
using System.Collections.Generic;
using Actions;
using Runtime.Gameplay.Actions;
using Runtime.Gameplay.Traits;
using UnityEngine;
using UnityEngine.Serialization;

namespace Runtime.Gameplay.Entities
{
    /// <summary>
    /// A plain number a unit carries that an item can raise for the rest of the match. Only stats
    /// whose base is one authored number belong here - <see cref="Unit.SightRange"/>,
    /// <see cref="Unit.MaxActionPoints"/> and <see cref="Unit.MaxHealth"/> are the queries that fold
    /// the bonus in. Anything a trait already moves (damage, reach, move cost) stays with the traits,
    /// where <see cref="Global.CombatRules"/> and <see cref="Global.MovementRules"/> fold it.
    ///
    /// A further stat is an entry here, a base in the matching query and a branch in
    /// <see cref="Unit.GrantStatBonus"/> - and nowhere else.
    /// </summary>
    public enum UnitStat
    {
        SightRange,
        ActionPoints,
        Health
    }

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
                ActionPointsChangedCallback?.Invoke(actionPoints);

                hasActionsLeft =
                    actionPoints >= MoveAction.Condition.Cost || 
                    actionPoints >= AttackAction.Condition.Cost;
                if(!hasActionsLeft)
                    OnNoActionsLeft?.Invoke();
                    
            }
        }
        
        public Tile Position;
        public Team Team;
        public int SightRange;
        public MoveActionData MoveAction;
        public AttackActionData AttackAction;
        
        public List<UnitTrait> Traits = new();

        public bool HasActionsLeft => hasActionsLeft;

        [SerializeField] private int health;
        [SerializeField] private int actionPoints;
        [SerializeField] private bool hasActionsLeft;

        [Tooltip("Runtime only: what has been permanently added to each stat, indexed by UnitStat. " +
                 "A blueprint leaves this empty - the numbers it authors are the fields above.")]
        [SerializeField] private int[] statBonuses;

        private static readonly int StatCount = Enum.GetValues(typeof(UnitStat)).Length;

        private Action<int> HealthChangedCallback;
        private Action<int> ActionPointsChangedCallback;

        public void SetValueChangedCallbacks(Action<int> healthCallback, Action<int> actionPointsCallback)
        {
            HealthChangedCallback = healthCallback;
            ActionPointsChangedCallback = actionPointsCallback;
        }

        /// <summary>What has been permanently added to <paramref name="stat"/>, zero until something has.</summary>
        public int GetBonus(UnitStat stat)
        {
            var index = (int)stat;

            return statBonuses != null && index < statBonuses.Length ? statBonuses[index] : 0;
        }

        /// <summary>
        /// Raises a stat for good. Only the number: what it takes to make the raise felt - lighting
        /// the ground a wider sight uncovers, handing out the points - is <see cref="Unit"/>'s.
        /// </summary>
        public void AddBonus(UnitStat stat, int amount)
        {
            Normalize();
            statBonuses[(int)stat] += amount;
        }

        /// <summary>
        /// A copy of every bonus, for a history snapshot. Permanent stat gains are world state rather
        /// than loadout - they cost action points and cannot be undone by choosing differently - so
        /// unlike the weapon in hand they cannot be re-derived and have to travel with the snapshot.
        /// </summary>
        public int[] CaptureBonuses()
        {
            Normalize();

            return (int[])statBonuses.Clone();
        }

        public void RestoreBonuses(int[] bonuses)
        {
            Normalize();

            for (var i = 0; i < statBonuses.Length; i++)
                statBonuses[i] = bonuses != null && i < bonuses.Length ? bonuses[i] : 0;
        }

        /// <summary>What a stat is called to the player - what an item raising it says it does.</summary>
        public static string NameOf(UnitStat stat)
        {
            return stat switch
            {
                UnitStat.SightRange => "sight range",
                UnitStat.ActionPoints => "action points each turn",
                UnitStat.Health => "maximum health",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Grows the bonus array to one slot per stat. Needed because it is serialized: a blueprint
        /// saved before a stat existed - or before this field did - comes back short or null.
        /// </summary>
        private void Normalize()
        {
            if (statBonuses == null || statBonuses.Length != StatCount)
                Array.Resize(ref statBonuses, StatCount);
        }

        public UnitState(UnitState other)
        {
            MoveAction = other.MoveAction;
            // Before ActionPoints: its setter asks the attack what a strike costs.
            AttackAction = other.AttackAction;
            Health = other.Health;
            Position = null;
            ActionPoints = other.ActionPoints;
            Team = other.Team;
            SightRange = other.SightRange;
            // Copy into a fresh list so per-unit runtime state never aliases the blueprint's list.
            Traits = other.Traits != null ? new List<UnitTrait>(other.Traits) : new List<UnitTrait>();
            // Same reason: a bonus an item grants one unit must not be granted to every unit the
            // blueprint spawns after it.
            RestoreBonuses(other.statBonuses);
        }
    }

    
}