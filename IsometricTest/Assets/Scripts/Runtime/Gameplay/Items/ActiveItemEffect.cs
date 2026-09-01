using Actions;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// What using an active item does to one unit. This is where an item mechanic is authored: a new
    /// active item is a new effect asset plus an <see cref="ActiveItemData"/> holding it, never a new
    /// case in the item manager or the action executor.
    ///
    /// <b>Which units it is applied to is not this class's business</b> - it is the targeting on
    /// <see cref="ActionEffect"/>, and an effect that names none is applied to the character using
    /// the item, which is every item authored so far. One that draws an area around the user is
    /// applied to each unit in it, this method once per unit.
    /// </summary>
    [System.Serializable]
    public abstract class ActiveItemEffect : ActionEffect
    {
        /// <summary>
        /// One short line saying what a use does, in numbers ("Restores 4 health"). Abstract rather
        /// than defaulted: an effect is the authored part of an active item, so a new one has to say
        /// what it does the same moment it does it, or the item it fills would announce itself blank.
        /// </summary>
        public abstract string Summary { get; }

        public abstract void Apply(Unit target);
    }
}
