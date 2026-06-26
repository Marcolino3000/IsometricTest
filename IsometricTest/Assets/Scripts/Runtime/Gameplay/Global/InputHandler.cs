using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Gameplay.Global
{
    public class InputHandler : MonoBehaviour
    {
        public event Action LeftClicked;

        public event Action RightClicked;

        [SerializeField] private float cameraPanSpeed = 10f;
        [SerializeField] private Transform cameraRig;

        private InputAction leftClickAction;
        private InputAction rightClickAction;
        private InputAction moveAction;
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

            leftClickAction.performed += OnLeftClickPerformed;
            rightClickAction.performed += OnRightClickPerformed;

            leftClickAction.Enable();
            rightClickAction.Enable();
            moveAction.Enable();
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

            moveAction?.Disable();
        }
    }
}
