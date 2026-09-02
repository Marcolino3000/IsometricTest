using Runtime.Gameplay.Global;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// The character's health and action points, drawn in the HUD directly above the item slots
    /// instead of on two world-space panels over its head. Only the character's: an enemy's numbers
    /// belong over the enemy, and there is one HUD.
    ///
    /// A piece of a view like <see cref="Banner"/> rather than a view of its own - it is put into
    /// whatever element the caller hands over. That element is the item bar's own column
    /// (<see cref="ItemBar.MountAbove"/>), which is what makes "above the slots" need no measuring:
    /// the rows are centred on the slot row and move with it, so nothing here has to know how tall
    /// the bar turned out or where on the screen it ended up.
    ///
    /// <b>It draws no blob itself.</b> <see cref="HealthBar"/> and <see cref="ActionsPointsBar"/>
    /// stay the only things that decide which blob is lit, previewed or spent; this hands them a row
    /// to draw in and a blob to draw with. The character's bars and an enemy's are therefore the same
    /// two classes, differing in where they are mounted and how big a blob is drawn and in nothing
    /// else - a preview icon, a raised maximum and an undo all reach the HUD without knowing it exists.
    ///
    /// <b>What a blob and a row look like is authored</b>, in <see cref="PlayerVitalsSettings"/>. Its
    /// edits are answered rather than polled for, and answered by restyling the elements the bars
    /// built rather than by asking them to build again: a size is written onto a blob, so moving one
    /// in the inspector needs no rebuild and no bar has to remember what it was last shown.
    ///
    /// It picks nothing: the rows say what the character has, and a click on them is meant for the
    /// board behind.
    /// </summary>
    public class PlayerVitals
    {
        /// <summary>The block holding both rows, for the caller to place.</summary>
        public VisualElement Root { get; }

        /// <summary>Where the health blobs go - handed to the character's <see cref="HealthBar"/>.</summary>
        public VisualElement HealthRow { get; }

        /// <summary>The same for its <see cref="ActionsPointsBar"/>.</summary>
        public VisualElement ActionPointRow { get; }

        private readonly PlayerVitalsSettings settings;

        public PlayerVitals()
        {
            settings = PlayerVitalsSettings.Load();

            Root = new VisualElement { name = "playerVitals", pickingMode = PickingMode.Ignore };
            Root.style.alignItems = Align.Center;

            HealthRow = AddTrack("healthRow");
            // Under the health and nearest the slots: what a point is about to be spent on is
            // previewed both here and on the row of items below it.
            ActionPointRow = AddTrack("actionPointRow");

            ApplyLook();

            settings.Changed += ApplyLook;
            // The asset outlives the panel, so the row that has left it stops listening - see
            // RuntimeSettings.
            Root.RegisterCallback<DetachFromPanelEvent>(_ => settings.Changed -= ApplyLook);
        }

        /// <summary>
        /// One health blob. The name is the template's own and is ignored - a health row has one
        /// kind of blob.
        /// </summary>
        public VisualElement HealthBlob(string name) => StyleHealthBlob(NewBlob(name));

        /// <summary>
        /// One of the three blobs an action point is drawn with, told apart by the name the
        /// template gives it: the point in hand, the point a plan would spend, the point already
        /// gone. <see cref="ActionsPointsBar"/> shows exactly one of them per point and reads the
        /// preview one's colour and radius back off it, so both are set here rather than left to
        /// the fallback.
        /// </summary>
        public VisualElement ActionPointBlob(string name) => StyleActionPointBlob(NewBlob(name));

        /// <summary>
        /// Draws both rows the way the settings asset says. Called once at build and again whenever
        /// the asset is edited - the blobs the bars built are restyled in place, since what a size
        /// or a colour changes is written onto them and nothing about which one is lit.
        /// </summary>
        private void ApplyLook()
        {
            Root.style.marginBottom = settings.BottomGap;

            StyleTrack(HealthRow, settings.RowGap);
            StyleTrack(ActionPointRow, 0f);

            foreach (var blob in HealthRow.Children())
                StyleHealthBlob(blob);

            foreach (var blob in ActionPointRow.Children())
                StyleActionPointBlob(blob);
        }

        private VisualElement StyleHealthBlob(VisualElement blob) =>
            StyleBlob(blob, settings.HealthBlobWidth, settings.HealthBlobHeight,
                settings.HealthBlobRadius, settings.HealthColor);

        private VisualElement StyleActionPointBlob(VisualElement blob)
        {
            var color = blob.name switch
            {
                "active" => settings.ActionPointColor,
                "previewInactive" => settings.PreviewColor,
                _ => settings.SpentColor
            };

            return StyleBlob(blob, settings.ActionPointBlobSize, settings.ActionPointBlobSize,
                settings.ActionPointBlobRadius, color);
        }

        private static VisualElement NewBlob(string name) =>
            new() { name = name, pickingMode = PickingMode.Ignore };

        private VisualElement StyleBlob(VisualElement blob, float width, float height, float radius,
            Color color)
        {
            blob.style.width = width;
            blob.style.height = height;
            blob.style.marginLeft = settings.BlobGap;
            blob.style.marginRight = settings.BlobGap;
            blob.style.backgroundColor = color;

            blob.style.borderTopLeftRadius = radius;
            blob.style.borderTopRightRadius = radius;
            blob.style.borderBottomRightRadius = radius;
            blob.style.borderBottomLeftRadius = radius;

            return blob;
        }

        /// <summary>
        /// The strip a row of blobs stands on. It is what a spent point leaves behind: the bar hides
        /// a blob rather than dropping it, so the gap it leaves reads as an empty socket instead of
        /// as a row that shrank.
        /// </summary>
        private void StyleTrack(VisualElement track, float gapBelow)
        {
            track.style.backgroundColor = settings.TrackColor;
            track.style.marginBottom = gapBelow;

            CardStyle.SetPadding(track, settings.TrackPaddingVertical, settings.TrackPaddingHorizontal);

            SetBorder(track, settings.TrackBorderWidth, settings.TrackCornerRadius,
                settings.TrackBorderColor);
        }

        private VisualElement AddTrack(string name)
        {
            var track = new VisualElement { name = name, pickingMode = PickingMode.Ignore };

            track.style.flexDirection = FlexDirection.Row;
            track.style.alignItems = Align.Center;

            Root.Add(track);

            return track;
        }

        /// <summary>
        /// <see cref="CardStyle.SetBorder"/> with the colour handed in - a row's frame is authored
        /// rather than the one every card built in code wears.
        /// </summary>
        private static void SetBorder(VisualElement element, float width, float radius, Color color)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;

            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;

            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
        }
    }
}
