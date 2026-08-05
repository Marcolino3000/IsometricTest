using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Gameplay.Global
{
    public class InputHandler : MonoBehaviour
    {
        public event Action LeftClicked;

        public event Action RightClicked;

        /// <summary>
        /// Raised with the zero-based index of the number key that was pressed (key 1 reports 0).
        /// Raw input only — who cares about which index is a valid target is up to the listener.
        /// </summary>
        public event Action<int> NumberKeyPressed;

        /// <summary>Keys 1..9. Zero and the numpad are deliberately left alone.</summary>
        private const int NumberKeyCount = 9;

        [SerializeField] private float cameraPanSpeed = 10f;
        [SerializeField] private Transform cameraRig;

        private InputAction leftClickAction;
        private InputAction rightClickAction;
        private InputAction moveAction;
        private InputAction numberKeyAction;
        private Transform panTarget;
        private Camera cam;

        private bool isDragging;
        private Vector3 dragWorldAnchor;

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
        /// </summary>
        private void DragPan()
        {
            if (panTarget == null || cam == null || Mouse.current == null)
                return;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                dragWorldAnchor = ScreenToWorld(Mouse.current.position.ReadValue());
                isDragging = true;
            }
            else if (!Mouse.current.rightButton.isPressed)
            {
                isDragging = false;
            }

            if (!isDragging)
                return;

            Vector3 worldUnderCursor = ScreenToWorld(Mouse.current.position.ReadValue());
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

        private void OnRightClickPerformed(InputAction.CallbackContext ctx) => RightClicked?.Invoke();

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

            rightClickAction = new InputAction(
                type: InputActionType.Button,
                binding: "<Mouse>/rightButton");

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

            leftClickAction.performed += OnLeftClickPerformed;
            rightClickAction.performed += OnRightClickPerformed;
            numberKeyAction.performed += OnNumberKeyPerformed;

            leftClickAction.Enable();
            rightClickAction.Enable();
            moveAction.Enable();
            numberKeyAction.Enable();
        }

        private void OnDisable()
        {
            if (leftClickAction != null)
            {
                leftClickAction.performed -= OnLeftClickPerformed;
                leftClickAction.Disable();
            }

            if (rightClickAction != null)
            {
                rightClickAction.performed -= OnRightClickPerformed;
                rightClickAction.Disable();
            }

            if (numberKeyAction != null)
            {
                numberKeyAction.performed -= OnNumberKeyPerformed;
                numberKeyAction.Disable();
            }

            moveAction?.Disable();
        }
    }
}
