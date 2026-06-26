using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Gameplay.Global
{
    /// <summary>
    /// Single entry point for raw user input. Translates Input System actions into semantic
    /// events that other systems subscribe to (e.g. <see cref="Raycaster"/> listens for
    /// <see cref="LeftClicked"/>), and pans the main camera from the WASD keys each frame.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        /// <summary>Raised when the left mouse button is pressed.</summary>
        public event Action LeftClicked;

        /// <summary>Raised when the right mouse button is pressed.</summary>
        public event Action RightClicked;

        [SerializeField] private float cameraPanSpeed = 10f;

        private InputAction leftClickAction;
        private InputAction rightClickAction;
        private InputAction moveAction;
        private Camera cam;

        private void Update()
        {
            PanCamera();
        }

        private void PanCamera()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();

            if (move == Vector2.zero)
                return;

            Vector3 delta = new Vector3(move.x, move.y, 0f) * (cameraPanSpeed * Time.deltaTime);
            cam.transform.Translate(delta, Space.World);
        }

        private void OnLeftClickPerformed(InputAction.CallbackContext ctx) => LeftClicked?.Invoke();

        private void OnRightClickPerformed(InputAction.CallbackContext ctx) => RightClicked?.Invoke();

        private void OnEnable()
        {
            cam = Camera.main;

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
