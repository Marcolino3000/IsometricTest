using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Gameplay.Global
{
    public class InputHandler : MonoBehaviour
    {
        public event Action LeftClicked;

        /// <summary>
        /// Raised when the right button is released without having travelled. The same button drags
        /// the camera, so a pan must not read as a click - hence the release rather than the press.
        /// </summary>
        public event Action RightClicked;

        /// <summary>Raised when the interact key (E) goes down.</summary>
        public event Action InteractPressed;

        /// <summary>
        /// Raised with the zero-based index of the number key that was pressed (key 1 reports 0).
        /// Raw input only — who cares about which index is a valid target is up to the listener.
        /// </summary>
        public event Action<int> NumberKeyPressed;

        /// <summary>Raised when the confirm key (space) goes down.</summary>
        public event Action ConfirmPressed;

        /// <summary>Raised when the cancel key (escape) goes down.</summary>
        public event Action CancelPressed;

        /// <summary>Raised when the end-turn key (Q) goes down.</summary>
        public event Action EndTurnPressed;

        /// <summary>Keys 1..9. Zero and the numpad are deliberately left alone.</summary>
        private const int NumberKeyCount = 9;

        /// <summary>
        /// How far in screen pixels the cursor may travel with the right button down and still count
        /// as a click rather than as a pan. A few pixels of slip belong to every press.
        /// </summary>
        private const float ClickTravelThreshold = 4f;

        [SerializeField] private float cameraPanSpeed = 10f;
        [SerializeField] private Transform cameraRig;

        private InputAction leftClickAction;
        private InputAction moveAction;
        private InputAction numberKeyAction;
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction interactAction;
        private InputAction endTurnAction;
        private Transform panTarget;
        private Camera cam;

        private bool isDragging;
        private Vector3 dragWorldAnchor;
        private Vector2 dragScreenStart;

        private void Update()
        {
            PanCamera();
            DragPan();
        }

        private void PanCamera()
        {
            if (panTarget == null)
                return;

            Vector2 move = moveAction.ReadValue<Vector2>();

            if (move == Vector2.zero)
                return;

            Vector3 delta = new Vector3(move.x, move.y, 0f) * (cameraPanSpeed * Time.deltaTime);
            panTarget.Translate(delta, Space.World);
        }

        /// <summary>
        /// Pans by dragging with the right mouse button. The world point grabbed when the button
        /// went down is kept under the cursor (grab-the-world). Moves the same rig as WASD, so the
        /// UI overlay camera follows along too.
        ///
        /// Also decides where a press ends up: one that never travelled was meant as a click and is
        /// announced as <see cref="RightClicked"/> on release. Both live here because the drag is
        /// what tells the two apart, and it is tracked from the mouse directly rather than through an
        /// InputAction so the travelled distance is available at the same moment.
        /// </summary>
        private void DragPan()
        {
            if (cam == null || Mouse.current == null)
                return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                dragWorldAnchor = ScreenToWorld(screenPosition);
                dragScreenStart = screenPosition;
                isDragging = true;
            }
            else if (!Mouse.current.rightButton.isPressed)
            {
                if (isDragging && Vector2.Distance(screenPosition, dragScreenStart) <= ClickTravelThreshold)
                    RightClicked?.Invoke();

                isDragging = false;
            }

            if (!isDragging || panTarget == null)
                return;

            Vector3 worldUnderCursor = ScreenToWorld(screenPosition);
            Vector3 delta = dragWorldAnchor - worldUnderCursor;
            delta.z = 0f;
            panTarget.position += delta;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        }

        /// <summary>
        /// Resolves what WASD pans. Defaults to the main camera's parent to move all cameras
        /// at once (also UI-Cam).
        /// </summary>
        private Transform ResolvePanTarget()
        {
            if (cameraRig != null)
                return cameraRig;

            Camera main = Camera.main;
            if (main == null)
                return null;

            return main.transform.parent != null ? main.transform.parent : main.transform;
        }

        private void OnLeftClickPerformed(InputAction.CallbackContext ctx) => LeftClicked?.Invoke();

        private void OnConfirmPerformed(InputAction.CallbackContext ctx) => ConfirmPressed?.Invoke();

        private void OnCancelPerformed(InputAction.CallbackContext ctx) => CancelPressed?.Invoke();

        private void OnInteractPerformed(InputAction.CallbackContext ctx) => InteractPressed?.Invoke();

        private void OnEndTurnPerformed(InputAction.CallbackContext ctx) => EndTurnPressed?.Invoke();

        /// <summary>
        /// The bindings are added in key order, so the binding index of the control that fired is
        /// already the zero-based number key index. The action is pass-through, so it also reports
        /// releases and a second key pressed while the first is still held — hence the press filter.
        /// </summary>
        private void OnNumberKeyPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.ReadValueAsButton())
                return;

            int index = numberKeyAction.GetBindingIndexForControl(ctx.control);

            if (index < 0)
                return;

            NumberKeyPressed?.Invoke(index);
        }

        private void OnEnable()
        {
            cam = Camera.main;
            panTarget = ResolvePanTarget();

            leftClickAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Mouse>/leftButton");

            // The right button has no action of its own: it drags the camera, and whether a press was
            // a pan or a click is only known on release - see DragPan.

            moveAction = new InputAction(type: InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Pass-through instead of Button: a Button action resolves conflicts between its
            // bindings, so pressing 2 while 1 is still held would be swallowed.
            numberKeyAction = new InputAction(type: InputActionType.PassThrough);
            for (int key = 1; key <= NumberKeyCount; key++)
                numberKeyAction.AddBinding($"<Keyboard>/{key}");

            confirmAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Keyboard>/space");

            cancelAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Keyboard>/escape");

            interactAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Keyboard>/e");

            endTurnAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Keyboard>/q");

            leftClickAction.performed += OnLeftClickPerformed;
            numberKeyAction.performed += OnNumberKeyPerformed;
            confirmAction.performed += OnConfirmPerformed;
            cancelAction.performed += OnCancelPerformed;
            interactAction.performed += OnInteractPerformed;
            endTurnAction.performed += OnEndTurnPerformed;

            leftClickAction.Enable();
            moveAction.Enable();
            numberKeyAction.Enable();
            confirmAction.Enable();
            cancelAction.Enable();
            interactAction.Enable();
            endTurnAction.Enable();
        }

        private void OnDisable()
        {
            if (leftClickAction != null)
            {
                leftClickAction.performed -= OnLeftClickPerformed;
                leftClickAction.Disable();
            }

            if (numberKeyAction != null)
            {
                numberKeyAction.performed -= OnNumberKeyPerformed;
                numberKeyAction.Disable();
            }

            if (confirmAction != null)
            {
                confirmAction.performed -= OnConfirmPerformed;
                confirmAction.Disable();
            }

            if (cancelAction != null)
            {
                cancelAction.performed -= OnCancelPerformed;
                cancelAction.Disable();
            }

            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.Disable();
            }

            if (endTurnAction != null)
            {
                endTurnAction.performed -= OnEndTurnPerformed;
                endTurnAction.Disable();
            }

            moveAction?.Disable();
        }
    }
}
