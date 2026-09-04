using System;
using UnityEngine;

namespace Runtime.Gameplay.Traits
{
    /// <summary>
    /// A <see cref="UnitTrait"/> that is <i>put on</i> a unit rather than carried by it, and wears
    /// off again - a bleed, a crippled leg. Everything it does it does through the hooks every trait
    /// has: <see cref="Trait.ModifyMoveCost"/> slows a step, <see cref="Trait.OnTurnBegan"/> takes
    /// health each turn. This class adds one thing and one thing only - <b>a countdown</b>.
    ///
    /// <b>It is applied as a copy, never as the asset.</b> A trait asset is shared by every unit
    /// whose blueprint, weapon or ground names it, so a counter written on it would be one counter
    /// for the whole board. <see cref="CreateInstance"/> makes a runtime copy the way
    /// <c>MergeRules.Combine</c> copies a weapon - <see cref="HideFlags.HideAndDontSave"/>, so it
    /// never reaches the project files - and that copy is what goes on
    /// <c>UnitState.Traits</c>. Copies are never destroyed: a history snapshot behind the cursor
    /// still refers to them.
    ///
    /// Being an ordinary entry in that one list is the whole point: <c>CombatRules.TraitsAffecting</c>
    /// folds it into damage, reach, sight and move cost, the badge row draws it and the unit's card
    /// prints it, all without knowing statuses exist.
    ///
    /// <b>A further status is a further subclass with an authored number</b> - never a case anywhere
    /// that applies one.
    /// </summary>
    public abstract class StatusTrait : UnitTrait
    {
        [Tooltip("How many of the carrier's own turns it lasts, counted down at the start of each. " +
                 "Zero never wears off - what a curse wants, and what has to be cured to be lifted.")]
        [Min(0)] public int Duration = 3;

        [Tooltip("Whether inflicting it again deepens it as well as renewing it. Off, one unit " +
                 "carries one copy however often it is inflicted. On, each infliction adds a copy " +
                 "and every copy is folded - a wound stacked three deep bleeds three times - with " +
                 "no ceiling, so a status that can be inflicted freely is worth leaving off.")]
        public bool Stackable;

        // Runtime state, and only ever on a copy (see CreateInstance). Not serialized: the asset has
        // no countdown to author, and a copy is made from the asset rather than loaded from disk.
        [NonSerialized] private int turnsLeft;

        // Which asset this copy was made from. What tells two applications of the same status apart
        // from two different statuses - the copies are distinct objects and their names are equal,
        // so neither could answer it. Null on the asset itself.
        [NonSerialized] private StatusTrait source;

        /// <summary>Turns still to run. On the asset itself, always zero - it is not on anybody.</summary>
        public int TurnsLeft => turnsLeft;

        /// <summary>A status authored to outlast the match: it is lifted by being cured, or not at all.</summary>
        public bool IsPermanent => Duration <= 0;


        /// <summary>Whether it has run out and should come off. A permanent one never has.</summary>
        public bool HasExpired => !IsPermanent && turnsLeft <= 0;

        /// <summary>
        /// The asset this instance came from, or the asset itself when asked of one. What
        /// <c>UnitState</c> compares to decide whether a unit already carries this status, and what a
        /// cure names.
        /// </summary>
        public StatusTrait Source => source != null ? source : this;

        /// <summary>
        /// A copy of this status with a countdown of its own, to be put on one unit. See the note on
        /// the class for why the asset itself is never applied.
        /// </summary>
        public StatusTrait CreateInstance()
        {
            var instance = Instantiate(this);

            // Instantiate names the copy "<asset> (Clone)", which would show on the badge and on the
            // unit's card.
            instance.name = name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.source = Source;
            instance.turnsLeft = Duration;

            return instance;
        }

        /// <summary>
        /// A copy shows the symbol of the asset it was made from rather than cutting one of its own -
        /// otherwise every affliction on every unit would cut a sprite.
        /// </summary>
        protected override Trait IconOwner => Source;

        /// <summary>Put on rather than carried, and the card says which.</summary>
        protected override string KindName => "Status";

        /// <summary>Puts the countdown back to full - what applying a status somebody already has does.</summary>
        public void Refresh()
        {
            turnsLeft = Duration;
        }

        /// <summary>
        /// Counts one of the carrier's turns off. Called after the turn's hooks have run, so a status
        /// authored to last one turn is felt on the turn it was applied for.
        /// </summary>
        public void Age()
        {
            if (!IsPermanent)
                turnsLeft--;
        }

        /// <summary>
        /// Puts the countdown back to a recorded value - what undo and redo do. Beside
        /// <c>UnitState.RestoreBonuses</c> and for the same reason: a countdown is world state that
        /// cost somebody an action, so it can only come back by having been written down.
        /// </summary>
        public void RestoreTurnsLeft(int value)
        {
            turnsLeft = value;
        }

        /// <summary>
        /// What the status does, and then how long it has left. The duration is part of the line
        /// because it is half of what a status is worth - the same number the weapon applying it
        /// promises. Asked of the asset it reads what it would last; asked of an instance, what is
        /// left of it.
        /// </summary>
        public override string Summary
        {
            get
            {
                if (IsPermanent)
                    return StatusSummary;

                var turns = turnsLeft > 0 ? turnsLeft : Duration;
                var stacking = Stackable ? ", stacks" : string.Empty;

                return $"{StatusSummary}, {turns} turn{(turns == 1 ? string.Empty : "s")}{stacking}";
            }
        }

        /// <summary>
        /// What it does, in the stat-then-number shape every trait summary takes - the duration is
        /// added around it. Abstract for the same reason <c>ActiveItemEffect.Summary</c> is: a status
        /// nobody can read is one nobody can play around.
        /// </summary>
        protected abstract string StatusSummary { get; }
    }
}
