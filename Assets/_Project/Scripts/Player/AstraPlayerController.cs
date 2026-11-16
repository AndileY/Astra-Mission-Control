using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Astra
{
    [RequireComponent(typeof(CharacterController))]
    public class AstraPlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintMultiplier = 1.55f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gamepadLookSpeed = 150f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float cameraDistance = 8.6f;
        [SerializeField] private float cameraSmoothTime = 0.08f;
        [SerializeField] private float cameraCollisionRadius = 0.22f;

        private CharacterController controller;
        private Camera followCamera;
        private AstraGameManager game;
        private float yaw;
        private float pitch = 27f;
        private float verticalSpeed;
        private float moveAmount;
        private bool sprinting;
        private Vector3 cameraVelocity;
        private Vector3 clickMoveTarget;
        private bool hasClickMoveTarget;
        private AstraRoverDrive mountedRover;
        private Renderer[] astronautRenderers;
        private Vector2 touchMoveInput;
        private Vector2 touchLookInput;
        private bool touchJumpQueued;
        private bool touchInteractQueued;
        private bool touchRoverExitQueued;

        public float MoveAmount => moveAmount;
        public bool IsSprinting => sprinting;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public float VerticalSpeed => verticalSpeed;
        public bool IsMountedInRover => mountedRover != null && mountedRover.IsDriving;
        public AstraRoverDrive MountedRover => mountedRover;
        public Vector2 TouchMoveInput => touchMoveInput;

        public void SetTouchMove(Vector2 input) => touchMoveInput = Vector2.ClampMagnitude(input, 1f);
        public void SetTouchLook(Vector2 input) => touchLookInput = Vector2.ClampMagnitude(input, 1f);
        public void QueueTouchJump() => touchJumpQueued = true;
        public void QueueTouchInteract() => touchInteractQueued = true;
        public void QueueTouchRoverExit() => touchRoverExitQueued = true;
        public bool ConsumeTouchInteract() { bool queued = touchInteractQueued; touchInteractQueued = false; return queued; }
        public bool ConsumeTouchRoverExit() { bool queued = touchRoverExitQueued; touchRoverExitQueued = false; return queued; }

        public void RefreshAstronautRenderers()
        {
            astronautRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer item in astronautRenderers)
                if (item != null) item.enabled = !IsMountedInRover;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            astronautRenderers = GetComponentsInChildren<Renderer>(true);
            followCamera = Camera.main;
            game = FindFirstObjectByType<AstraGameManager>();
            if (cameraTarget == null) cameraTarget = transform;
            if (followCamera != null)
            {
                Vector3 euler = followCamera.transform.eulerAngles;
                yaw = euler.y;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            Joystick joystick = Joystick.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame ||
                 gamepad != null && gamepad.startButton.wasPressedThisFrame) && game != null)
                game.TogglePause();

            if (game == null || !game.CanControlPlayer)
            {
                moveAmount = 0f;
                sprinting = false;
                touchMoveInput = Vector2.zero;
                touchLookInput = Vector2.zero;
                touchJumpQueued = touchInteractQueued = touchRoverExitQueued = false;
                return;
            }

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, 8f, 45f);
                if (delta.sqrMagnitude > 1f) TutorialManager.Instance?.RecordLook(delta.magnitude);
            }
            if (gamepad != null)
            {
                Vector2 look = gamepad.rightStick.ReadValue();
                yaw += look.x * gamepadLookSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - look.y * gamepadLookSpeed * Time.deltaTime, 8f, 45f);
                if (look.sqrMagnitude > 0.04f) TutorialManager.Instance?.RecordLook(look.magnitude * gamepadLookSpeed * Time.deltaTime);
            }
            else if (joystick != null && joystick.twist != null)
            {
                yaw += joystick.twist.ReadValue() * gamepadLookSpeed * Time.deltaTime;
            }
            if (touchLookInput.sqrMagnitude > 0.001f)
            {
                yaw += touchLookInput.x * gamepadLookSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - touchLookInput.y * gamepadLookSpeed * Time.deltaTime, 8f, 45f);
            }

            if (IsMountedInRover)
            {
                touchJumpQueued = false;
                transform.position = mountedRover.transform.position;
                moveAmount = 0f;
                sprinting = false;
                return;
            }

            Vector2 move = Vector2.zero;
            PlayerSettingsManager settings = PlayerSettingsManager.Instance;
            if (keyboard != null)
            {
                Key forward = settings != null ? settings.GetKey(AstraBinding.MoveForward) : Key.W;
                Key back = settings != null ? settings.GetKey(AstraBinding.MoveBack) : Key.S;
                Key left = settings != null ? settings.GetKey(AstraBinding.MoveLeft) : Key.A;
                Key right = settings != null ? settings.GetKey(AstraBinding.MoveRight) : Key.D;
                if (keyboard[forward].isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard[back].isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard[right].isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard[left].isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            }
            if (gamepad != null)
                move += gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();
            if (joystick != null)
            {
                move += joystick.stick.ReadValue();
                if (joystick.hatswitch != null) move += joystick.hatswitch.ReadValue();
            }
            move += touchMoveInput;

            if (settings != null && settings.ClickToMove && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Camera clickCamera = followCamera != null ? followCamera : Camera.main;
                if (clickCamera != null)
                {
                    Ray clickRay = clickCamera.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(clickRay, out RaycastHit groundHit, 240f)) { clickMoveTarget = groundHit.point; hasClickMoveTarget = true; }
                }
            }
            if (hasClickMoveTarget && move.sqrMagnitude < 0.001f)
            {
                Vector3 toClick = clickMoveTarget - transform.position;
                toClick.y = 0f;
                if (toClick.magnitude < 0.45f) hasClickMoveTarget = false;
                else move = Vector2.ClampMagnitude(new Vector2(toClick.x, toClick.z), 1f);
            }
            move = Vector2.ClampMagnitude(move, 1f);
            moveAmount = move.magnitude;

            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 direction = facing * new Vector3(move.x, 0f, move.y);
            bool keyboardSprint = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool gamepadSprint = gamepad != null && gamepad.leftShoulder.isPressed;
            sprinting = moveAmount > 0.05f && (keyboardSprint || gamepadSprint);
            float speed = walkSpeed * (sprinting ? sprintMultiplier : 1f);

            if (controller.isGrounded)
            {
                verticalSpeed = -1.5f;
                bool keyboardJump = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
                bool gamepadJump = gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
                bool touchJump = touchJumpQueued;
                touchJumpQueued = false;
                if (keyboardJump || gamepadJump || touchJump)
                    verticalSpeed = Mathf.Sqrt(jumpHeight * 2f * LunarGravity);
            }
            else verticalSpeed -= LunarGravity * Time.deltaTime;

            Vector3 previousPosition = transform.position;
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            TutorialManager.Instance?.RecordMovement(Vector3.Distance(previousPosition, transform.position));
            if (direction.sqrMagnitude > 0.02f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);

            Key interact = settings != null ? settings.GetKey(AstraBinding.Interact) : Key.E;
            bool keyboardInteract = keyboard != null && keyboard[interact].wasPressedThisFrame;
            bool gamepadInteract = gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
            bool joystickInteract = joystick != null && joystick.trigger.wasPressedThisFrame;
            bool touchInteract = ConsumeTouchInteract();
            if (keyboardInteract || gamepadInteract || joystickInteract || touchInteract)
                game.TryInteract(transform.position);

        }

        private const float LunarGravity = 1.62f;

        private void LateUpdate()
        {
            if (game != null && game.CanControlPlayer) UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (followCamera == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = cameraTarget.position + Vector3.up * 1.1f;
            Vector3 offset = rotation * new Vector3(0f, 0f, -cameraDistance);
            Vector3 desiredPosition = focus + offset;
            Vector3 travel = desiredPosition - focus;
            float travelDistance = travel.magnitude;
            if (travelDistance > 0.01f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(focus, cameraCollisionRadius, travel / travelDistance,
                    travelDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearestBlockDistance = travelDistance;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                    if (IsMountedInRover && (hit.transform == mountedRover.transform || hit.transform.IsChildOf(mountedRover.transform))) continue;
                    if (hit.distance < nearestBlockDistance) nearestBlockDistance = hit.distance;
                }
                if (nearestBlockDistance < travelDistance)
                    desiredPosition = focus + travel.normalized * Mathf.Max(1.2f, nearestBlockDistance - 0.12f);
            }
            followCamera.transform.position = Vector3.SmoothDamp(followCamera.transform.position, desiredPosition, ref cameraVelocity, cameraSmoothTime);
            followCamera.transform.rotation = rotation;
            followCamera.transform.LookAt(focus);
        }

        public AstraInteractable GetNearestFacility()
        {
            AstraInteractable[] facilities = FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None);
            AstraInteractable nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (AstraInteractable facility in facilities)
            {
                Collider facilityCollider = facility.GetComponent<Collider>();
                Vector3 point = facilityCollider != null ? facilityCollider.ClosestPoint(transform.position + Vector3.up * 1.1f) : facility.transform.position;
                float distance = Vector3.Distance(transform.position, point);
                if (distance <= facility.interactionRange && distance < nearestDistance)
                {
                    // Use proximity instead of line-of-sight. Large imported dish,
                    // rover, and habitat meshes often sit between the astronaut and
                    // their root interaction collider; ray blocking made nearby
                    // mission equipment report as unusable even when its prompt was shown.
                    nearest = facility;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        public void ResetForMission()
        {
            if (mountedRover != null) mountedRover.ForceEndDrive();
            hasClickMoveTarget = false;
            verticalSpeed = 0f;
            controller.enabled = false;
            transform.position = new Vector3(0f, 0.05f, -1.5f);
            transform.rotation = Quaternion.identity;
            controller.enabled = true;
            cameraVelocity = Vector3.zero;
            yaw = 0f;
            pitch = 27f;
            touchMoveInput = touchLookInput = Vector2.zero;
            touchJumpQueued = touchInteractQueued = touchRoverExitQueued = false;
        }

        public void MountRover(AstraRoverDrive rover)
        {
            if (rover == null) return;
            mountedRover = rover;
            controller.enabled = false;
            transform.position = rover.transform.position;
            transform.rotation = rover.transform.rotation;
            yaw = rover.transform.eulerAngles.y;
            foreach (Renderer item in astronautRenderers)
                if (item != null) item.enabled = false;
            moveAmount = 0f;
            sprinting = false;
        }

        public void DismountRover(AstraRoverDrive rover)
        {
            if (mountedRover != rover) return;
            transform.position = rover.transform.position - rover.transform.forward * 2.3f;
            transform.rotation = rover.transform.rotation;
            mountedRover = null;
            controller.enabled = true;
            foreach (Renderer item in astronautRenderers)
                if (item != null) item.enabled = true;
            verticalSpeed = 0f;
        }
    }
}
