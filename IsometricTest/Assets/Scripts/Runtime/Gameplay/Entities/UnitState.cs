using System;
using System.Collections.Generic;
using Actions;
using Runtime.Core.State;
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

        /// <summary>
        /// Health moved, with what it was and why. Real events rather than the single injected
        /// callback this used to be: several things watch a unit's vitals now (its bar, its popups,
        /// and whatever presentation is added next), and a callback has room for one of them.
        ///
        /// Safe to hold on to for a unit's lifetime: <c>Unit.Init</c> assigns the state once and
        /// <c>Unit.RestoreSnapshot</c> writes into it rather than replacing it, so subscriptions
        /// survive undo. Only a respawn invalidates them.
        /// </summary>
        public event Action<ChangeEvent<int>> HealthChanged;

        /// <summary>Action points moved. Same contract as <see cref="HealthChanged"/>.</summary>
        public event Action<ChangeEvent<int>> ActionPointsChanged;

        /// <summary>
        /// What the unit is carrying changed - the weapon in hand or a worn trait. One event for
        /// both because everything reading it (the badge row, the unit card) reads the pair.
        /// </summary>
        public event Action LoadoutChanged;

        /// <summary>The unit stands somewhere else. Raised for a step and for a restore alike.</summary>
        public event Action<ChangeEvent<Tile>> PositionChanged;

        public int Health
        {
            get => health;
            set
            {
                if (health == value)
                    return;

                var previous = health;
                health = value;
                HealthChanged?.Invoke(new ChangeEvent<int>(previous, health, reason));
            }
        }

        public int ActionPoints
        {
            get => actionPoints;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ActionPoints), "ActionPoints cannot be negative.");

                var previous = actionPoints;
                actionPoints = value;
                ActionPointsChanged?.Invoke(new ChangeEvent<int>(previous, actionPoints, reason));

                hasActionsLeft =
                    actionPoints >= MoveAction.Condition.Cost ||
                    actionPoints >= AttackAction.Condition.Cost;
                if(!hasActionsLeft)
                    OnNoActionsLeft?.Invoke();

            }
        }

        public Tile Position
        {
            get => position;
            set
            {
                if (position == value)
                    return;

                var previous = position;
                position = value;
                PositionChanged?.Invoke(new ChangeEvent<Tile>(previous, position, reason));
            }
        }

        /// <summary>
        /// The weapon in hand. Equipping is writing here - there is no separate "equipped" field,
        /// since the weapon and the attack are the same asset.
        /// </summary>
        public AttackActionData AttackAction
        {
            get => attackAction;
            set
            {
                if (attackAction == value)
                    return;

                attackAction = value;
                LoadoutChanged?.Invoke();
            }
        }

        public Team Team;
        public int SightRange;
        public MoveActionData MoveAction;

        public List<UnitTrait> Traits = new();

        public bool HasActionsLeft => hasActionsLeft;

        [SerializeField] private int health;
        [SerializeField] private int actionPoints;
        [SerializeField] private bool hasActionsLeft;
        [SerializeField] private Tile position;

        // Renamed from the public field it used to be, so every blueprint that authored "AttackAction"
        // keeps its weapon. Do not drop the attribute until every UnitBlueprint has been re-saved.
        [FormerlySerializedAs("AttackAction")]
        [SerializeField] private AttackActionData attackAction;

        // Why the values written from here on are moving. Ambient rather than a parameter on every
        // setter: a restore writes half a dozen of them in a row, and the alternative is passing the
        // reason through every call that leads to one. Set with Changing(), never assigned directly.
        [NonSerialized] private ChangeReason reason = ChangeReason.Gameplay;

        [Tooltip("Runtime only: what has been permanently added to each stat, indexed by UnitStat. " +
                 "A blueprint leaves this empty - the numbers it authors are the fields above.")]
        [SerializeField] private int[] statBonuses;

        private static readonly int StatCount = Enum.GetValues(typeof(UnitStat)).Length;

        /// <summary>
        /// Marks everything written until the returned scope is disposed as happening for the given
        /// reason, so the events raised carry it. Nests: the previous reason is put back rather than
        /// reset to <see cref="ChangeReason.Gameplay"/>.
        ///
        /// <code>using (state.Changing(ChangeReason.Restore)) { ... }</code>
        /// </summary>
        public ReasonScope Changing(ChangeReason newReason) => new(this, newReason);

        public readonly struct ReasonScope : IDisposable
        {
            private readonly UnitState state;
            private readonly ChangeReason previous;

            public ReasonScope(UnitState state, ChangeReason reason)
            {
                this.state = state;
                previous = state.reason;
                state.reason = reason;
            }

            public void Dispose()
            {
                state.reason = previous;
            }
        }

        /// <summary>
        /// Puts a trait on the unit - what wearing a passive item amounts to. A method rather than
        /// writing <see cref="Traits"/> directly so the row of badges hears about it; duplicates are
        /// kept on purpose, since one instance is removed per item taken off and a trait the
        /// blueprint also grants has to survive that.
        /// </summary>
        public void AddTrait(UnitTrait trait)
        {
            if (trait == null)
                return;

            Traits.Add(trait);
            LoadoutChanged?.Invoke();
        }

        /// <summary>Takes one instance of a trait off. See <see cref="AddTrait"/>.</summary>
        public void RemoveTrait(UnitTrait trait)
        {
            if (trait == null || !Traits.Remove(trait))
                return;

            LoadoutChanged?.Invoke();
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
            // Nothing is subscribed to a state being built, but the reason is what it is: this is a
            // blueprint being copied, not a turn being played.
            using var _ = Changing(ChangeReason.Setup);

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