namespace Runtime.Gameplay.Fog
{
    /// <summary>
    /// How much a tile is revealed for the team currently being viewed.
    /// </summary>
    public enum TileVisibility
    {
        /// <summary>Never seen by this team: rendered dark, no terrain or unit info.</summary>
        Hidden,

        /// <summary>
        /// Seen before but not currently in sight: terrain is remembered (dimmed),
        /// but units on it are hidden because their current position is unknown.
        /// </summary>
        Explored,

        /// <summary>Currently within a friendly unit's sight: fully lit, units shown.</summary>
        Visible
    }
}
