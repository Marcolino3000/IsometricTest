using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// How the character's two HUD rows are drawn - the health blobs, the action point blobs and the
    /// strip each row stands on. Its own asset rather than a switch on <see cref="GameRules"/> for
    /// the same reason <see cref="TooltipSettings"/> is: a rule decides what happens on the board,
    /// this decides only how a number is shown.
    ///
    /// Loaded from Resources rather than injected, like the tooltip settings: the view that reads it
    /// is built from code, so nothing has to be wired, and a missing asset falls back to these
    /// defaults instead of leaving the player without bars.
    ///
    /// A <see cref="RuntimeSettings"/>, so a size moved in the inspector redraws the rows mid-play -
    /// <c>UI.PlayerVitals</c> subscribes rather than comparing fields in an <c>Update</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Settings/Player Vitals Settings")]
    public class PlayerVitalsSettings : RuntimeSettings
    {
        public const string ResourcePath = "Settings/Default PlayerVitalsSettings";

        [Header("Health")]
        [Tooltip("Width of one health blob, in panel pixels.")]
        [Min(1f)] [SerializeField] private float healthBlobWidth = 36f;

        [Tooltip("Height of one health blob, in panel pixels.")]
        [Min(1f)] [SerializeField] private float healthBlobHeight = 36f;

        [Tooltip("How far the corners of a health blob are rounded. Half the shorter side rounds it " +
                 "into a disc; anything larger is clamped to that.")]
        [Min(0f)] [SerializeField] private float healthBlobRadius = 8f;

        [SerializeField] private Color healthColor = new(0.604f, 0.110f, 0.110f);

        [Header("Action points")]
        [Tooltip("Width and height of one action point blob - they are square.")]
        [Min(1f)] [SerializeField] private float actionPointBlobSize = 36.4f;

        [Tooltip("How far the corners of an action point blob are rounded. Left high it is a disc, " +
                 "which is what the world-space bar over a unit's head draws.")]
        [Min(0f)] [SerializeField] private float actionPointBlobRadius = 500f;

        [Tooltip("A point the turn still has.")]
        [SerializeField] private Color actionPointColor = new(0.886f, 0.694f, 0.055f);

        [Tooltip("A point the plan under the cursor would spend. Also what shows behind a previewed " +
                 "point that has no action icon of its own.")]
        [SerializeField] private Color previewColor = new(0.745f, 0.682f, 0.467f);

        [Tooltip("A point already gone.")]
        [SerializeField] private Color spentColor = new(0.878f, 0.878f, 0.878f);

        [Header("Traits")]
        [Tooltip("Width and height of one trait symbol in the row above the health - they are square.")]
        [Min(1f)] [SerializeField] private float traitIconSize = 26f;

        [Tooltip("Space between the trait row and the health row under it.")]
        [Min(0f)] [SerializeField] private float traitRowGap = 4f;

        [Header("Rows")]
        [Tooltip("Space either side of a blob, so the gap between two of them is twice this.")]
        [Min(0f)] [SerializeField] private float blobGap = 3f;

        [Tooltip("Space between the health row and the action point row under it.")]
        [Min(0f)] [SerializeField] private float rowGap = 4f;

        [Tooltip("Space between the block and the item bar under it.")]
        [Min(0f)] [SerializeField] private float bottomGap = 6f;

        [Tooltip("Space between a row's frame and its blobs - above and below, then either side.")]
        [Min(0f)] [SerializeField] private float trackPaddingVertical = 6f;

        [Min(0f)] [SerializeField] private float trackPaddingHorizontal = 5f;

        [Tooltip("Thickness of the frame around a row. Zero draws none.")]
        [Min(0f)] [SerializeField] private float trackBorderWidth = 2f;

        [Min(0f)] [SerializeField] private float trackCornerRadius = 6f;

        [Tooltip("What a row stands on. It is also what a spent point leaves behind, since a blob " +
                 "is hidden rather than dropped.")]
        [SerializeField] private Color trackColor = new(0.07f, 0.07f, 0.086f, 0.85f);

        [SerializeField] private Color trackBorderColor = new(1f, 1f, 1f, 0.35f);

        public float HealthBlobWidth => healthBlobWidth;
        public float HealthBlobHeight => healthBlobHeight;
        public float HealthBlobRadius => healthBlobRadius;
        public Color HealthColor => healthColor;

        public float ActionPointBlobSize => actionPointBlobSize;
        public float ActionPointBlobRadius => actionPointBlobRadius;
        public Color ActionPointColor => actionPointColor;
        public Color PreviewColor => previewColor;
        public Color SpentColor => spentColor;

        /// <summary>
        /// How big a trait symbol is drawn. Read rather than the field so an asset saved before the
        /// trait row existed - which deserializes to zero - still draws something.
        /// </summary>
        public float TraitIconSize => traitIconSize > 0f ? traitIconSize : 26f;

        public float TraitRowGap => traitRowGap;

        public float BlobGap => blobGap;
        public float RowGap => rowGap;
        public float BottomGap => bottomGap;
        public float TrackPaddingVertical => trackPaddingVertical;
        public float TrackPaddingHorizontal => trackPaddingHorizontal;
        public float TrackBorderWidth => trackBorderWidth;
        public float TrackCornerRadius => trackCornerRadius;
        public Color TrackColor => trackColor;
        public Color TrackBorderColor => trackBorderColor;

        /// <summary>
        /// The authored asset, or a default instance where none is authored yet. Never null, so the
        /// view needs no branch for a project that has not made one.
        /// </summary>
        public static PlayerVitalsSettings Load()
        {
            var settings = Resources.Load<PlayerVitalsSettings>(ResourcePath);

            return settings != null ? settings : CreateInstance<PlayerVitalsSettings>();
        }
    }
}
