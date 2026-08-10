using Actions;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// What using an active item does to the character that used it. This is where an item mechanic
    /// is authored: a new active item is a new effect asset plus an <see cref="ActiveItemData"/>
    /// holding it, never a new case in the item manager or the action executor.
    /// </summary>
    public abstract class ActiveItemEffect : ActionEffect
    {
        public abstract void Apply(Unit user);
    }
}
