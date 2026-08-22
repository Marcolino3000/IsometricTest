using System.Collections.Generic;
using System.Text;
using Runtime.Gameplay.Entities;

namespace Runtime.Gameplay.History
{
    /// <summary>
    /// One recorded action, together with the state of the match on either side of it:
    /// <see cref="Before"/> is the state it was taken in, <see cref="After"/> the state it produced.
    /// Undo restores a Before, redo restores an After.
    /// </summary>
    public sealed class HistoryEntry
    {
        public readonly ActionKind Kind;

        /// <summary>Short description of the action, e.g. "Archer attacks Knight".</summary>
        public readonly string Label;

        /// <summary>What it changed, derived from the two snapshots, e.g. "Knight -5 HP, 3 AP".</summary>
        public readonly string Detail;

        public readonly GameSnapshot Before;
        public readonly GameSnapshot After;

        public HistoryEntry(ActionReport report, GameSnapshot before, GameSnapshot after, int turnNumber)
        {
            Kind = report.Kind;
            Before = before;
            After = after;
            Label = BuildLabel(report, after, turnNumber);
            Detail = BuildDetail(report, before, after);
        }

        private static string BuildLabel(ActionReport report, GameSnapshot after, int turnNumber)
        {
            switch (report.Kind)
            {
                case ActionKind.Move:
                    var tile = FindUnit(after, report.Actor).Position;
                    var destination = tile != null ? $"{tile.Position.x},{tile.Position.y}" : "?";
                    return $"{DisplayName(report.Actor)} moves to ({destination})";

                case ActionKind.Attack:
                    return $"{DisplayName(report.Actor)} attacks {DisplayName(report.Target)}";

                case ActionKind.Pickup:
                    return $"{DisplayName(report.Actor)} picks up a lootbox";

                case ActionKind.UseItem:
                    return $"{DisplayName(report.Actor)} uses an item";

                case ActionKind.Merge:
                    return $"{DisplayName(report.Actor)} merges two items";

                default:
                    return $"Turn {turnNumber}: {report.Team}";
            }
        }

        /// <summary>
        /// Reads the outcome out of the two snapshots rather than out of the action, so damage,
        /// kills and spent action points show up for any action - including ones added later.
        /// </summary>
        private static string BuildDetail(ActionReport report, GameSnapshot before, GameSnapshot after)
        {
            var parts = new List<string>();

            foreach (var afterUnit in after.Units)
            {
                var beforeUnit = FindUnit(before, afterUnit.Unit);
                if (beforeUnit.Unit == null)
                    continue;

                var healthDelta = afterUnit.Health - beforeUnit.Health;

                if (beforeUnit.Alive && !afterUnit.Alive)
                    parts.Add($"{DisplayName(afterUnit.Unit)} falls");
                else if (healthDelta != 0)
                    parts.Add($"{DisplayName(afterUnit.Unit)} {healthDelta:+#;-#;0} HP");
            }

            // How a merge went, read out of the inventory rather than off the report: a successful
            // one leaves behind a weapon that was not owned before, a failed one only spends what it
            // was fed. Diffed like everything else here, so the report itself stays "I acted".
            if (report.Kind == ActionKind.Merge)
                parts.Add(after.Items.Exists(item => !before.Items.Contains(item))
                    ? "merge succeeded"
                    : "merge failed");

            // Action point refreshes on a turn change would drown out everything else, so only an
            // acting unit's own spending is reported.
            if (report.Actor != null)
            {
                var spent = FindUnit(before, report.Actor).ActionPoints - FindUnit(after, report.Actor).ActionPoints;
                if (spent > 0)
                    parts.Add($"{spent} AP");
            }

            return string.Join(", ", parts);
        }

        private static UnitSnapshot FindUnit(GameSnapshot snapshot, Unit unit)
        {
            if (unit == null)
                return default;

            foreach (var candidate in snapshot.Units)
            {
                if (candidate.Unit == unit)
                    return candidate;
            }

            return default;
        }

        /// <summary>Readable unit name for the history list ("Melee(Clone)" reads as "Melee").</summary>
        private static string DisplayName(Unit unit)
        {
            if (unit == null)
                return "Unit";

            var name = new StringBuilder(unit.name).Replace("(Clone)", string.Empty).ToString();
            return name.Trim();
        }
    }
}
