using System;
using System.Collections.Generic;
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

        /// <summary>
        /// The right button went down, with the screen point it went down at. What a drag means in
        /// the world is <see cref="CameraController"/>'s: this reports pointer facts in screen
        /// pixels and never touches a camera.
        /// </summary>
        public event Action<Vector2> PanDragStarted;

        /// <summary>The cursor moved with the right button held, at this screen point.</summary>
        public event Action<Vector2> PanDragMoved;

        /// <summary>The right button came up.</summary>
        public event Action PanDragEnded;

        /// <summary>Raised with the scroll wheel's notch: positive zooms in.</summary>
        public event Action<float> ZoomChanged;

        /// <summary>
        /// The pan axis as it stands - WASD, read rather than raised because it is a held direction
        /// rather than an event. Polled once a frame by whoever moves the view.
        /// </summary>
        public Vector2 PanAxis => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>Keys 1..9. Zero and the numpad are deliberately left alone.</summary>
        private const int NumberKeyCount = 9;

        /// <summary>
        /// How far in screen pixels the cursor may travel with the right button down and still count
        /// as a click rather than as a pan. A few pixels of slip belong to every press.
        /// </summary>
        private const float ClickTravelThreshold = 4f;

        /// <summary>
        /// Whatever currently stands in front of the game — the find popup is the only one today.
        /// A list rather than a flag so nothing has to be unset: see <see cref="IInputBlocker"/>.
        /// </summary>
        private readonly List<IInputBlocker> blockers = new();

        private InputAction leftClickAction;
        private InputAction moveAction;
        private InputAction numberKeyAction;
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction interactAction;
        private InputAction endTurnAction;
        private InputAction zoomAction;

        private bool isDragging;
        private Vector2 dragScreenStart;

        /// <summary>
        /// Whether the right button went down while something was blocking. A right click is only
        /// known to be one on release, by which time the card it dismissed is gone — so what the
        /// press was worth has to be remembered from the press.
        /// </summary>
        private bool dragBlocked;

        /// <summary>
        /// True while something in front of the game is swallowing input. Nothing below is announced
        /// then, so the press that puts a card away does nothing else.
        /// </summary>
        public bool Blocked
        {
            get
            {
                for (int i = 0; i < blockers.Count; i++)
                {
                    if (blockers[i] != null && blockers[i].BlocksInput)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Registers a view that swallows input while it is up.</summary>
        public void AddBlocker(IInputBlocker blocker)
        {
            if (blocker != null && !blockers.Contains(blocker))
                blockers.Add(blocker);
        }

        private void Update()
        {
            TrackRightButton();
            ReadZoom();
        }

        /// <summary>
        /// Tells a pan from a right click and announces whichever it was. Both live here because the
        /// travelled distance is what separates them: a press that never moved was meant as a click
        /// and is announced on release, one that moved is a drag and is handed to whoever owns the
        /// view. Tracked from the mouse directly rather than through an InputAction, so the distance
        /// is available at the same moment the button state is.
        /// </summary>
        private void TrackRightButton()
        {
            if (Mouse.current == null)
                return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                dragScreenStart = screenPosition;
                isDragging = true;
                dragBlocked = Blocked;

                PanDragStarted?.Invoke(screenPosition);
            }
            else if (!Mouse.current.rightButton.isPressed)
            {
                if (isDragging)
                {
                    if (!dragBlocked && Vector2.Distance(screenPosition, dragScreenStart) <= ClickTravelThreshold)
                        RightClicked?.Invoke();

                    PanDragEnded?.Invoke();
                }

                isDragging = false;
                dragBlocked = false;
                return;
            }

            if (isDragging)
                PanDragMoved?.Invoke(screenPosition);
        }

        private void ReadZoom()
        {
            if (zoomAction == null)
                return;

            var scroll = zoomAction.ReadValue<Vector2>().y;

            if (!Mathf.Approximately(scroll, 0f))
                ZoomChanged?.Invoke(Mathf.Sign(scroll));
        }

        /// <summary>
        /// Announces <paramref name="input"/> unless something in front of the game swallows it.
        /// Every event goes through here, so a card that blocks blocks all of them at once — the
        /// pan and the zoom are the exception and are left alone, since moving the view is nothing
        /// the game answers.
        /// </summary>
        private void Raise(Action input)
        {
            if (!Blocked)
                input?.Invoke();
        }

        private void OnLeftClickPerformed(InputAction.CallbackContext ctx) => Raise(LeftClicked);

        private void OnConfirmPerformed(InputAction.CallbackContext ctx) => Raise(ConfirmPressed);

        private void OnCancelPerformed(InputAction.CallbackContext ctx) => Raise(CancelPressed);

        private void OnInteractPerformed(InputAction.CallbackContext ctx) => Raise(InteractPressed);

        private void OnEndTurnPerformed(InputAction.CallbackContext ctx) => Raise(EndTurnPressed);

        /// <summary>
        /// The bindings are added in key order, so the binding index of the control that fired is
        /// already the zero-based number key index. The action is pass-through, so it also reports
        /// releases and a second key pressed while the first is still held — hence the press filter.
        /// </summary>
        private void OnNumberKeyPerformed(InputAction.CallbackContext ctx)
        {
            if (Blocked || !ctx.ReadValueAsButton())
                return;

            int index = numberKeyAction.GetBindingIndexForControl(ctx.control);

            if (index < 0)
                return;

            NumberKeyPressed?.Invoke(index);
        }

        private void OnEnable()
        {
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

            zoomAction = new InputAction(
                type: InputActionType.Value,
                binding: "<Mouse>/scroll");

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
            zoomAction.Enable();
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
            zoomAction?.Disable();
        }
    }
}
