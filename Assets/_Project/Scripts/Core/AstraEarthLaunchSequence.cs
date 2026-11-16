using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Astra
{
    /// <summary>
    /// First-play Earth-to-Moon learning flight. It runs before First Light starts, so
    /// the mission clock and survival resources do not tick during the journey.
    /// </summary>
    public sealed class AstraEarthLaunchSequence : MonoBehaviour
    {
        private const string AtmosphereSource = "https://science.nasa.gov/earth/earth-atmosphere/earths-atmosphere-a-multi-layered-cake/";
        private const string ArtemisSource = "https://www.nasa.gov/learning-resources/for-kids-and-students/what-is-the-space-launch-system-grades-k-4/";
        private const string MoonSource = "https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/moon/";

        public static AstraEarthLaunchSequence Instance { get; private set; }
        public bool IsRunning { get; private set; }
        public bool AllowPlayerControl => IsRunning && !inFlight && !touchdown;

        private AstraGameManager game;
        private AstraHUD hud;
        private AstraPlayerController player;
        private GameObject lunarEnvironment;
        private GameObject lunarOutpost;
        private bool lunarEnvironmentWasActive;
        private bool lunarOutpostWasActive;
        private GameObject earthSite;
        private GameObject rocketRoot;
        private GameObject boosterGroup;
        private GameObject exhaust;
        private GameObject earthGlobe;
        private GameObject moonGlobe;
        private GameObject landerRoot;
        private AstraRuntimeModelLoader nasaModelLoader;
        private Transform astronautModel;
        private Light sun;
        private LaunchGuide guide;
        private Action missionStart;
        private Action launchCancelled;
        private Coroutine flightRoutine;
        private Camera mainCamera;
        private bool inFlight;
        private bool touchdown;
        private bool missionStartCalled;
        private bool savedCamera;
        private CameraClearFlags previousClearFlags;
        private Color previousBackground;
        private float previousFarClip;
        private bool previousFog;
        private Color previousFogColor;
        private FogMode previousFogMode;
        private float previousFogDensity;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private float previousAmbientIntensity;
        private Material previousSkybox;
        private Quaternion previousSunRotation;
        private Color previousSunColor;
        private float previousSunIntensity;
        private readonly List<Material> generatedMaterials = new List<Material>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Begin(Action onMissionStart, Action onLaunchCancelled = null)
        {
            if (IsRunning) return;
            IsRunning = true;
            inFlight = false;
            touchdown = false;
            missionStartCalled = false;
            missionStart = onMissionStart;
            launchCancelled = onLaunchCancelled;
            game = AstraGameManager.Instance;
            hud = FindFirstObjectByType<AstraHUD>();
            player = FindFirstObjectByType<AstraPlayerController>();
            mainCamera = Camera.main;
            nasaModelLoader = AstraRuntimeModelLoader.Instance;
            if (nasaModelLoader == null)
            {
                nasaModelLoader = GetComponent<AstraRuntimeModelLoader>();
                if (nasaModelLoader == null) nasaModelLoader = gameObject.AddComponent<AstraRuntimeModelLoader>();
            }

            lunarEnvironment = GameObject.Find("LUNAR ENVIRONMENT");
            lunarOutpost = GameObject.Find("SHACKLETON OUTPOST");
            lunarEnvironmentWasActive = lunarEnvironment != null && lunarEnvironment.activeSelf;
            lunarOutpostWasActive = lunarOutpost != null && lunarOutpost.activeSelf;
            if (lunarEnvironment != null) lunarEnvironment.SetActive(false);
            if (lunarOutpost != null) lunarOutpost.SetActive(false);

            SaveAndSetEarthLighting();
            BuildEarthLaunchSite();
            guide = new LaunchGuide(SkipFlight, BeginMissionNow, CancelLaunch);
            guide.Show("EARTH / CREW PREPARATION", "YOUR JOURNEY STARTS HERE",
                "You are at the crew preparation base in Florida. Walk your astronaut through the 3D scene, along the bright path to the rocket on the launch pad. When you are close, use the interact control to board. NASA reference buttons below open websites; they do not move your astronaut.",
                "WALK TO ROCKET: WASD / LEFT STICK     BOARD WHEN CLOSE: E / X / JOYSTICK TRIGGER",
                false, false);

            if (player != null)
            {
                player.enabled = true;
                player.ResetForMission();
                player.transform.position = new Vector3(0f, 0.05f, -8f);
                astronautModel = player.transform.Find("Suit model");
            }
        }

        /// <summary>Called by the normal interact input before mission gameplay begins.</summary>
        public bool TryInteract(Vector3 playerPosition)
        {
            if (!IsRunning) return false;
            if (inFlight || touchdown) return true;

            Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 rocketXZ = rocketRoot != null
                ? new Vector2(rocketRoot.transform.position.x, rocketRoot.transform.position.z)
                : Vector2.zero;
            if (Vector2.Distance(playerXZ, rocketXZ) > 6.25f)
            {
                guide?.Show("EARTH / CREW PREPARATION", "WALK TO THE ROCKET",
                    "In the 3D scene, follow the bright path from the crew preparation building to the rocket. NASA reference buttons open websites. You can take your time.",
                    "WALK TO ROCKET: WASD / LEFT STICK     BOARD WHEN CLOSE: E / X / JOYSTICK TRIGGER",
                    false, false);
                return true;
            }

            StartFlight();
            return true;
        }

        private void Update()
        {
            if (!IsRunning) return;
            if (inFlight)
            {
                UpdateFlightCamera();
                if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame ||
                    Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                    SkipFlight();
            }
            else if (touchdown)
            {
                bool keyboardContinue = Keyboard.current != null &&
                    (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
                bool padContinue = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
                if (keyboardContinue || padContinue) BeginMissionNow();
            }
        }

        private void BuildEarthLaunchSite()
        {
            earthSite = new GameObject("ASTRA EARTH LAUNCH BASE - TRAINING SCENE");
            Material grass = MakeMaterial(new Color(0.17f, 0.27f, 0.20f));
            Material concrete = MakeMaterial(new Color(0.31f, 0.37f, 0.41f));
            Material white = MakeMaterial(new Color(0.84f, 0.87f, 0.86f));
            Material dark = MakeMaterial(new Color(0.055f, 0.09f, 0.12f));
            Material blue = MakeMaterial(new Color(0.08f, 0.32f, 0.55f));
            Material orange = MakeMaterial(new Color(0.87f, 0.34f, 0.08f));
            Material cloud = MakeMaterial(new Color(0.91f, 0.95f, 0.97f));

            CreateBlock("Launch-site ground", earthSite.transform, new Vector3(0f, -0.30f, 0f), new Vector3(260f, 0.5f, 260f), grass);
            CreateBlock("Launch apron", earthSite.transform, new Vector3(0f, -0.02f, 8f), new Vector3(28f, 0.16f, 46f), concrete);
            CreateBlock("Crew walk path", earthSite.transform, new Vector3(0f, 0.08f, -1f), new Vector3(3f, 0.1f, 26f), white);
            for (int i = 0; i < 6; i++)
                CreateBlock("Path light", earthSite.transform, new Vector3(i % 2 == 0 ? -2.1f : 2.1f, 0.3f, -10f + i * 4f), new Vector3(0.22f, 0.5f, 0.22f), blue);

            CreateCrewPreparationBuilding(white, blue, dark);
            CreateRocket(white, dark, orange, blue);
            CreateCloudDeck(cloud);
            CreateDistantEarthAndMoon(blue, white);

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0.36f, 0.64f, 0.87f);
                mainCamera.farClipPlane = 3000f;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.49f, 0.72f, 0.86f);
            RenderSettings.fogDensity = 0.0018f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.61f, 0.68f);
            RenderSettings.ambientIntensity = 0.8f;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(34f, -28f, 0f);
                sun.color = new Color(1f, 0.93f, 0.79f);
                sun.intensity = 1.25f;
            }
        }

        private void CreateCrewPreparationBuilding(Material white, Material blue, Material dark)
        {
            Transform root = earthSite.transform;
            CreateBlock("Crew preparation center", root, new Vector3(-13f, 3.1f, -5f), new Vector3(12f, 6.2f, 11f), white);
            CreateBlock("Preparation center roof", root, new Vector3(-13f, 6.35f, -5f), new Vector3(12.8f, 0.35f, 11.8f), dark);
            CreateBlock("Entrance canopy", root, new Vector3(-9.3f, 3.5f, 1.1f), new Vector3(4.5f, 0.25f, 2f), blue);
            CreateBlock("Preparation window", root, new Vector3(-6.88f, 4f, -5f), new Vector3(0.06f, 2.8f, 6.5f), blue);
            CreateBlock("Preparation door", root, new Vector3(-6.82f, 1.4f, -1.2f), new Vector3(0.12f, 2.8f, 2f), dark);
            CreateBlock("Crew center sign", root, new Vector3(-7f, 6.9f, -5f), new Vector3(0.18f, 1.2f, 7.2f), blue);
            CreateWorldLabel("ASTRONAUT CREW PREP", new Vector3(-6.75f, 7f, -5f), 0.22f, Color.white);

            for (int i = 0; i < 4; i++)
                CreateBlock("Launch mast", root, new Vector3(5.7f, 7f, 11f + i * 4.5f), new Vector3(0.42f, 14f, 0.42f), dark);
            for (int y = 1; y < 14; y += 2)
                CreateBlock("Launch mast crossbar", root, new Vector3(5.7f, y, 17.5f), new Vector3(0.5f, 0.28f, 15f), blue);
            CreateBlock("Service bridge", root, new Vector3(3.3f, 7f, 16f), new Vector3(4.4f, 0.45f, 1.2f), dark);
        }

        private void CreateRocket(Material white, Material dark, Material orange, Material blue)
        {
            rocketRoot = new GameObject("ASTRA Simplified Lunar Rocket");
            rocketRoot.transform.SetParent(earthSite.transform, false);
            rocketRoot.transform.position = new Vector3(0f, 0.05f, 17f);
            CreatePrimitive(PrimitiveType.Cylinder, "Core stage", rocketRoot.transform, new Vector3(0f, 6.0f, 0f), new Vector3(1.55f, 6.0f, 1.55f), orange);
            CreatePrimitive(PrimitiveType.Sphere, "Crew capsule", rocketRoot.transform, new Vector3(0f, 12.7f, 0f), new Vector3(1.58f, 1.55f, 1.58f), white);
            CreatePrimitive(PrimitiveType.Cylinder, "Upper stage", rocketRoot.transform, new Vector3(0f, 10.6f, 0f), new Vector3(1.16f, 1.25f, 1.16f), white);

            boosterGroup = new GameObject("Separable solid boosters");
            boosterGroup.transform.SetParent(rocketRoot.transform, false);
            CreatePrimitive(PrimitiveType.Cylinder, "Left booster", boosterGroup.transform, new Vector3(-1.68f, 4.4f, 0f), new Vector3(0.42f, 4.4f, 0.42f), white);
            CreatePrimitive(PrimitiveType.Cylinder, "Right booster", boosterGroup.transform, new Vector3(1.68f, 4.4f, 0f), new Vector3(0.42f, 4.4f, 0.42f), white);
            CreatePrimitive(PrimitiveType.Cylinder, "Left engine bell", rocketRoot.transform, new Vector3(-0.55f, -0.35f, 0f), new Vector3(0.38f, 0.62f, 0.38f), dark);
            CreatePrimitive(PrimitiveType.Cylinder, "Right engine bell", rocketRoot.transform, new Vector3(0.55f, -0.35f, 0f), new Vector3(0.38f, 0.62f, 0.38f), dark);
            CreatePrimitive(PrimitiveType.Cube, "Crew window", rocketRoot.transform, new Vector3(0f, 11.9f, 0.78f), new Vector3(0.64f, 0.48f, 0.12f), blue);

            GameObject exhaustRoot = new GameObject("Engine plume particles");
            exhaustRoot.transform.SetParent(rocketRoot.transform, false);
            exhaustRoot.transform.localPosition = new Vector3(0f, -2.4f, 0f);
            ParticleSystem plumeSystem = exhaustRoot.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule plumeMain = plumeSystem.main;
            plumeMain.loop = true;
            plumeMain.startLifetime = 0.55f;
            plumeMain.startSpeed = 10f;
            plumeMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            plumeMain.startColor = new Color(1f, 0.48f, 0.12f, 0.78f);
            ParticleSystem.EmissionModule plumeEmission = plumeSystem.emission;
            plumeEmission.rateOverTime = 260f;
            ParticleSystem.ShapeModule plumeShape = plumeSystem.shape;
            plumeShape.shapeType = ParticleSystemShapeType.Cone;
            plumeShape.angle = 14f;
            plumeShape.radius = 0.55f;
            exhaust = exhaustRoot;
            exhaust.SetActive(false);
            CapsuleCollider boardingRange = rocketRoot.AddComponent<CapsuleCollider>();
            boardingRange.direction = 1;
            boardingRange.center = new Vector3(0f, 7f, 0f);
            boardingRange.height = 17f;
            boardingRange.radius = 2.5f;
            boardingRange.isTrigger = true;
            AstraInteractable interactable = rocketRoot.AddComponent<AstraInteractable>();
            interactable.facilityName = "Lunar launch rocket";
            interactable.description = "Board the crew capsule for a short, interactive Earth-to-Moon learning flight.";
            interactable.interactionRange = 5.5f;

            Material padMaterial = MakeMaterial(new Color(0.24f, 0.29f, 0.32f));
            CreatePrimitive(PrimitiveType.Cylinder, "Rocket launch pad", earthSite.transform, new Vector3(0f, -0.08f, 17f), new Vector3(8f, 0.18f, 8f), padMaterial);
            CreateBlock("Pad marking north", earthSite.transform, new Vector3(0f, 0.12f, 23.5f), new Vector3(1.4f, 0.07f, 0.3f), white);

            if (nasaModelLoader != null)
            {
                // Keep the complete colored 3D rocket visible while the large NASA
                // STL is read. If import fails or takes too long, this recognizable
                // fallback still travels with the astronaut instead of disappearing.
                nasaModelLoader.LoadStlInto("MissionHardware/SLS_extract/Space Launch System (SLS).stl", rocketRoot.transform, 25f, model =>
                {
                    if (model == null) return;
                    // Replace the fallback only after a visible NASA model exists.
                    foreach (Renderer renderer in rocketRoot.GetComponentsInChildren<Renderer>(true))
                        if (renderer.transform != model.transform && !renderer.transform.IsChildOf(model.transform)) renderer.enabled = false;
                    foreach (Collider collider in rocketRoot.GetComponentsInChildren<Collider>(true))
                        if (collider.transform != rocketRoot.transform && !collider.transform.IsChildOf(model.transform)) collider.enabled = false;
                    Debug.Log("ASTRA launch scene now uses NASA's Space Launch System 3D model.");
                });
            }
        }

        private void CreateCloudDeck(Material cloud)
        {
            for (int row = 0; row < 3; row++)
            for (int i = 0; i < 7; i++)
            {
                float x = -24f + i * 8f + (row % 2) * 3f;
                float z = 25f + row * 8f;
                float y = 22f + Mathf.Sin(i * 1.7f + row) * 2.4f;
                GameObject puff = CreatePrimitive(PrimitiveType.Sphere, "Soft cloud", earthSite.transform,
                    new Vector3(x, y, z), new Vector3(5.5f, 2.2f, 4.2f), cloud);
                Collider collider = puff.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                Renderer renderer = puff.GetComponent<Renderer>();
                if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void CreateDistantEarthAndMoon(Material earthBlue, Material moonGray)
        {
            earthGlobe = CreatePrimitive(PrimitiveType.Sphere, "Earth - seen from space", earthSite.transform,
                new Vector3(0f, -350f, -70f), new Vector3(430f, 430f, 430f), earthBlue);
            earthGlobe.SetActive(false);
            moonGlobe = CreatePrimitive(PrimitiveType.Sphere, "Moon - approach marker", earthSite.transform,
                new Vector3(0f, 485f, 1590f), new Vector3(190f, 190f, 190f), moonGray);
            Collider moonCollider = moonGlobe.GetComponent<Collider>();
            if (moonCollider != null) moonCollider.enabled = false;
            moonGlobe.SetActive(false);

            Material starMaterial = MakeMaterial(new Color(0.84f, 0.91f, 1f));
            var random = new System.Random(1128);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)(random.NextDouble() * 1200.0 - 600.0);
                float y = 100f + (float)(random.NextDouble() * 850.0);
                float z = 500f + (float)(random.NextDouble() * 1400.0);
                GameObject star = CreatePrimitive(PrimitiveType.Sphere, "Distant star", earthSite.transform,
                    new Vector3(x, y, z), Vector3.one * 1.4f, starMaterial);
                Collider collider = star.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                star.SetActive(false);
                star.transform.SetParent(earthSite.transform, false);
            }
        }

        private void StartFlight()
        {
            if (inFlight || rocketRoot == null) return;
            inFlight = true;
            if (player != null)
            {
                player.enabled = false;
                if (astronautModel != null) astronautModel.gameObject.SetActive(false);
            }
            if (mainCamera != null) mainCamera.farClipPlane = 3000f;
            guide?.Show("EARTH / COUNTDOWN", "ROCKET ENGINES ON",
                "Rocket engines push hot gas downward. That push produces thrust in the opposite direction and lifts the vehicle. The flight shown here is shortened for learning.",
                "FLIGHT CONTROLS PAUSED     SKIP FLIGHT: BACKSPACE / B OR USE THE BUTTON",
                true, false);
            flightRoutine = StartCoroutine(FlightSequence());
        }

        private IEnumerator FlightSequence()
        {
            exhaust?.SetActive(true);
            yield return MoveRocket("LIFTOFF", "GROUND TO 1 km",
                "The engines push exhaust downward, creating thrust that lifts the rocket. The vehicle first rises through the lower air.",
                8f, 0f, 2.5f, new Color(0.36f, 0.64f, 0.87f));

            yield return MoveRocket("TROPOSPHERE / CLOUDS", "ABOUT 0–12 km",
                "This is the lowest atmospheric layer. Nearly all clouds and weather form here. Cloud heights vary; the altitude shown in ASTRA is approximate.",
                34f, 8f, 3.4f, new Color(0.43f, 0.70f, 0.88f));

            yield return MoveRocket("THE ROCKET TILTS", "GRAVITY TURN",
                "The rocket gradually turns sideways. It needs strong sideways speed to enter orbit; orbit is not simply going straight up. Earth’s gravity still pulls on a spacecraft in orbit.",
                65f, 43f, 3.4f, new Color(0.35f, 0.62f, 0.82f));

            yield return MoveRocket("STRATOSPHERE", "ABOUT 12–50 km",
                "The air is thinner here. The ozone layer is in this region and absorbs much of the Sun’s harmful ultraviolet light.",
                112f, 70f, 3.1f, new Color(0.20f, 0.43f, 0.72f));

            yield return MoveRocket("MESOSPHERE", "ABOUT 50–85 km",
                "Many meteoroids heat up and glow as they enter this layer. The air is extremely thin compared with the air near the ground.",
                158f, 78f, 3.0f, new Color(0.11f, 0.26f, 0.53f));

            yield return MoveRocket("THERMOSPHERE", "ABOUT 85–600 km",
                "Auroras occur in this very thin upper atmosphere. The International Space Station orbits Earth in the thermosphere.",
                226f, 80f, 3.2f, new Color(0.045f, 0.12f, 0.31f));

            yield return MoveRocket("EXOSPHERE / SPACE", "ABOUT 600–10,000 km · OUTER EDGE VARIES",
                "The exosphere is the outermost atmospheric layer. Its particles are extremely far apart, and the air gradually fades into space rather than ending at a sharp edge. Scientists often use 100 km, called the Kármán line, as a convenient boundary for space.",
                310f, 82f, 3.3f, new Color(0.006f, 0.015f, 0.055f));

            if (boosterGroup != null) boosterGroup.SetActive(false);
            if (earthGlobe != null) earthGlobe.SetActive(true);
            yield return MoveRocket("EARTH ORBIT", "BUILDING SIDEWAYS SPEED",
                "A rocket can drop empty stages to reduce mass. Near orbit, a spacecraft moves sideways so gravity bends its path around Earth; it is still falling, but keeps missing the ground.",
                365f, 82f, 3.2f, new Color(0.002f, 0.006f, 0.025f));

            yield return MoveRocket("TRANS-LUNAR INJECTION", "THE MOON-BOUND BURN",
                "A planned engine burn adds speed and sets the spacecraft on a route toward the Moon. Artemis flights use this kind of maneuver after Earth orbit; ASTRA’s vehicle is a simplified teaching model.",
                415f, 82f, 3.4f, new Color(0.001f, 0.003f, 0.015f));

            if (moonGlobe != null) moonGlobe.SetActive(true);
            if (earthSite != null)
                foreach (Transform child in earthSite.transform)
                    if (child.name == "Distant star") child.gameObject.SetActive(true);
            yield return MoveRocket("COAST TO THE MOON", "DAYS OF TRAVEL — COMPRESSED HERE",
                "The spacecraft coasts for most of the journey; the engine does not burn the whole way. Real lunar voyages take days. ASTRA compresses this trip into a few seconds.",
                485f, 82f, 4.2f, new Color(0.001f, 0.002f, 0.012f));

            flightRoutine = null;
            yield return StartCoroutine(LunarLandingSequence());
        }

        private IEnumerator MoveRocket(string title, string altitude, string body, float targetY, float targetTilt,
            float duration, Color skyColor)
        {
            if (guide != null) guide.Show("EARTH TO MOON / LEARNING FLIGHT", title, body,
                "ALTITUDE GUIDE: REAL-WORLD RANGES · ROCKET MOTION: COMPRESSED SCALE",
                true, false, altitude);

            Vector3 fromPosition = rocketRoot.transform.position;
            Vector3 toPosition = new Vector3(Mathf.Min(targetY * 0.035f, 15f), targetY, 17f + targetY * 2f);
            float fromTilt = rocketRoot.transform.eulerAngles.x;
            if (fromTilt > 180f) fromTilt -= 360f;
            Color fromSky = mainCamera != null ? mainCamera.backgroundColor : skyColor;
            float elapsed = 0f;
            while (elapsed < duration && IsRunning && inFlight)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rocketRoot.transform.position = Vector3.Lerp(fromPosition, toPosition, t);
                float tilt = Mathf.Lerp(fromTilt, targetTilt, t);
                rocketRoot.transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
                if (mainCamera != null) mainCamera.backgroundColor = Color.Lerp(fromSky, skyColor, t);
                if (RenderSettings.fog) RenderSettings.fogDensity = Mathf.Lerp(0.0018f, 0f, Mathf.InverseLerp(8f, 120f, rocketRoot.transform.position.y));
                yield return null;
            }
        }

        private void UpdateFlightCamera()
        {
            if (mainCamera == null || rocketRoot == null) return;
            Vector3 forward = rocketRoot.transform.forward;
            Vector3 target = rocketRoot.transform.position - forward * 18f + Vector3.up * 7f;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target, Time.deltaTime * 2f);
            mainCamera.transform.LookAt(rocketRoot.transform.position + forward * 8f + Vector3.up * 5f);
        }

        private void SkipFlight()
        {
            if (!IsRunning || !inFlight) return;
            if (flightRoutine != null) StopCoroutine(flightRoutine);
            flightRoutine = null;
            StartCoroutine(LunarLandingSequence());
        }

        private void CancelLaunch()
        {
            if (!IsRunning || missionStartCalled) return;
            if (flightRoutine != null) StopCoroutine(flightRoutine);
            flightRoutine = null;
            StopAllCoroutines();
            inFlight = false;
            touchdown = false;
            if (landerRoot != null) Destroy(landerRoot);
            landerRoot = null;
            if (earthSite != null) Destroy(earthSite);
            earthSite = null;
            RestoreMoonLighting();
            if (lunarEnvironment != null) lunarEnvironment.SetActive(lunarEnvironmentWasActive);
            if (lunarOutpost != null) lunarOutpost.SetActive(lunarOutpostWasActive);
            if (astronautModel != null) astronautModel.gameObject.SetActive(true);
            if (player != null)
            {
                player.enabled = true;
                player.ResetForMission();
            }
            IsRunning = false;
            guide?.Destroy();
            guide = null;
            missionStart = null;
            Action callback = launchCancelled;
            launchCancelled = null;
            callback?.Invoke();
        }

        private IEnumerator LunarLandingSequence()
        {
            inFlight = false;
            if (guide != null)
                guide.Show("LUNAR ARRIVAL / DESCENT", "LANDING ON THE MOON",
                    "The Moon has almost no atmosphere, so a lander cannot use parachutes to slow down. Rocket thrust controls the descent. The dust below is lunar regolith, a layer of loose rock and soil.",
                    "POWERED DESCENT: THE ENGINES SLOW THE LANDER",
                    false, false, "FINAL DESCENT · APPROACHING SHACKLETON OUTPOST");

            if (earthSite != null) earthSite.SetActive(false);
            if (lunarEnvironment != null) lunarEnvironment.SetActive(lunarEnvironmentWasActive);
            if (lunarOutpost != null) lunarOutpost.SetActive(lunarOutpostWasActive);
            RestoreMoonLighting();
            if (rocketRoot != null) rocketRoot.SetActive(false);
            if (mainCamera != null) mainCamera.farClipPlane = previousFarClip > 0f ? previousFarClip : 260f;
            if (player != null && player.transform != null)
            {
                player.enabled = false;
                if (astronautModel != null) astronautModel.gameObject.SetActive(false);
            }

            landerRoot = CreateLunarLander(new Vector3(0f, 20f, -1.5f));
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(15f, 15f, -22f);
                mainCamera.transform.LookAt(new Vector3(0f, 8f, -1.5f));
            }
            Vector3 from = landerRoot.transform.position;
            Vector3 to = new Vector3(from.x, 0.8f, from.z);
            float elapsed = 0f;
            while (elapsed < 5.5f && IsRunning)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 5.5f));
                landerRoot.transform.position = Vector3.Lerp(from, to, t);
                if (mainCamera != null) mainCamera.transform.LookAt(landerRoot.transform.position + Vector3.up * 2f);
                yield return null;
            }

            CreateLandingDust();
            if (landerRoot != null) Destroy(landerRoot);
            landerRoot = null;
            touchdown = true;
            if (guide != null)
                guide.Show("LUNAR TOUCHDOWN / SHACKLETON REGION", "WELCOME TO THE MOON",
                    "You have landed at the lunar outpost. Your first mission begins next: bring the habitat systems online. The journey was shortened for play; the atmosphere lesson used approximate NASA layer heights.",
                    "PRESS ENTER / A OR SELECT BEGIN MISSION 01",
                    false, true, "TOUCHDOWN CONFIRMED · MISSION 01 READY");

            float wait = 10f;
            while (wait > 0f && IsRunning && touchdown)
            {
                wait -= Time.deltaTime;
                if (guide != null) guide.SetContinueCountdown(Mathf.CeilToInt(wait));
                yield return null;
            }
            if (IsRunning && touchdown) BeginMissionNow();
        }

        private GameObject CreateLunarLander(Vector3 position)
        {
            GameObject lander = new GameObject("ASTRA Lunar Lander - Arrival Demonstration");
            lander.transform.position = position;
            Material foil = MakeMaterial(new Color(0.69f, 0.61f, 0.39f));
            Material strut = MakeMaterial(new Color(0.20f, 0.23f, 0.25f));
            Material window = MakeMaterial(new Color(0.08f, 0.46f, 0.62f));
            CreatePrimitive(PrimitiveType.Cube, "Lander cabin", lander.transform, new Vector3(0f, 3f, 0f), new Vector3(3.5f, 3.6f, 3.5f), foil);
            CreatePrimitive(PrimitiveType.Cube, "Lander window", lander.transform, new Vector3(0f, 3.3f, 1.78f), new Vector3(1.2f, 0.8f, 0.08f), window);
            CreatePrimitive(PrimitiveType.Cylinder, "Descent engine", lander.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 0.65f, 0.7f), strut);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                CreatePrimitive(PrimitiveType.Cube, "Landing leg", lander.transform,
                    new Vector3(direction.x * 2.1f, 0.6f, direction.z * 2.1f), new Vector3(0.22f, 2.1f, 0.22f), strut)
                    .transform.localRotation = Quaternion.Euler(direction.z * 22f, 0f, -direction.x * 22f);
                CreatePrimitive(PrimitiveType.Cube, "Landing foot", lander.transform,
                    new Vector3(direction.x * 2.55f, 0.04f, direction.z * 2.55f), new Vector3(0.95f, 0.12f, 0.75f), foil);
            }
            GameObject plume = CreatePrimitive(PrimitiveType.Cylinder, "Powered landing exhaust", lander.transform,
                new Vector3(0f, -1.5f, 0f), new Vector3(0.65f, 1.3f, 0.65f), MakeMaterial(new Color(0.92f, 0.65f, 0.24f)));
            Collider plumeCollider = plume.GetComponent<Collider>();
            if (plumeCollider != null) plumeCollider.enabled = false;
            // Replace the landing demonstration's temporary blockout with NASA's flown Apollo LM mesh.
            if (nasaModelLoader != null)
            {
                foreach (Renderer renderer in lander.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (Collider collider in lander.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                nasaModelLoader.LoadInto("ApolloLunarModule", lander.transform, 7.5f, model =>
                {
                    if (model == null) return;
                    foreach (Renderer renderer in lander.GetComponentsInChildren<Renderer>(true))
                        if (!renderer.transform.IsChildOf(model.transform)) renderer.enabled = false;
                    foreach (Collider collider in lander.GetComponentsInChildren<Collider>(true))
                        if (!collider.transform.IsChildOf(model.transform)) collider.enabled = false;
                });
            }
            return lander;
        }

        private void CreateLandingDust()
        {
            if (lunarEnvironment == null) return;
            Material dust = MakeMaterial(new Color(0.53f, 0.53f, 0.55f));
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 2.3f, 0.18f, Mathf.Sin(angle) * 2.3f);
                GameObject puff = CreatePrimitive(PrimitiveType.Sphere, "Landing regolith puff", lunarEnvironment.transform,
                    new Vector3(offset.x, 0.2f, -1.5f + offset.z), new Vector3(1.2f, 0.36f, 1.2f), dust);
                Collider collider = puff.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                StartCoroutine(FadeDust(puff, offset));
            }
        }

        private IEnumerator FadeDust(GameObject puff, Vector3 offset)
        {
            Vector3 start = puff.transform.position;
            float elapsed = 0f;
            while (elapsed < 1.5f && puff != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 1.5f;
                puff.transform.position = start + offset * t + Vector3.up * (1.5f * t);
                puff.transform.localScale = Vector3.one * (0.6f + 1.4f * t);
                yield return null;
            }
            if (puff != null) Destroy(puff);
        }

        private void BeginMissionNow()
        {
            if (missionStartCalled || !IsRunning) return;
            missionStartCalled = true;
            touchdown = false;
            inFlight = false;
            if (landerRoot != null) Destroy(landerRoot);
            if (earthSite != null) Destroy(earthSite);
            earthSite = null;
            RestoreMoonLighting();
            if (lunarEnvironment != null) lunarEnvironment.SetActive(lunarEnvironmentWasActive);
            if (lunarOutpost != null) lunarOutpost.SetActive(lunarOutpostWasActive);
            if (player != null)
            {
                if (astronautModel != null) astronautModel.gameObject.SetActive(true);
                player.enabled = true;
                player.ResetForMission();
            }
            IsRunning = false;
            guide?.Destroy();
            guide = null;
            Action callback = missionStart;
            missionStart = null;
            launchCancelled = null;
            callback?.Invoke();
        }

        private void SaveAndSetEarthLighting()
        {
            if (mainCamera != null)
            {
                previousClearFlags = mainCamera.clearFlags;
                previousBackground = mainCamera.backgroundColor;
                previousFarClip = mainCamera.farClipPlane;
                savedCamera = true;
            }
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode;
            previousFogDensity = RenderSettings.fogDensity;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientLight = RenderSettings.ambientLight;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousSkybox = RenderSettings.skybox;
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in lights)
                if (light.type == LightType.Directional && light.name.Contains("Sun")) { sun = light; break; }
            if (sun != null)
            {
                previousSunRotation = sun.transform.rotation;
                previousSunColor = sun.color;
                previousSunIntensity = sun.intensity;
            }
        }

        private void RestoreMoonLighting()
        {
            if (savedCamera && mainCamera != null)
            {
                mainCamera.clearFlags = previousClearFlags;
                mainCamera.backgroundColor = previousBackground;
                mainCamera.farClipPlane = previousFarClip;
            }
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogDensity = previousFogDensity;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.skybox = previousSkybox;
            if (sun != null)
            {
                sun.transform.rotation = previousSunRotation;
                sun.color = previousSunColor;
                sun.intensity = previousSunIntensity;
            }
        }

        private void CreateWorldLabel(string value, Vector3 position, float scale, Color color)
        {
            GameObject label = new GameObject(value + " sign");
            label.transform.SetParent(earthSite.transform, false);
            label.transform.position = position;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = value;
            text.fontSize = 60;
            text.characterSize = scale;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
        }

        private GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, parent, position, scale, material);
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return item;
        }

        private Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            generatedMaterials.Add(material);
            return material;
        }

        private sealed class LaunchGuide
        {
            private readonly GameObject canvasObject;
            private readonly Text kicker;
            private readonly Text altitude;
            private readonly Text title;
            private readonly Text body;
            private readonly Text controls;
            private readonly Text countdown;
            private readonly Button skipButton;
            private readonly Button startButton;
            private readonly Button backButton;

            public LaunchGuide(Action onSkip, Action onStart, Action onBack)
            {
                canvasObject = new GameObject("ASTRA Earth-to-Moon Learning Guide", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 150;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;

                Color panel = new Color(0.015f, 0.045f, 0.085f, 0.86f);
                if (PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.HighContrast)
                    panel = new Color(0f, 0f, 0f, 0.98f);
                GameObject card = MakePanel(canvasObject.transform, panel, new Vector2(0.025f, 0.59f), new Vector2(0.49f, 0.97f));
                Image cardImage = card.GetComponent<Image>();
                Sprite panelSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                if (panelSprite != null)
                {
                    cardImage.sprite = panelSprite;
                    cardImage.type = Image.Type.Sliced;
                    cardImage.pixelsPerUnitMultiplier = 1.4f;
                }
                Outline cyanRim = card.AddComponent<Outline>();
                cyanRim.effectColor = new Color(0.12f, 0.78f, 1f, 0.72f);
                cyanRim.effectDistance = new Vector2(1.5f, -1.5f);
                Shadow cardShadow = card.AddComponent<Shadow>();
                cardShadow.effectColor = new Color(0f, 0.55f, 0.95f, 0.22f);
                cardShadow.effectDistance = new Vector2(0f, -5f);
                MakePanel(card.transform, new Color(0.12f, 0.85f, 1f, 0.96f), new Vector2(0f, 0f), new Vector2(0.012f, 1f));

                kicker = MakeText(card.transform, "EARTH / CREW PREPARATION", 13, new Color(0.18f, 0.88f, 1f), FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(0.055f, 0.83f), new Vector2(0.46f, 0.98f));
                altitude = MakeText(card.transform, "", 13, new Color(1f, 0.79f, 0.36f), FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(0.48f, 0.83f), new Vector2(0.70f, 0.98f));
                title = MakeText(card.transform, "YOUR JOURNEY STARTS HERE", 24, Color.white, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(0.055f, 0.68f), new Vector2(0.94f, 0.84f));
                body = MakeText(card.transform, "", 17, new Color(0.84f, 0.9f, 0.95f), FontStyle.Normal,
                    TextAnchor.UpperLeft, new Vector2(0.055f, 0.35f), new Vector2(0.94f, 0.68f));
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                controls = MakeText(card.transform, "", 12, new Color(0.57f, 0.75f, 0.86f), FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(0.055f, 0.22f), new Vector2(0.94f, 0.35f));
                countdown = MakeText(card.transform, "", 12, new Color(1f, 0.79f, 0.36f), FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(0.055f, 0.16f), new Vector2(0.94f, 0.23f));
                MakeButton(card.transform, "NASA ATMOSPHERE ↗", () => Application.OpenURL(AtmosphereSource),
                    new Vector2(0.055f, 0.035f), new Vector2(0.26f, 0.15f), new Color(0.035f, 0.22f, 0.31f), 11);
                MakeButton(card.transform, "NASA ARTEMIS ↗", () => Application.OpenURL(ArtemisSource),
                    new Vector2(0.275f, 0.035f), new Vector2(0.46f, 0.15f), new Color(0.035f, 0.22f, 0.31f), 11);
                MakeButton(card.transform, "NASA MOON ↗", () => Application.OpenURL(MoonSource),
                    new Vector2(0.475f, 0.035f), new Vector2(0.625f, 0.15f), new Color(0.035f, 0.22f, 0.31f), 11);
                skipButton = MakeButton(card.transform, "SKIP FLIGHT", onSkip,
                    new Vector2(0.65f, 0.035f), new Vector2(0.94f, 0.15f), new Color(0.05f, 0.31f, 0.39f), 12);
                startButton = MakeButton(card.transform, "BEGIN MISSION 01", onStart,
                    new Vector2(0.65f, 0.035f), new Vector2(0.94f, 0.15f), new Color(0.05f, 0.43f, 0.49f), 12);
                backButton = MakeButton(card.transform, "← BACK TO BOARD", onBack,
                    new Vector2(0.73f, 0.845f), new Vector2(0.94f, 0.97f), new Color(0.05f, 0.20f, 0.29f), 11);
                skipButton.gameObject.SetActive(false);
                startButton.gameObject.SetActive(false);
                backButton.gameObject.SetActive(false);
            }

            public void Show(string kickerText, string titleText, string bodyText, string controlsText,
                bool allowSkip, bool allowStart, string altitudeText = "")
            {
                if (canvasObject == null) return;
                kicker.text = kickerText;
                altitude.text = altitudeText;
                title.text = titleText;
                body.text = bodyText;
                controls.text = controlsText;
                countdown.text = allowStart ? "" : "NASA learning flight · Visual distances and travel time are compressed.";
                skipButton.gameObject.SetActive(allowSkip && !allowStart);
                startButton.gameObject.SetActive(allowStart);
                backButton.gameObject.SetActive(true);
                if (allowStart && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(startButton.gameObject);
                else if (!allowSkip && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            public void SetContinueCountdown(int seconds)
            {
                if (countdown != null && startButton != null && startButton.gameObject.activeSelf)
                    countdown.text = seconds > 0 ? "Starting Mission 01 in " + seconds + " seconds · choose BEGIN MISSION 01 to start now." : "Starting Mission 01…";
            }

            public void Destroy()
            {
                if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
            }

            private static GameObject MakePanel(Transform parent, Color color, Vector2 min, Vector2 max)
            {
                GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = color;
                return panel;
            }

            private static Text MakeText(Transform parent, string value, int size, Color color, FontStyle style, TextAnchor anchor,
                Vector2 min, Vector2 max)
            {
                GameObject item = new GameObject("Text", typeof(RectTransform), typeof(Text));
                item.transform.SetParent(parent, false);
                RectTransform rect = item.GetComponent<RectTransform>();
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                PlayerSettingsManager settings = PlayerSettingsManager.Instance;
                float scale = settings != null ? settings.TextScale : 1f;
                Text text = item.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * scale), 12, 34);
                text.color = color;
                text.fontStyle = style;
                text.alignment = anchor;
                text.text = value;
                text.raycastTarget = false;
                Shadow shadow = item.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0.04f, 0.10f, 0.82f);
                shadow.effectDistance = new Vector2(0f, -1.5f);
                return text;
            }

            private static Button MakeButton(Transform parent, string label, Action action, Vector2 min, Vector2 max, Color color, int size)
            {
                GameObject item = MakePanel(parent, Color.white, min, max);
                item.name = label;
                Button button = item.AddComponent<Button>();
                button.targetGraphic = item.GetComponent<Image>();
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = new Color(0.02f, 0.72f, 0.90f, 1f);
                colors.pressedColor = new Color(0.015f, 0.34f, 0.48f, 1f);
                colors.selectedColor = new Color(0.04f, 0.60f, 0.78f, 1f);
                colors.disabledColor = new Color(0.16f, 0.22f, 0.29f, 0.78f);
                colors.fadeDuration = 0.10f;
                button.colors = colors;
                item.AddComponent<LaunchButtonMotion>();
                Outline edge = item.AddComponent<Outline>();
                edge.effectColor = new Color(0.14f, 0.72f, 0.96f, 0.58f);
                edge.effectDistance = new Vector2(1f, -1f);
                button.onClick.AddListener(() => action?.Invoke());
                Text text = MakeText(item.transform, label, size, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(0.035f, 0f), new Vector2(0.965f, 1f));
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 10;
                text.resizeTextMaxSize = size;
                return button;
            }
        }

        /// <summary>Subtle pointer and controller focus motion for the existing launch-guide buttons.</summary>
        private sealed class LaunchButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
            IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
        {
            private RectTransform rect;
            private Vector3 targetScale = Vector3.one;
            private bool pointerInside;

            private void Awake() => rect = transform as RectTransform;

            private void Update()
            {
                if (rect != null)
                    rect.localScale = Vector3.Lerp(rect.localScale, targetScale, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            }

            public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; targetScale = Vector3.one * 1.035f; }
            public void OnPointerExit(PointerEventData eventData) { pointerInside = false; targetScale = Vector3.one; }
            public void OnPointerDown(PointerEventData eventData) => targetScale = Vector3.one * 0.975f;
            public void OnPointerUp(PointerEventData eventData) => targetScale = pointerInside ? Vector3.one * 1.035f : Vector3.one;
            public void OnSelect(BaseEventData eventData) => targetScale = Vector3.one * 1.035f;
            public void OnDeselect(BaseEventData eventData) => targetScale = Vector3.one;
        }

        private void OnDestroy()
        {
            if (guide != null) guide.Destroy();
            if (earthSite != null) Destroy(earthSite);
            if (IsRunning)
            {
                RestoreMoonLighting();
                if (lunarEnvironment != null) lunarEnvironment.SetActive(lunarEnvironmentWasActive);
                if (lunarOutpost != null) lunarOutpost.SetActive(lunarOutpostWasActive);
                if (player != null) player.enabled = true;
            }
            foreach (Material material in generatedMaterials)
                if (material != null) Destroy(material);
            if (Instance == this) Instance = null;
        }
    }
}
