using UnityEngine;

namespace UI
{
    /// <summary>
    /// Cuts one cell out of an icon sheet. Shared by the tables that name cells - which cell stands
    /// for which kind of action, which for which category of item - so the convention they are all
    /// authored in is written down once: cells are counted from the top left corner and start at
    /// one, the numbers a sprite sheet viewer prints.
    /// </summary>
    public static class IconSheet
    {
        /// <summary>
        /// The sprite for <paramref name="cell"/>, or null when there is no sheet or the cell falls
        /// outside it - which is what leaves whoever asked with the plain look it had before.
        /// <paramref name="context"/> is what a warning points at and <paramref name="what"/> what it
        /// says the cell was wanted for.
        /// </summary>
        public static Sprite Cut(Texture2D sheet, int cellSize, Vector2Int cell, Object context, string what)
        {
            if (sheet == null || cellSize <= 0)
                return null;

            // Cells are counted from the top left while a texture is measured from the bottom left,
            // so the row is mirrored - the sheet is read the way it is looked at.
            var rect = new Rect(
                (cell.x - 1) * cellSize,
                sheet.height - cell.y * cellSize,
                cellSize,
                cellSize);

            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > sheet.width || rect.yMax > sheet.height)
            {
                Debug.LogWarning($"{context?.name}: cell {cell} for {what} lies outside {sheet.name}.", context);

                return null;
            }

            var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), cellSize);
            sprite.name = $"{sheet.name} {cell.x},{cell.y}";
            // Made at runtime, so it must never be written into the asset that made it.
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }

        /// <summary>
        /// Drops sprites cut here rather than leaking one per domain reload - what a table calls when
        /// it goes away, after which asking again cuts them afresh.
        /// </summary>
        public static void Release(Sprite sprite)
        {
            if (sprite == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(sprite);
            else
                Object.DestroyImmediate(sprite);
        }
    }
}
