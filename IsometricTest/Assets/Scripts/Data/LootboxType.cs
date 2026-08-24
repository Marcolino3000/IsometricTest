using System.Collections.Generic;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// The two ways a box can turn up. A kind is one or the other rather than some of each: where a
    /// box comes from is what it *is*, so a tier can read as the spoils of a kill or as a find on the
    /// map without a second number saying how much of it is which.
    /// </summary>
    public enum LootboxSource
    {
        /// <summary>Lying about the map from the start, within the kind's own ring.</summary>
        ScatteredOnMap,

        /// <summary>
        /// Left behind by a fallen unit. Every unit leaves one box, so a kind of this source makes
        /// exactly as many boxes as there are units to fall and its own count is never asked.
        /// </summary>
        DroppedByUnits
    }

    /// <summary>
    /// One kind - in practice one tier - of lootbox: what it looks like, what it costs to take, where
    /// its boxes come from, how many of them a match contains and what share of them holds what.
    ///
    /// It deliberately does **not** list what may be inside it. That is authored the other way round:
    /// every <see cref="Item"/> names the kind of box it turns up in on <see cref="Item.FoundIn"/>,
    /// so a new item is placed in the loot table by the same asset that defines it and no second
    /// list has to be kept in step. A kind is therefore a whole ScriptableObject holding nothing but
    /// its own look and its own numbers, and a further one is a further asset and no code.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Loot/LootboxType")]
    public class LootboxType : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("What this kind of box is called. Falls back to the asset name while empty.")]
        public string DisplayName;

        [Tooltip("The sprite a box of this kind is drawn with. Replaces whatever the shared prefab " +
                 "carries, so a further kind needs a further asset and no further prefab - and so " +
                 "the tier of a box is legible on the board before it is opened.")]
        public Sprite Sprite;

        [Tooltip("When on, what lies on the tile is drawn with the symbol of the item inside it " +
                 "rather than with the sprite above - the find is not hidden in a box and can be " +
                 "seen for what it is from across the map. What artefacts are found this way. An " +
                 "item carrying no symbol yet falls back to the sprite, so nothing goes invisible " +
                 "while the art is still being drawn.")]
        public bool ShowsContent;

        [Tooltip("Action points taking a box of this kind costs. Belongs to the box rather than to " +
                 "the unit taking it, so a richer tier can simply ask for more.")]
        [Min(0)] public int PickupCost = 1;

        [Header("Where They Come From")]
        [Tooltip("Whether this kind lies about the map from the start or is left behind by fallen " +
                 "units. Every unit leaves one box, so a dropped kind makes exactly as many boxes " +
                 "as there are units to fall and the count below is not asked; two dropped kinds " +
                 "share those units evenly.")]
        public LootboxSource Source;

        [Header("How Many And What Is In Them")]
        [Tooltip("How many boxes of this kind a match contains. Only asked of a kind scattered over " +
                 "the map - a dropped one makes one box per unit. Still capped by how many free " +
                 "tiles are left to lie on.")]
        [Min(0)] public int LootboxCount = 1;

        [Tooltip("Percent of this kind's boxes holding a melee weapon.")]
        [Range(0, 100)] public int MeleeWeaponPercent;

        [Tooltip("Percent of this kind's boxes holding a ranged weapon.")]
        [Range(0, 100)] public int RangedWeaponPercent;

        [Tooltip("Percent of this kind's boxes holding an active item - a potion or the like, used " +
                 "once and gone.")]
        [Range(0, 100)] public int ActiveItemPercent;

        [Tooltip("Percent of this kind's boxes holding a passive item - gear worn for its traits.")]
        [Range(0, 100)] public int PassiveItemPercent;

        [Tooltip("Percent of this kind's boxes holding an artefact - a unique find worn for good in " +
                 "a slot of its own. Each artefact is dealt once, so a kind asking for as many " +
                 "boxes as there are artefacts puts every one of them on the map.")]
        [Range(0, 100)] public int ArtefactPercent;

        [Header("Where On The Map They Lie")]
        [Tooltip("The innermost this kind's scattered boxes lie, as a fraction of the way from the " +
                 "middle of the map to its furthest tile: 0 is the centre, 1 the rim. A fraction " +
                 "rather than a number of tiles, so it means the same on any map size.")]
        [Range(0f, 1f)] public float MinDistanceFromCenter;

        [Tooltip("The outermost they lie, on the same scale. Together with the field above this is " +
                 "the ring a tier is found in - a richer tier set further out is a longer walk for " +
                 "a better find. A ring with no tile left to spare spills over its border rather " +
                 "than losing its boxes, exactly as a spawn zone does.")]
        [Range(0f, 1f)] public float MaxDistanceFromCenter = 1f;

        /// <summary>What to call this kind, falling back to the asset name while none is authored.</summary>
        public string Title => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        /// <summary>
        /// The percentages added up. Meant to come to 100, but read as shares of whatever it comes
        /// to, so a kind authored to 90 or to 120 still fills every box it asks for rather than
        /// leaving some empty or asking for more than it makes. Zero means nothing was authored at
        /// all, which is the one case that yields no boxes.
        /// </summary>
        public int TotalPercent =>
            MeleeWeaponPercent + RangedWeaponPercent + ActiveItemPercent + PassiveItemPercent +
            ArtefactPercent;

        /// <summary>
        /// How many of <paramref name="total"/> boxes hold each category, indexed by
        /// <see cref="SlotKind"/>. Derived rather than authored so the total and the parts can never
        /// disagree: the boxes exist because the kind asked for that many, and the percentages only
        /// say how they are shared out.
        /// </summary>
        public int[] CategoryCounts(int total)
        {
            return Distribute(total, new[]
            {
                MeleeWeaponPercent, RangedWeaponPercent, ActiveItemPercent, PassiveItemPercent,
                ArtefactPercent
            });
        }

        /// <summary>
        /// Hands <paramref name="total"/> out in the proportions of <paramref name="shares"/>, in
        /// whole boxes that always add back up to it: each gets its floor, and the rounding
        /// leftovers go to the largest remainders first. A share of nothing - or shares that are all
        /// nothing - gets nothing.
        ///
        /// The one place a proportion becomes a count, shared by the categories within a kind and by
        /// the dropped kinds splitting the units between them, so neither can round its way to a
        /// different total than it was given.
        /// </summary>
        public static int[] Distribute(int total, IReadOnlyList<int> shares)
        {
            var counts = new int[shares.Count];

            if (total <= 0)
                return counts;

            var sum = 0;

            foreach (var share in shares)
                sum += Mathf.Max(0, share);

            if (sum <= 0)
                return counts;

            var remainders = new int[shares.Count];
            var given = 0;

            for (var i = 0; i < shares.Count; i++)
            {
                var share = Mathf.Max(0, shares[i]) * total;

                counts[i] = share / sum;
                remainders[i] = share % sum;
                given += counts[i];
            }

            // Fewer left over than there are shares, so nobody is topped up twice.
            for (var left = total - given; left > 0; left--)
            {
                var best = -1;

                for (var i = 0; i < remainders.Length; i++)
                    if (remainders[i] > 0 && (best < 0 || remainders[i] > remainders[best]))
                        best = i;

                if (best < 0)
                    break;

                counts[best]++;
                remainders[best] = 0;
            }

            return counts;
        }

        /// <summary>
        /// How far a tile lying at <paramref name="distanceFromCenter"/> (0 in the middle of the map,
        /// 1 at its furthest tile) falls outside this kind's ring; 0 inside it, which is what makes
        /// the ring itself sort as one block for the shuffle behind it to scatter.
        ///
        /// The same shape as a spawn zone's miss distance, and for the same reason: the ring is
        /// preferred rather than required, so a kind whose ring is walled off by mountains or already
        /// taken up by the tiers before it takes the nearest ground outside it instead of not being
        /// placed at all.
        /// </summary>
        public float DistanceOutsideRing(float distanceFromCenter)
        {
            // Authored the wrong way round is read as the ring between the two rather than as an
            // empty one, which would push every box of the kind out to the rim.
            var inner = Mathf.Min(MinDistanceFromCenter, MaxDistanceFromCenter);
            var outer = Mathf.Max(MinDistanceFromCenter, MaxDistanceFromCenter);

            return Mathf.Max(0f, Mathf.Max(inner - distanceFromCenter, distanceFromCenter - outer));
        }

        /// <summary>What share of this kind's boxes hold <paramref name="kind"/>. Zero for a non-category.</summary>
        public int PercentFor(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.Melee => MeleeWeaponPercent,
                SlotKind.Ranged => RangedWeaponPercent,
                SlotKind.Active => ActiveItemPercent,
                SlotKind.Passive => PassiveItemPercent,
                SlotKind.Artefact => ArtefactPercent,
                _ => 0
            };
        }
    }
}
