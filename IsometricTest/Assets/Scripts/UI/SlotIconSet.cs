using System;
using System.Collections.Generic;
using Runtime.Gameplay.Items;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Which cell of an icon sheet and which colour stand for which category of item. Authored as one
    /// table like <see cref="ActionIconSet"/>, so a category is described in a single place: a further
    /// category is a further entry here, never a look decided wherever a slot is drawn.
    ///
    /// Both belong to the *category*, not to any item in it. The symbol is what an empty slot shows
    /// in place of the item it does not hold, and the accent is worn by every slot of the category
    /// whether it holds something or not - which is the point of it: the row has to say what each of
    /// its slots is for before anything has been found to put there.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/UI/Slot Icon Set")]
    public class SlotIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The category this entry describes. One listed twice takes its first entry.")]
            public SlotKind Kind;

            [Tooltip("Cell on the sheet, counted from the top left corner and starting at one - the " +
                     "numbers a sprite sheet viewer prints.")]
            public Vector2Int Cell;

            [Tooltip("Colour of the strip along the bottom edge of every slot of this category. Not " +
                     "the frame, which is where the yellow of an item in use already lives.")]
            public Color Accent;
        }

        [Tooltip("Sheet the symbols are cut from. Cut here rather than sliced by the importer so the " +
                 "coordinates stay readable next to the category they belong to.")]
        [SerializeField] private Texture2D sheet;

        [Tooltip("Edge length of one cell in pixels. The sheet is taken to be an even grid of them.")]
        [SerializeField] private int cellSize = 16;

        [SerializeField] private List<Entry> categories = new();

        /// <summary>
        /// Cut once and kept: every slot of the bar asks again whenever anything about the inventory
        /// changes, and cutting again would be a fresh sprite per slot per refresh.
        /// </summary>
        private readonly Dictionary<SlotKind, Sprite> cut = new();

        /// <summary>
        /// The symbol standing for <paramref name="kind"/>, or null when the table names none - which
        /// leaves an unlisted category's empty slots blank, the way they were before.
        /// </summary>
        public Sprite IconFor(SlotKind kind)
        {
            if (cut.TryGetValue(kind, out var cached) && cached != null)
                return cached;

            var sprite = Cut(kind);
            cut[kind] = sprite;

            return sprite;
        }

        /// <summary>
        /// The colour standing for <paramref name="kind"/>, or a fully transparent one for a category
        /// the table does not name - which draws no strip at all rather than a black one.
        /// </summary>
        public Color AccentFor(SlotKind kind)
        {
            foreach (var entry in categories)
                if (entry.Kind == kind)
                    return entry.Accent;

            return Color.clear;
        }

        private Sprite Cut(SlotKind kind)
        {
            foreach (var entry in categories)
                if (entry.Kind == kind)
                    return IconSheet.Cut(sheet, cellSize, entry.Cell, this, Item.NameOf(kind));

            return null;
        }

        private void OnDisable()
        {
            // The sprites are made here, so they are dropped here; the next IconFor cuts them again.
            foreach (var sprite in cut.Values)
                IconSheet.Release(sprite);

            cut.Clear();
        }
    }
}
