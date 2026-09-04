using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Takes statuses off - the salve to the wound. Names the ones it lifts, or none at all to lift
    /// whatever the unit is carrying, which is what a full remedy is.
    ///
    /// Its own class rather than a switch on <see cref="ApplyStatusEffect"/>: what it authors is a
    /// different list - the statuses to look for rather than the ones to put on - and an item that
    /// both inflicts and cures is two effects on one asset.
    /// </summary>
    [System.Serializable]
    public class CureStatusEffect : ActiveItemEffect
    {
        [Tooltip("Statuses lifted. Leave empty to lift every status the unit carries.")]
        public List<StatusTrait> Statuses = new();

        [Tooltip("Shown over a unit nothing came off - a remedy taken by somebody who was not " +
                 "afflicted. Empty says nothing.")]
        public string NothingToCureNotice = "Nothing to cure";

        private bool CuresEverything => Statuses == null || Statuses.All(status => status == null);

        public override string Summary
        {
            get
            {
                if (CuresEverything)
                    return $"Cures every status{TargetSummary}";

                var names = Statuses.Where(status => status != null).Select(status => status.name);

                return $"Cures {string.Join(", ", names)}{TargetSummary}";
            }
        }

        public override void Apply(Unit target)
        {
            var cured = false;

            if (CuresEverything)
            {
                cured = target.CurrentState.CureAllStatuses();
            }
            else
            {
                foreach (var status in Statuses)
                    cured |= target.CurrentState.CureStatus(status);
            }

            // A remedy that found nothing is still drunk and still paid for, so it has to say why
            // nothing happened - the same reason a lootbox holding something uncarryable does.
            if (!cured && !string.IsNullOrWhiteSpace(NothingToCureNotice))
                target.ShowNotice(NothingToCureNotice);
        }
    }
}
