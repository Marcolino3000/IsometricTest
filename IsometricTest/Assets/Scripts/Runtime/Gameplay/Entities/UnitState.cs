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

                hasActionsLeft = CanAfford(MoveAction) || CanAfford(AttackAction);
                if(!hasActionsLeft)
                    OnNoActionsLeft?.Invoke();

            }
        }

        /// <summary>
        /// Whether the points in hand pay for <paramref name="action"/>. An action the blueprint
        /// never named is not affordable rather than a null reference: a half-authored unit is
        /// worth a warning, not an exception that takes the whole spawn down with it.
        /// </summary>
        private bool CanAfford<UCondition>(ActionData<UCondition> action) where UCondition : ActionCondition =>
            action != null && action.Condition != null && actionPoints >= action.Condition.Cost;

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

        /// <summary>A unit that belongs to no ring of the map, and so is confined by none.</summary>
        public const int NoZone = -1;

        /// <summary>
        /// Which ring of the map the unit was put on the board in - what
        /// <see cref="Global.MovementRules.CanEnter"/> confines it to while
        /// <see cref="Global.GameRules.ConfineOpponentsToSpawnZone"/> is on. Read off the tile it
        /// was actually placed on rather than off the roster entry that asked for it, since a ring
        /// walled off by mountains spills its units over its own border.
        ///
        /// Runtime only, and deliberately outside <see cref="History.GameSnapshot"/>: it is written
        /// once when the unit arrives and never moves again, so there is nothing for an undo to put
        /// back. <see cref="NoZone"/> for the player's character and for every unit on a map with no
        /// rings authored.
        /// </summary>
        [NonSerialized] public int HomeZone = NoZone;

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

        /// <summary>
        /// Puts a status on the unit. What an attack effect or an item hands over is the <b>asset</b>;
        /// what goes on the list is a copy of it with a countdown of its own - see
        /// <see cref="StatusTrait"/>.
        ///
        /// <b>It always renews, and deepens only if the status says it may.</b> Being afflicted again
        /// puts every copy carried back to full - so a stack runs out as one thing rather than
        /// fraying - and adds a further copy when <see cref="StatusTrait.Stackable"/> is set. The two
        /// halves do not interact: <i>how long</i> is the renewal, <i>whether it deepens</i> is the
        /// switch, and a status that does not stack behaves exactly as it did before any could.
        ///
        /// Deepening is copies rather than a counter for a reason: every rule query is already a
        /// fold over <see cref="Traits"/>, so a second copy doubles the bleed, the slow and anything
        /// a later status does <b>without a single subclass multiplying by anything</b>. What it
        /// costs instead is the badge row, which counts them rather than drawing each - see
        /// <c>UnitRules.GetCapabilities</c>.
        ///
        /// Two <i>different</i> statuses stack as any two traits do, whatever their limits say.
        /// </summary>
        public void ApplyStatus(StatusTrait status)
        {
            if (status == null)
                return;

            var source = status.Source;
            var carried = 0;

            foreach (var trait in Traits)
            {
                if (trait is not StatusTrait afflicted || afflicted.Source != source)
                    continue;

                afflicted.Refresh();
                carried++;
            }

            // Nothing carried means the first copy whatever the switch says; a further one only
            // where deepening is authored.
            if (carried == 0 || status.Stackable)
                Traits.Add(status.CreateInstance());

            LoadoutChanged?.Invoke();
        }

        /// <summary>
        /// Takes a status off - what a cure does. Named by the asset rather than the copy, since that
        /// is the only thing an item or an effect can author. Answers whether anything came off, so a
        /// cure that found nothing can say so.
        ///
        /// <b>Every copy of it</b>, however deep it was stacked: a remedy lifts the affliction, not
        /// one layer of it, and one that had to be drunk three times over would be a different item.
        /// </summary>
        public bool CureStatus(StatusTrait status)
        {
            if (status == null)
                return false;

            var source = status.Source;

            if (Traits.RemoveAll(trait => trait is StatusTrait afflicted && afflicted.Source == source) == 0)
                return false;

            LoadoutChanged?.Invoke();

            return true;
        }

        /// <summary>Takes every status off, whatever it is - what a full cure does.</summary>
        public bool CureAllStatuses()
        {
            if (Traits.RemoveAll(trait => trait is StatusTrait) == 0)
                return false;

            LoadoutChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Counts one of the unit's turns off every status it carries and drops the ones that have
        /// run out. Called once per turn by <c>StatusRunner</c>, after the turn's hooks have run - so
        /// a status authored to last one turn is felt on the turn it was put on.
        /// </summary>
        public void AgeStatuses()
        {
            var aged = false;
            var expired = false;

            foreach (var trait in Traits)
            {
                if (trait is not StatusTrait status)
                    continue;

                status.Age();
                aged = true;
                expired |= status.HasExpired;
            }

            if (expired)
                Traits.RemoveAll(trait => trait is StatusTrait status && status.HasExpired);

            // Said for a countdown that merely moved, not only for one that ran out: what a status
            // is worth is partly how long it has left, so a view holding that line - the HUD's trait
            // row captures it rather than re-reading it - would go on showing the turn before.
            if (aged)
                LoadoutChanged?.Invoke();
        }

        /// <summary>Whether the unit carries this status, named by the asset it was applied from.</summary>
        public bool HasStatus(StatusTrait status) => StacksOf(status) > 0;

        /// <summary>
        /// How many copies of a status the unit carries - what it is worth, since every copy is
        /// folded. Zero for one it does not have.
        /// </summary>
        public int StacksOf(StatusTrait status)
        {
            if (status == null)
                return 0;

            var source = status.Source;
            var stacks = 0;

            foreach (var trait in Traits)
                if (trait is StatusTrait afflicted && afflicted.Source == source)
                    stacks++;

            return stacks;
        }

        /// <summary>
        /// Every status carried and how long each has left, for a history snapshot. World state
        /// rather than loadout, exactly like <see cref="CaptureBonuses"/>: a status cost whoever
        /// applied it an action and cannot be shrugged off by choosing differently, so it can only
        /// come back by having been written down.
        ///
        /// The instances themselves are recorded, not the assets they came from - the same reason
        /// the snapshot holds units and lootboxes rather than ids. They are never destroyed.
        /// </summary>
        public StatusRecord[] CaptureStatuses()
        {
            var captured = new List<StatusRecord>();

            foreach (var trait in Traits)
                if (trait is StatusTrait status)
                    captured.Add(new StatusRecord(status, status.TurnsLeft));

            return captured.ToArray();
        }

        /// <summary>
        /// Puts the recorded statuses back. Only the statuses: what the blueprint grants and what a
        /// worn passive item put on the list are not recorded and must survive untouched - the
        /// passive's traits are re-derived from the restored inventory a moment later, and taking
        /// them off here would have them added twice.
        /// </summary>
        public void RestoreStatuses(StatusRecord[] records)
        {
            var removed = Traits.RemoveAll(trait => trait is StatusTrait);
            var restored = 0;

            if (records != null)
            {
                foreach (var record in records)
                {
                    if (record.Status == null)
                        continue;

                    record.Status.RestoreTurnsLeft(record.TurnsLeft);
                    Traits.Add(record.Status);
                    restored++;
                }
            }

            // Said only when the list actually moved: this runs on every unit for every undo, and a
            // unit that carried nothing and carries nothing has no badge row to rebuild.
            if (removed > 0 || restored > 0)
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
            // Not copied: a blueprint belongs to no ring, and where a unit spawns is decided when it
            // is placed. Set explicitly because a state read back off an asset never ran an
            // initializer.
            HomeZone = NoZone;
            // Copy into a fresh list so per-unit runtime state never aliases the blueprint's list.
            // A status authored straight onto a blueprint is copied one step further, into an
            // instance of its own: the asset carries no countdown, so shared it would run out on the
            // first turn of the first unit to be asked and vanish off all of them.
            Traits = new List<UnitTrait>();

            if (other.Traits != null)
                foreach (var trait in other.Traits)
                    Traits.Add(trait is StatusTrait status ? status.CreateInstance() : trait);
            // Same reason: a bonus an item grants one unit must not be granted to every unit the
            // blueprint spawns after it.
            RestoreBonuses(other.statBonuses);
        }
    }

    

    /// <summary>
    /// One status a unit carried at the moment a snapshot was taken, and how long it had left. The
    /// instance is held rather than the asset it came from: instances are per-unit and are never
    /// destroyed, so putting one back is putting back the very status that was taken off.
    /// </summary>
    public readonly struct StatusRecord
    {
        public readonly StatusTrait Status;
        public readonly int TurnsLeft;

        public StatusRecord(StatusTrait status, int turnsLeft)
        {
            Status = status;
            TurnsLeft = turnsLeft;
        }
    }
}
