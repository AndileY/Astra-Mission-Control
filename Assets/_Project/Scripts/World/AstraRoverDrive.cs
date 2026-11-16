using UnityEngine;
using UnityEngine.InputSystem;

namespace Astra
{
    /// <summary>Drives the outpost rover between its parking point and a nearby virtual sample site.</summary>
    public sealed class AstraRoverDrive : MonoBehaviour
    {
        [SerializeField] private float driveSpeed = 3.5f;
        [SerializeField] private float steeringSpeed = 95f;
        [SerializeField] private float sampleRadius = 2.4f;

        private AstraGameManager game;
        private AstraPlayerController player;
        private Vector3 homePosition;
        private Vector3 defaultSamplePosition;
        private Transform sampleSite;
        private Transform sampleBeacon;
        private Transform sampleCore;
        private Light sampleBeaconLight;
        private float sampleBeaconBaseY;
        private Transform nasaVehicleModel;
        private Vector3 nasaVehicleRestPosition;
        private Quaternion nasaVehicleRestRotation;
        private Vector3 lastVehiclePosition;
        private bool nasaVehicleRestCaptured;
        private bool sampleTaken;
        private float scheduledProgress;
        private float scheduledDirection = 1f;

        public bool IsDriving { get; private set; }
        public bool ScheduledTraverseActive { get; private set; }
        public Transform SampleSite => sampleSite;
        public float DistanceToSample => sampleSite != null
            ? Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(sampleSite.position.x, 0f, sampleSite.position.z))
            : float.MaxValue;
        public bool IsAtSample => DistanceToSample <= sampleRadius;

        private void Awake()
        {
            game = FindAnyObjectByType<AstraGameManager>();
            player = FindAnyObjectByType<AstraPlayerController>();
            homePosition = transform.position;
            lastVehiclePosition = transform.position;
            CreateSampleSite();
        }

