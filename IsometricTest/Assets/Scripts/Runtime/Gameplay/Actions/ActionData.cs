using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Items;
using UnityEngine;
using ActionContext = Runtime.Gameplay.Actions.ActionContext;

namespace Actions
{
    /// <summary>
    /// An authored action. Derives from <see cref="Item"/> because the actions the player chooses
    /// between - the weapon it swings, the potion it drinks - are exactly the things it carries;
    /// one that is not carried (the move action) simply keeps <see cref="SlotKind.None"/> and is
    /// never offered in a slot.
    ///
    /// The condition is authored <b>inline</b> on this asset rather than as a further asset beside
    /// it. It is a one-off value - a cost of 1, a range of 3 - and an asset each meant three files
    /// per weapon and a folder that had to be kept in step by hand. Things genuinely referenced from
    /// several places stay ScriptableObjects: <see cref="Traits.Trait"/>, because terrain, weapons
    /// and passives all point at the same one, and this asset itself, because it is an
    /// <see cref="Item"/>.
    ///
    /// The cost of inlining is that a renamed or moved condition or effect class loses its data, so
    /// any such class needs <c>[MovedFrom]</c> - and that one cannot be shared by two actions.
    ///
    /// <b>What an action's effects are is the subclass's business, not this base's.</b> Both kinds
    /// that carry any hold their own list - <see cref="AttackActionData"/>'s folded into one number
    /// by <c>CombatRules.BaseDamageOf</c>, <see cref="ActiveItemData"/>'s run in order - so there is
    /// no effect field here. An action that does nothing authored (the move action) declares none.
    /// </summary>
    public abstract class ActionData<UCondition> : Item
        where UCondition : ActionCondition
    {
        /// <summary>What the action takes. Authored inline; see the note on the class.</summary>
        public UCondition Condition => condition;

        [SerializeReference] private UCondition condition;

        public abstract IUnitAction CreateAction(ActionContext context);
    }

    /// <summary>
    /// What an action takes. A plain serializable class, authored inline on the action that uses it.
    /// A subclass added later needs <c>[MovedFrom]</c> if it is ever renamed or moved namespace,
    /// since SerializeReference stores the type name.
    /// </summary>
    [Serializable]
    public abstract class ActionCondition
    {
        public int Cost;
    }

    /// <summary>
    /// What an action does. Authored inline; see <see cref="ActionCondition"/>.
    ///
    /// <b>An effect carries its own targeting and its own conditions.</b> They belong here rather
    /// than on the action because one action's effects need not agree: a swing that hits its target
    /// and also spills onto the hurt units around it is two effects on one weapon, differing in
    /// exactly those two fields. Both are left empty by every effect authored so far, which is the
    /// plain case - the effect applies to what the action was aimed at, unconditionally.
    /// </summary>
    [Serializable]
    public abstract class ActionEffect
    {
        [Tooltip("Who this effect reaches. Left empty it applies to whatever the action was aimed " +
                 "at - the unit being struck, the character using the item.")]
        [SerializeReference] public TargetSelector Targets;

        [Tooltip("Tests every unit it reaches has to pass, all of them. Asked per unit, so this " +
                 "picks which of them are affected rather than whether the effect happens at all.")]
        [SerializeReference] public List<TargetCondition> Requires = new();

        /// <summary>Whether this effect names an area rather than the action's own target.</summary>
        public bool HasOwnTargets => Targets != null;

        /// <summary>
        /// Every unit this effect applies to: what its selector reaches, or the action's own target
        /// when it names none, minus whatever its conditions turn away. Never yields a unit that is
        /// out of play.
        /// </summary>
        public IEnumerable<Unit> ResolveTargets(EffectContext context)
        {
            var candidates = Targets != null
                ? Targets.Resolve(context)
                : OnlyTheTarget(context);

            foreach (var candidate in candidates)
                if (candidate != null && candidate.IsAlive && Passes(candidate, context))
                    yield return candidate;
        }

        /// <summary>Whether one unit clears every condition on this effect.</summary>
        public bool Passes(Unit candidate, EffectContext context)
        {
            if (Requires == null)
                return true;

            foreach (var condition in Requires)
                if (condition != null && !condition.Holds(candidate, context))
                    return false;

            return true;
        }

        /// <summary>
        /// How this effect's targeting reads on a card - empty for the plain case, so an effect that
        /// aims at nothing in particular says nothing in particular.
        /// </summary>
        public string TargetSummary
        {
            get
            {
                if (Targets == null)
                    return string.Empty;

                var line = $" to {Targets.Summary}";

                var tests = Requires?
                    .Where(condition => condition != null)
                    .Select(condition => condition.Summary)
                    .ToList();

                if (tests != null && tests.Count > 0)
                    line += $" ({string.Join(", ", tests)})";

                return line;
            }
        }

        private static IEnumerable<Unit> OnlyTheTarget(EffectContext context)
        {
            if (context.Target != null)
                yield return context.Target;
        }
    }
}
