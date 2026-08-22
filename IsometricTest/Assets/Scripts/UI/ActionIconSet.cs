using System;
using System.Collections.Generic;
using Runtime.Gameplay.History;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Which cell of an icon sheet stands for which kind of action. Authored rather than decided in
    /// code, and kept as one table, so a kind is named in a single place: a further kind is a further
    /// entry here, never a switch wherever an action has to be shown.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/UI/Action Icon Set")]
    public class ActionIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The kind of action this cell stands for. A kind listed twice takes its first entry.")]
            public ActionKind Kind;

            [Tooltip("Cell on the sheet, counted from the top left corner and starting at one - the " +
                     "numbers a sprite sheet viewer prints.")]
            public Vector2Int Cell;
        }

        [Tooltip("Sheet the icons are cut from. Cut here rather than sliced by the importer so the " +
                 "coordinates stay readable next to the kind they belong to.")]
        [SerializeField] private Texture2D sheet;

        [Tooltip("Edge length of one cell in pixels. The sheet is taken to be an even grid of them.")]
        [SerializeField] private int cellSize = 16;

        [SerializeField] private List<Entry> icons = new();

        /// <summary>
        /// Cut once and kept: a preview is rebuilt for every hovered tile, and cutting again would be
        /// a fresh sprite per blob per frame.
        /// </summary>
        private readonly Dictionary<ActionKind, Sprite> cut = new();

        /// <summary>
        /// The icon standing for <paramref name="kind"/>, or null when the sheet names none - which
        /// is what leaves an unlisted kind with the plain look it had before.
        /// </summary>
        public Sprite For(ActionKind kind)
        {
            if (cut.TryGetValue(kind, out var cached) && cached != null)
                return cached;

            var sprite = Cut(kind);
            cut[kind] = sprite;

            return sprite;
        }

        private Sprite Cut(ActionKind kind)
        {
            if (sheet == null || cellSize <= 0)
                return null;

            foreach (var entry in icons)
            {
                if (entry.Kind != kind)
                    continue;

                // Cells are counted from the top left while a texture is measured from the bottom
                // left, so the row is mirrored - the sheet is read the way it is looked at.
                var rect = new Rect(
                    (entry.Cell.x - 1) * cellSize,
                    sheet.height - entry.Cell.y * cellSize,
                    cellSize,
                    cellSize);

                if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > sheet.width || rect.yMax > sheet.height)
                {
                    Debug.LogWarning($"{name}: cell {entry.Cell} for {kind} lies outside {sheet.name}.", this);
                    return null;
                }

                var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), cellSize);
                sprite.name = $"{sheet.name} {entry.Cell.x},{entry.Cell.y}";
                // Made at runtime, so it must never be written into the asset that made it.
                sprite.hideFlags = HideFlags.HideAndDontSave;

                return sprite;
            }

            return null;
        }

        private void OnDisable()
        {
            // The sprites are made here, so they are dropped here rather than leaking one per domain
            // reload; the next For() cuts them again.
            foreach (var sprite in cut.Values)
            {
                if (sprite == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }

            cut.Clear();
        }
    }
}
