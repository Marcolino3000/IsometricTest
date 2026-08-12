using System.Collections.Generic;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// What a strike has to say to the *player*, next to what <see cref="CombatLog"/> has to say to
    /// whoever is debugging it. A trait that moved the number in a way worth seeing - a crit - adds a
    /// word while the damage is folded, and the popup over the unit that was hit shows it beside the
    /// number.
    ///
    /// A channel rather than a field on <see cref="Traits.CombatContext"/> because the context is
    /// immutable and a trait hands back nothing but an int: this is the one way a trait can say
    /// anything about the strike it just changed, so a further one that wants to be seen needs no new
    /// plumbing. It is <em>collected</em> by whoever resolved the strike rather than pushed anywhere,
    /// which is what keeps it out of a caller that only asks what a hit would cost.
    /// </summary>
    public static class StrikeNotes
    {
        private static readonly List<string> Notes = new();

        /// <summary>Opened by <see cref="CombatRules.CalculateDamage"/> before the traits fold.</summary>
        public static void Begin()
        {
            Notes.Clear();
        }

        /// <summary>
        /// Said by a trait while it is being asked. A blank note is ignored, so a trait whose label
        /// has been cleared on the asset simply shows nothing.
        /// </summary>
        public static void Add(string note)
        {
            if (!string.IsNullOrWhiteSpace(note))
                Notes.Add(note);
        }

        /// <summary>
        /// Everything said about the strike just resolved, as one label - null when nothing was said.
        /// Empties the channel: a note is shown once, by the popup for the damage it explains.
        /// </summary>
        public static string Collect()
        {
            if (Notes.Count == 0)
                return null;

            var collected = string.Join(" ", Notes);
            Notes.Clear();

            return collected;
        }
    }
}
