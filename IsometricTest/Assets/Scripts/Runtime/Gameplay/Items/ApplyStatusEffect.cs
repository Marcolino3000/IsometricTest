using System.Collections.Generic;
using System.Linq;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Traits;
using UnityEngine;

namespace Runtime.Gameplay.Items
{
    /// <summary>
    /// Puts statuses on whoever the item reaches - a war paint that hastens the drinker, a flask
    /// thrown to cripple what stands around it. Which units that is is not this class's business but
    /// the targeting on <c>ActionEffect</c>: naming none it is the character using the item, and an
    /// area selector makes the same asset a thrown one.
    /// </summary>
    [System.Serializable]
    public class ApplyStatusEffect : ActiveItemEffect
    {
        [Tooltip("Statuses put on each unit this effect reaches.")]
        public List<StatusTrait> Statuses = new();

        public override string Summary
        {
            get
            {
                var named = Statuses.Where(status => status != null).ToList();

                if (named.Count == 0)
                    return "Inflicts nothing";

                // Each says what it is worth and for how long, so nothing here takes it apart.
                return $"Inflicts {string.Join(", ", named.Select(status => status.Summary))}{TargetSummary}";
            }
        }

        public override void Apply(Unit target)
        {
            foreach (var status in Statuses)
                target.ApplyStatus(status);
        }
    }
}
