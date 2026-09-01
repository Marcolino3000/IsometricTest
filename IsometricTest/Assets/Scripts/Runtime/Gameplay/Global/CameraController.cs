using UnityEngine;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Owns the view of the board: where it looks, how far out it is zoomed, and how far it may
    /// travel. Its own system rather than a corner of <see cref="InputHandler"/>, which used to hold
    /// the pan, the drag and the centring with no state of its own - a growing map makes bounds and
    /// zoom load-bearing, and neither belongs in a class about what the player pressed.
    ///
    /// The split with the input handler is that one reports pointer facts in screen pixels and this
    /// decides what they mean in the world: the handler never touches a camera, and this never reads
    /// a key. Grab-the-world dragging needs a screen point turned into a world one, which is why the
    /// drag arrives here as three plain events rather than as a finished delta.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 10f;

        [Tooltip("What the pan moves. Defaults to the main camera's parent, so every camera on the " +
                 "rig - the UI overlay included - travels together.")]
        [SerializeField] private Transform cameraRig;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minOrthographicSize = 2f;
        [SerializeField] private float maxOrthographicSize = 20f;

        [Header("Bounds")]
        [Tooltip("How far past the edge of the board the view may travel, in world units. Zero pins " +
                 "the rig to the outermost tiles; a little slack keeps the corner tiles clear of the " +
                 "screen edge.")]
        [SerializeField] private float boundsPadding = 4f;

        private Camera cam;
        private Transform panTarget;
        private InputHandler input;

        // The board's extent, handed over once the tiles exist. Until then the view is unbounded -
        // clamping to a board that has not been built would pin it to the origin.
        private Bounds? bounds;

        private bool isDragging;
        private Vector3 dragWorldAnchor;

        public void Setup(InputHandler inputHandler)
        {
            ResolveCamera();

            if (inputHandler == null)
                return;

            input = inputHandler;

            input.PanDragStarted += HandleDragStarted;
            input.PanDragMoved += HandleDragMoved;
            input.PanDragEnded += HandleDragEnded;
            input.ZoomChanged += Zoom;
        }

        private void OnDestroy()
        {
            if (input == null)
                return;

            input.PanDragStarted -= HandleDragStarted;
            input.PanDragMoved -= HandleDragMoved;
            input.PanDragEnded -= HandleDragEnded;
            input.ZoomChanged -= Zoom;
        }

        // The held pan axis is read once a frame rather than raised: it is a direction being held,
        // not something that happened.
        private void Update()
        {
            if (input != null)
                Pan(input.PanAxis);
        }

        /// <summary>
        /// The board the view is kept over. Given by the Initiator once the tiles are down, since how
        /// far the camera may travel is the grid's business and the grid is built at runtime.
        /// </summary>
        public void SetBounds(Bounds boardBounds)
        {
            bounds = boardBounds;
            ClampToBounds();
        }

        /// <summary>
        /// Puts <paramref name="target"/> in the middle of the screen. The offset is measured from
        /// the world point currently at the screen centre, so a camera sitting off-centre inside the
        /// rig keeps its offset instead of being corrected away.
        /// </summary>
        public void CenterOn(Transform target)
        {
            if (target == null)
                return;

            ResolveCamera();

            if (cam == null || panTarget == null)
                return;

            var screenCenter = ScreenToWorld(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            var delta = target.position - screenCenter;
            delta.z = 0f;
            panTarget.position += delta;

            ClampToBounds();
        }

        /// <summary>
        /// Moves the view. <paramref name="axis"/> is the raw stick or WASD vector; the frame's own
        /// length is applied here, so the caller hands over a direction rather than a distance.
        /// </summary>
        public void Pan(Vector2 axis)
        {
            if (panTarget == null || axis == Vector2.zero)
                return;

            var delta = new Vector3(axis.x, axis.y, 0f) * (panSpeed * Time.deltaTime);
            panTarget.Translate(delta, Space.World);

            ClampToBounds();
        }

        /// <summary>
        /// Zooms by a scroll notch. Orthographic size only - the projection is flat, so there is no
        /// dolly to speak of, and clamping the size is what keeps the board readable at both ends.
        /// </summary>
        public void Zoom(float amount)
        {
            if (cam == null || !cam.orthographic || Mathf.Approximately(amount, 0f))
                return;

            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - amount * zoomSpeed,
                minOrthographicSize, maxOrthographicSize);

            // A wider view can see past an edge it was inside a moment ago.
            ClampToBounds();
        }

        private void HandleDragStarted(Vector2 screenPosition)
        {
            ResolveCamera();

            if (cam == null)
                return;

            dragWorldAnchor = ScreenToWorld(screenPosition);
            isDragging = true;
        }

        /// <summary>
        /// Keeps the world point grabbed on the press under the cursor - the drag moves the board,
        /// not the camera, which is what makes it feel like the map is being pulled.
        /// </summary>
        private void HandleDragMoved(Vector2 screenPosition)
        {
            if (!isDragging || cam == null || panTarget == null)
                return;

            var worldUnderCursor = ScreenToWorld(screenPosition);
            var delta = dragWorldAnchor - worldUnderCursor;
            delta.z = 0f;
            panTarget.position += delta;

            ClampToBounds();
        }

        private void HandleDragEnded() => isDragging = false;

        /// <summary>
        /// Keeps the rig over the board. Clamped on the rig rather than on the camera so every camera
        /// hanging off it stays in step, and skipped entirely until a board has been handed over.
        /// </summary>
        private void ClampToBounds()
        {
            if (bounds == null || panTarget == null)
                return;

            var limit = bounds.Value;
            var position = panTarget.position;

            position.x = Mathf.Clamp(position.x, limit.min.x - boundsPadding, limit.max.x + boundsPadding);
            position.y = Mathf.Clamp(position.y, limit.min.y - boundsPadding, limit.max.y + boundsPadding);

            panTarget.position = position;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        }

        /// <summary>
        /// Resolves the camera and the transform it pans. Asked again before centring, since that is
        /// requested from the Initiator's Awake, which may run before this component's own OnEnable.
        /// </summary>
        private void ResolveCamera()
        {
            if (cam == null)
                cam = Camera.main;

            if (panTarget != null)
                return;

            if (cameraRig != null)
            {
                panTarget = cameraRig;
                return;
            }

            if (cam == null)
                return;

            panTarget = cam.transform.parent != null ? cam.transform.parent : cam.transform;
        }
    }
}