        private void Update()
        {
            AnimateSampleSite();
            AnimateImportedVehicle();
            if (ScheduledTraverseActive && game != null && game.missionStarted && !game.missionFinished && !game.missionPaused && !game.EventOpen)
                UpdateScheduledTraverse();
            if (!IsDriving || game == null || !game.CanControlPlayer) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Vector2 input = Vector2.zero;
            PlayerSettingsManager settings = PlayerSettingsManager.Instance;
            if (keyboard != null)
            {
                Key forward = settings != null ? settings.GetKey(AstraBinding.MoveForward) : Key.W;
                Key back = settings != null ? settings.GetKey(AstraBinding.MoveBack) : Key.S;
                Key left = settings != null ? settings.GetKey(AstraBinding.MoveLeft) : Key.A;
                Key right = settings != null ? settings.GetKey(AstraBinding.MoveRight) : Key.D;
                if (keyboard[forward].isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard[back].isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard[right].isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard[left].isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }
            if (gamepad != null) input += gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();
            if (player != null) input += player.TouchMoveInput;
            input = Vector2.ClampMagnitude(input, 1f);

            transform.Rotate(Vector3.up, input.x * steeringSpeed * Time.deltaTime, Space.World);
            float travel = input.y * driveSpeed * Time.deltaTime;
            Vector3 nextPosition = transform.position + transform.forward * travel;
            if (Vector3.Distance(new Vector3(homePosition.x, 0f, homePosition.z), new Vector3(nextPosition.x, 0f, nextPosition.z)) <= 34f)
            {
                transform.position = nextPosition;
                SnapToTerrain();
            }

            Key interactKey = settings != null ? settings.GetKey(AstraBinding.Interact) : Key.E;
            bool keyboardCollect = keyboard != null && keyboard[interactKey].wasPressedThisFrame;
            bool gamepadCollect = gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
            bool touchCollect = player != null && player.ConsumeTouchInteract();
            bool collectPressed = keyboardCollect || gamepadCollect || touchCollect;
            bool keyboardExit = keyboard != null && keyboard.qKey.wasPressedThisFrame;
            bool gamepadExit = gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            bool touchExit = player != null && player.ConsumeTouchRoverExit();
            bool exitPressed = keyboardExit || gamepadExit || touchExit;
            if (collectPressed)
            {
                if (IsAtSample) CollectSample();
                else game.ShowMessage("Follow the surface-scan waypoint on your mission HUD. " + Mathf.CeilToInt(DistanceToSample) + " m to go.");
            }
            if (exitPressed) EndDrive();
        }

        private void AnimateImportedVehicle()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = Vector3.Distance(transform.position, lastVehiclePosition) / dt;
            lastVehiclePosition = transform.position;
            if (nasaVehicleModel == null)
                nasaVehicleModel = transform.Find("ASTRA NASA hardware/NASA SpaceExplorationVehicle");
            if (nasaVehicleModel == null) return;
            if (!nasaVehicleRestCaptured)
            {
                nasaVehicleRestPosition = nasaVehicleModel.localPosition;
                nasaVehicleRestRotation = nasaVehicleModel.localRotation;
                nasaVehicleRestCaptured = true;
            }
            // The NASA SEV file is a single complete vehicle mesh, without separate
            // wheel joints. Give the actual imported vehicle a restrained suspension
            // response while it traverses the terrain; do not spin hidden blockout wheels.
            float moving = Mathf.Clamp01(speed / Mathf.Max(0.1f, driveSpeed));
            float bounce = Mathf.Sin(Time.time * 8.5f) * 0.025f * moving;
            float pitch = Mathf.Sin(Time.time * 8.5f) * 1.1f * moving;
            nasaVehicleModel.localRotation = Quaternion.Slerp(nasaVehicleModel.localRotation,
                nasaVehicleRestRotation * Quaternion.Euler(pitch, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
            Vector3 position = nasaVehicleModel.localPosition;
            position.y = Mathf.Lerp(position.y, nasaVehicleRestPosition.y + bounce, 1f - Mathf.Exp(-8f * dt));
            nasaVehicleModel.localPosition = position;
        }

        public void BeginDrive()
        {
            if (IsDriving || sampleTaken || sampleSite == null) return;
            ScheduledTraverseActive = false;
            IsDriving = true;
            player = FindAnyObjectByType<AstraPlayerController>();
            player?.MountRover(this);
            VisualHintManager.Instance?.SetTarget(sampleSite);
        }

        public void EndDrive()
        {
            if (!IsDriving) return;
            IsDriving = false;
            player?.DismountRover(this);
            VisualHintManager.Instance?.RefreshMissionTarget(game);
        }

        public void ForceEndDrive() => EndDrive();

        public void BeginScheduledTraverse()
        {
            if (sampleSite == null) return;
            ScheduledTraverseActive = true;
            scheduledProgress = 0f;
            scheduledDirection = 1f;
        }

        public void SetEuropaSurveyRoute(int route)
        {
            if (sampleSite == null) return;
            Vector3 offset = route == 0 ? new Vector3(14f, 0f, -11f) : new Vector3(8f, 0f, -15f);
            sampleSite.position = GetGroundedPosition(homePosition + offset);
        }

        public void SuspendScheduledTraverse()
        {
            ScheduledTraverseActive = false;
        }

        public void ResetForMission()
        {
            EndDrive();
            ScheduledTraverseActive = false;
            scheduledProgress = 0f;
            scheduledDirection = 1f;
            transform.position = homePosition;
            transform.rotation = Quaternion.identity;
            if (sampleSite != null) sampleSite.position = GetGroundedPosition(defaultSamplePosition);
            sampleTaken = false;
            if (sampleCore != null) sampleCore.gameObject.SetActive(true);
            if (sampleBeaconLight != null) sampleBeaconLight.color = new Color(1f, 0.57f, 0.12f);
        }

        private void CollectSample()
        {
            if (sampleTaken || !IsAtSample) return;
            sampleTaken = true;
            if (sampleCore != null) sampleCore.gameObject.SetActive(false);
            if (sampleBeaconLight != null) sampleBeaconLight.color = new Color(0.20f, 0.95f, 0.58f);
            EndDrive();
            game.CollectRoverSample(this);
        }

        private void CreateSampleSite()
        {
            if (sampleSite != null) return;
            Vector3 sitePosition = GetGroundedPosition(homePosition + new Vector3(8f, 0f, -10f));
            defaultSamplePosition = sitePosition;
            GameObject site = new GameObject("Visible virtual surface sample site");
            sampleSite = site.transform;
            sampleSite.position = sitePosition;

            // The scan waypoint is deliberately a world object: players can spot it
            // from the rover and drive directly to the terrain feature it marks.
            CreateSamplePiece(PrimitiveType.Sphere, "Sample-site regolith outcrop", sampleSite,
                new Vector3(0f, 0.32f, 0f), new Vector3(1.25f, 0.60f, 0.95f), new Color(0.40f, 0.39f, 0.37f), Color.black);
            CreateSamplePiece(PrimitiveType.Sphere, "Sample-site rock fragment", sampleSite,
                new Vector3(-0.78f, 0.19f, 0.26f), new Vector3(0.40f, 0.28f, 0.34f), new Color(0.54f, 0.50f, 0.43f), Color.black);
            sampleCore = CreateSamplePiece(PrimitiveType.Cylinder, "Virtual sample core", sampleSite,
                new Vector3(0.08f, 0.49f, -0.10f), new Vector3(0.20f, 0.16f, 0.20f), new Color(1f, 0.62f, 0.12f), new Color(0.62f, 0.20f, 0.015f)).transform;

            GameObject marker = new GameObject("Amber rover waypoint beacon");
            marker.transform.SetParent(sampleSite, false);
            marker.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            sampleBeacon = marker.transform;
            sampleBeaconBaseY = marker.transform.localPosition.y;
            CreateSamplePiece(PrimitiveType.Cylinder, "Waypoint mast", marker.transform,
                new Vector3(0f, -0.48f, 0f), new Vector3(0.07f, 0.48f, 0.07f), new Color(0.74f, 0.78f, 0.79f), Color.black);
            CreateSamplePiece(PrimitiveType.Sphere, "Waypoint lamp", marker.transform,
                Vector3.zero, Vector3.one * 0.25f, new Color(1f, 0.57f, 0.12f), new Color(0.85f, 0.28f, 0.02f));
            sampleBeaconLight = marker.AddComponent<Light>();
            sampleBeaconLight.type = LightType.Point;
            sampleBeaconLight.color = new Color(1f, 0.57f, 0.12f);
            sampleBeaconLight.range = 7f;
            sampleBeaconLight.intensity = 1.8f;

            GameObject label = new GameObject("Sample-site 3D label");
            label.transform.SetParent(sampleSite, false);
            label.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "VIRTUAL SAMPLE SITE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 54;
            text.characterSize = 0.075f;
            text.color = new Color(1f, 0.82f, 0.43f);

            SphereCollider siteCollider = site.AddComponent<SphereCollider>();
            siteCollider.center = new Vector3(0f, 0.30f, 0f);
            siteCollider.radius = 1.1f;
            siteCollider.isTrigger = true;
        }

        private void AnimateSampleSite()
        {
            if (sampleBeacon == null || sampleTaken) return;
            float pulse = (Mathf.Sin(Time.time * 2.3f) + 1f) * 0.5f;
            Vector3 position = sampleBeacon.localPosition;
            position.y = sampleBeaconBaseY + pulse * 0.14f;
            sampleBeacon.localPosition = position;
            float glow = Mathf.Lerp(1.0f, 2.2f, pulse);
            sampleBeaconLight.intensity = glow;
        }

        private Vector3 GetGroundedPosition(Vector3 position)
        {
            GameObject terrain = GameObject.Find("Regolith terrain mesh");
            MeshCollider collider = terrain != null ? terrain.GetComponent<MeshCollider>() : null;
            if (collider != null)
            {
                Ray ray = new Ray(position + Vector3.up * 100f, Vector3.down);
                if (collider.Raycast(ray, out RaycastHit hit, 200f)) position.y = hit.point.y + 0.04f;
            }
            return position;
        }

        private static GameObject CreateSamplePiece(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color, Color emission)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            Renderer renderer = piece.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (renderer != null && shader != null)
            {
                Material material = new Material(shader) { name = "ASTRA sample-site " + name, color = color };
                if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
                renderer.sharedMaterial = material;
            }
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            return piece;
        }

        private void SnapToTerrain()
        {
            GameObject terrain = GameObject.Find("Regolith terrain mesh");
            MeshCollider collider = terrain != null ? terrain.GetComponent<MeshCollider>() : null;
            if (collider == null) return;
            Ray ray = new Ray(transform.position + Vector3.up * 12f, Vector3.down);
            if (collider.Raycast(ray, out RaycastHit hit, 30f))
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        private void UpdateScheduledTraverse()
        {
            const float routeLength = 8f;
            const float speed = 0.8f;
            scheduledProgress += scheduledDirection * speed * Time.deltaTime;
            if (scheduledProgress >= routeLength) { scheduledProgress = routeLength; scheduledDirection = -1f; }
            else if (scheduledProgress <= 0f) { scheduledProgress = 0f; scheduledDirection = 1f; }
            Vector3 next = homePosition + Vector3.right * scheduledProgress;
            transform.position = next;
            SnapToTerrain();
        }
    }
}
