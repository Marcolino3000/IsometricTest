using System.Text;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Debug trace of how an attack's damage came about: the weapon it started from, every trait that
    /// was consulted with the number before and after it had its say, the retaliation verdict and who
    /// died. Written to the Unity console as one message per attack, so a strike and the counter-strike
    /// it provokes read as a single block instead of arriving interleaved with everything else.
    ///
    /// Kept deliberately terse: who hits whom is stated once in the header line and never restated -
    /// a retaliation is the same pair with the roles swapped, the damage line's health numbers belong
    /// to whoever that strike is against, and traits that changed nothing are named together on one
    /// line instead of getting one each.
    ///
    /// Two switches, both on the live <see cref="GameRules"/> asset <see cref="CombatRules"/> is given,
    /// so either can be flipped mid-play:
    ///
    /// <list type="bullet">
    /// <item><see cref="GameRules.LogCombatCalculations"/> is the log itself, and what it writes is
    /// only damage and what moved it: the base damage, every trait that changed the number, the total,
    /// the health left, why nobody struck back, who fell.</item>
    /// <item><see cref="GameRules.LogCombatDetails"/> adds everything around those numbers - the tiles
    /// and the distance, what a trait rolled, the traits that were asked and changed nothing. That is
    /// the half one wants while hunting a specific interaction and not while watching a fight.</item>
    /// </list>
    ///
    /// Whichever is off costs nothing: every entry point returns before it formats anything, which is
    /// why the callers pass values rather than pre-built messages - and why a caller that has to build
    /// its own message (see <see cref="Detail"/>) asks <see cref="Details"/> first.
    ///
    /// Traits that want to explain themselves (a crit roll, a requirement that was not met) call
    /// <see cref="Detail"/> instead of <see cref="Debug.Log"/>, so their output obeys those switches
    /// and lands inside the breakdown it belongs to.
    /// </summary>
    public static class CombatLog
    {
        private const string EventIndent = "  ";
        private const string BodyIndent = "     ";

        private static GameRules rules;

        /// <summary>The attack being written. Empty and untouched while logging is off.</summary>
        private static readonly StringBuilder Builder = new();

        /// <summary>
        /// Notes a trait made while it was being asked. They are held back until the trait's own
        /// before/after line has been written, so an explanation reads under the number it explains
        /// rather than above it.
        /// </summary>
        private static readonly StringBuilder Pending = new();

        /// <summary>Traits consulted this strike that changed nothing and had nothing to say.</summary>
        private static readonly StringBuilder Unchanged = new();

        private static bool writing;

        /// <summary>
        /// Set when a strike opened the buffer itself, i.e. damage was calculated outside
        /// <see cref="CombatRunner"/>. Such a strike flushes on its own, since nobody will end it.
        /// </summary>
        private static bool strikeOwnsBuffer;

        /// <summary>Injected by the Initiator alongside <see cref="CombatRules.Setup"/>.</summary>
        public static void Setup(GameRules gameRules)
        {
            rules = gameRules;
        }

        /// <summary>
        /// Public so callers can skip building a message they would otherwise pay for while the log
        /// is off.
        /// </summary>
        public static bool Enabled => rules != null && rules.LogCombatCalculations;

        /// <summary>
        /// Whether the log also carries what is *around* the numbers: the tiles the two stood on, what
        /// a trait rolled, the traits that were asked and did nothing. Off, an attack is only its
        /// damage and what moved it. Guard building a <see cref="Detail"/> message with this.
        /// </summary>
        public static bool Details => Enabled && rules.LogCombatDetails;

        /// <summary>Opens the buffer. Writes nothing - the first strike is the header.</summary>
        public static void BeginAttack()
        {
            if (!Enabled)
                return;

            Reset();
            writing = true;
        }

        /// <summary>Flushes whatever was collected. Deliberately not gated on <see cref="Enabled"/>
        /// so an attack survives the switch being flipped while it resolves.</summary>
        public static void EndAttack()
        {
            if (writing)
                Flush();
        }

        public static void BeginStrike(CombatContext context, int baseDamage)
        {
            if (!Enabled)
                return;

            // Damage calculated outside an attack (a tool, a future preview) still gets a breakdown.
            if (!writing)
            {
                Reset();
                writing = true;
                strikeOwnsBuffer = true;
            }

            var weapon = context.Attacker != null ? context.Attacker.CurrentState.AttackAction : null;

            // A counter-strike is the same two units with their roles swapped, so only what differs -
            // the weapon that answers - is worth a line.
            if (context.IsRetaliation && Builder.Length > 0)
            {
                Line(EventIndent, $"retaliation | {NameOf(weapon)} base {baseDamage}");
                return;
            }

            var ground = Details
                ? $" | {Describe(context.AttackerTile)} -> {Describe(context.DefenderTile)}, distance {Distance(context)}"
                : "";

            Line("", $"[Combat] {Describe(context.Attacker)} -> {Describe(context.Defender)}" +
                     $" | {NameOf(weapon)} base {baseDamage}{ground}");
        }

        /// <summary>
        /// One trait's say over the running damage. A trait that changed nothing moved no number, so
        /// it is a <see cref="Details"/> matter - reported only by name, on the shared "no effect"
        /// line, unless it left a note explaining itself.
        /// </summary>
        public static void Modifier(Trait trait, bool outgoing, int before, int after)
        {
            if (!writing)
                return;

            if (after == before)
            {
                if (!Details)
                    return;

                if (Pending.Length == 0)
                {
                    if (Unchanged.Length > 0)
                        Unchanged.Append(", ");

                    Unchanged.Append(NameOf(trait));
                }
                else
                {
                    // Its note says what it would have said, so it does not also need a 0-delta line.
                    FlushPending();
                }

                return;
            }

            var origin = trait is TerrainTrait ? "terrain" : "unit";
            var side = outgoing ? "attacker" : "defender";

            Line(BodyIndent, $"{before,3} -> {after,-3} {NameOf(trait)} ({side}, {origin} {trait.GetType().Name})");

            FlushPending();
        }

        /// <summary>
        /// A trait explaining itself, e.g. a crit roll. Appears under that trait's own line, or in its
        /// place when the trait changed nothing - so name the trait in the message. Guard the call with
        /// <see cref="Details"/> so it is not built while details are off.
        /// </summary>
        public static void Detail(string message)
        {
            if (!writing || !Details)
                return;

            Pending.Append('\n').Append(BodyIndent).Append(message);
        }

        public static void EndStrike(int rawDamage, int finalDamage)
        {
            if (!writing)
                return;

            FlushPending();

            if (Unchanged.Length > 0)
            {
                Line(BodyIndent, $"no effect: {Unchanged}");
                Unchanged.Clear();
            }

            Line(BodyIndent, rawDamage != finalDamage
                ? $"= {finalDamage} damage (clamped up from {rawDamage})"
                : $"= {finalDamage} damage");

            if (strikeOwnsBuffer)
                Flush();
        }

        /// <summary>
        /// What the strike did once the damage was actually taken off. Appended to the damage line
        /// rather than given one of its own, and without a name: it is always the unit this strike
        /// is against.
        /// </summary>
        public static void Applied(int healthBefore, int healthAfter)
        {
            if (!writing)
                return;

            Builder.Append($", {healthBefore} -> {healthAfter} hp");
        }

        /// <summary>An attack-level line: why nobody struck back, who fell.</summary>
        public static void Note(string message)
        {
            if (!writing)
                return;

            Line(EventIndent, message);
        }

        public static void Removed(Unit unit)
        {
            if (unit != null)
                Note($"{unit.name} removed");
        }

        private static void Line(string indent, string text)
        {
            if (Builder.Length > 0)
                Builder.Append('\n');

            Builder.Append(indent).Append(text);
        }

        private static void FlushPending()
        {
            if (Pending.Length == 0)
                return;

            Builder.Append(Pending);
            Pending.Clear();
        }

        private static void Flush()
        {
            FlushPending();

            if (Builder.Length > 0)
                Debug.Log(Builder.ToString());

            Reset();
        }

        private static void Reset()
        {
            Builder.Clear();
            Pending.Clear();
            Unchanged.Clear();
            writing = false;
            strikeOwnsBuffer = false;
        }

        private static int Distance(CombatContext context)
        {
            if (context.AttackerTile == null || context.DefenderTile == null)
                return -1;

            return context.AttackerTile.DistanceTo(context.DefenderTile);
        }

        private static string Describe(Unit unit)
        {
            return unit != null ? $"{unit.name} ({unit.CurrentState.Team})" : "nobody";
        }

        private static string Describe(Tile tile)
        {
            return tile != null ? $"{tile.Terrain} {tile.Position}" : "no tile";
        }

        private static string NameOf(UnityEngine.Object asset)
        {
            return asset != null ? asset.name : "none";
        }
    }
}
