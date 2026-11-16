using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Astra
{
    /// <summary>Short, hands-on maintenance sequence with procedural tools and generated audio.</summary>
    public sealed class AstraMaintenanceSequence : MonoBehaviour
    {
        private static readonly string[] DefaultSteps = { "INSPECT THE CONNECTOR", "TIGHTEN WITH THE SPANNER", "SECURE WITH THE SCREWDRIVER" };
        private const float RepairDuration = 10f;
        private const float WorkSecondsPerStep = RepairDuration / 3f;
        private const float CheckpointDuration = 0.8f;

        private AstraGameManager game;
        private AstraHUD hud;
        private AstraAstronautMotion motion;
        private Transform spanner;
        private Transform screwdriver;
        private AudioSource audioSource;
        private AudioClip workLoop;
        private AudioClip toolClick;
        private GameObject overlay;
        private Text titleText;
        private Text stepText;
        private Text helpText;
        private Image progressFill;
        private Button continueButton;
        private RectTransform spannerVisual;
        private RectTransform screwdriverVisual;
        private AstraInteractable activeFacility;
        private AstraMissionId launchMission;
        private bool launchRepair;
        private bool confirmed;
        private int currentStep;
        private string[] currentSteps = DefaultSteps;
        private float clickClock;
        private bool toolsBuilt;

        public bool IsRunning { get; private set; }

        private void Awake()
        {
            game = GetComponent<AstraGameManager>();
            GameObject audioObject = new GameObject("Maintenance tool audio");
            audioObject.transform.SetParent(transform, false);
            audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0.25f;
            audioSource.volume = 0.18f;
            workLoop = MakeWorkLoop();
            toolClick = MakeToolClick();
        }

        private void Start()
        {
            hud = FindAnyObjectByType<AstraHUD>();
            BuildOverlay();
            BuildTools();
        }

        public void Begin(AstraInteractable facility)
        {
            BeginSequence(facility, false, AstraMissionId.FirstLight);
        }

        public void BeginLaunchRepair(AstraInteractable spacecraft, AstraMissionId nextMission)
        {
            BeginSequence(spacecraft, true, nextMission);
        }

        private void BeginSequence(AstraInteractable facility, bool isLaunchRepair, AstraMissionId nextMission)
        {
            if (IsRunning || facility == null) return;
            if (overlay == null) BuildOverlay();
            if (!toolsBuilt) BuildTools();
            activeFacility = facility;
            launchRepair = isLaunchRepair;
            launchMission = nextMission;
            currentSteps = StepsFor(facility);
            currentStep = 0;
            confirmed = false;
            clickClock = 0f;
            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            if (player != null)
            {
                Vector3 direction = facility.transform.position - player.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(direction);
                audioSource.transform.position = player.transform.position;
            }
            IsRunning = true;
            motion?.SetWorkingMotion(true);
            if (spanner != null) spanner.gameObject.SetActive(true);
            if (screwdriver != null) screwdriver.gameObject.SetActive(true);
            titleText.text = isLaunchRepair ? "SPACECRAFT  /  LAUNCH REPAIR" : facility.facilityName.ToUpperInvariant() + "  /  MAINTENANCE";
            progressFill.fillAmount = 1f;
            overlay.SetActive(true);
            continueButton.gameObject.SetActive(false);
            audioSource.clip = workLoop;
            audioSource.Play();
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            float totalElapsed = 0f;
            for (currentStep = 0; currentStep < currentSteps.Length; currentStep++)
            {
                helpText.text = "Tools at work. The blue bar counts down from 10 seconds. Escape cancels.";
                float elapsed = 0f;
                bool checkpointShown = false;
                confirmed = false;
                while (elapsed < WorkSecondsPerStep)
                {
                    float delta = Time.unscaledDeltaTime;
                    elapsed += delta;
                    totalElapsed = Mathf.Min(RepairDuration, totalElapsed + delta);
                    float remaining = Mathf.Max(0f, RepairDuration - totalElapsed);
                    stepText.text = "STEP " + (currentStep + 1) + " OF " + currentSteps.Length + "   •   " + currentSteps[currentStep] + "   •   " + Mathf.CeilToInt(remaining) + "s";
                    progressFill.fillAmount = remaining / RepairDuration;
                    AnimateTools(elapsed);
                    clickClock -= Time.unscaledDeltaTime;
                    if (clickClock <= 0f)
                    {
                        audioSource.PlayOneShot(toolClick, 0.75f);
                        clickClock = 1.15f;
                    }
                    if (!checkpointShown && elapsed >= WorkSecondsPerStep - CheckpointDuration)
                    {
                        checkpointShown = true;
                        continueButton.gameObject.SetActive(true);
                        helpText.text = "Press E / X or tap CONTINUE to check this step. Repair timer keeps running.";
                    }
                    if (checkpointShown && !confirmed && WasConfirmPressed())
                    {
                        confirmed = true;
                        continueButton.gameObject.SetActive(false);
                        helpText.text = "Step checked. Keep watching the repair timer.";
                        audioSource.PlayOneShot(toolClick, 1.1f);
                    }
                    if (WasCancelPressed()) { Cancel(); yield break; }
                    yield return null;
                }
                continueButton.gameObject.SetActive(false);
            }

            progressFill.fillAmount = 0f;
            AstraInteractable finished = activeFacility;
            bool completedLaunchRepair = launchRepair;
            AstraMissionId destinationMission = launchMission;
            FinishVisuals();
            if (game != null && finished != null)
            {
                if (completedLaunchRepair) game.CompleteSpacecraftLaunch(destinationMission);
                else game.CompleteMaintenance(finished);
            }
        }

        private void AnimateTools(float elapsed)
        {
            if (spanner != null)
            {
                bool active = currentStep != 2;
                spanner.gameObject.SetActive(active);
                if (active) spanner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 10f) * 22f);
            }
            if (spannerVisual != null)
            {
                spannerVisual.gameObject.SetActive(currentStep != 2);
                spannerVisual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 10f) * 18f);
            }
            if (screwdriver != null)
            {
                bool active = currentStep == 0 || currentStep == 2;
                screwdriver.gameObject.SetActive(active);
                if (active) screwdriver.localRotation = Quaternion.Euler(0f, Mathf.Sin(elapsed * 12f) * 42f, 0f);
            }
            if (screwdriverVisual != null)
            {
                screwdriverVisual.gameObject.SetActive(currentStep == 0 || currentStep == 2);
                screwdriverVisual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 12f) * 14f);
            }
        }

        private bool WasConfirmPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;
            return (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonWest.wasPressedThisFrame) ||
                   (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private bool WasCancelPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        }

        public void ConfirmStep() => confirmed = true;

        private string[] StepsFor(AstraInteractable facility)
        {
            switch (facility.facilityType)
            {
                case FacilityType.Communications: return new[] { "INSPECT THE RELAY JOINT", "ALIGN THE DISH WITH THE SPANNER", "SECURE THE TERMINAL WITH THE DRIVER" };
                case FacilityType.SolarArray: return new[] { "CHECK THE ARRAY HINGE", "TIGHTEN THE SUPPORT WITH THE SPANNER", "CALIBRATE THE PANEL WITH THE DRIVER" };
                case FacilityType.LifeSupport: return new[] { "INSPECT THE AIR AND WATER COUPLINGS", "TIGHTEN THE VALVE WITH THE SPANNER", "SECURE THE CONTROL PANEL" };
                case FacilityType.Habitat: return new[] { "CHECK THE PRESSURE SEAL", "TIGHTEN THE AIRLOCK LATCH", "VERIFY THE HABITAT SEAL" };
                case FacilityType.Greenhouse:
                    return game != null && game.CropReady
                        ? new[] { "CHECK THE HARVEST TRAY", "RELEASE THE TRAY FASTENER", "SECURE THE FOOD CANISTER" }
                        : new[] { "CHECK THE GROW-LIGHT CONNECTOR", "TIGHTEN THE IRRIGATION COUPLING", "SET THE GROW-LIGHT PANEL" };
                case FacilityType.RadiationShelter: return new[] { "INSPECT THE SHELTER DOOR", "TIGHTEN THE ACCESS LATCH", "SECURE THE SHIELD PANEL" };
                case FacilityType.OxygenProcessor: return new[] { "CHECK THE OXYGEN LINE", "TIGHTEN THE PROCESSOR COUPLING", "SECURE THE CONTROL PANEL" };
                case FacilityType.WaterRecycler: return new[] { "CHECK THE RETURN PIPE", "TIGHTEN THE WATER COUPLING", "SECURE THE FILTER PANEL" };
                case FacilityType.Rover: return new[] { "CHECK THE SAMPLE SCOOP", "SECURE THE SCOOP WITH THE SPANNER", "LOCK THE SAMPLE CANISTER" };
                default: return DefaultSteps;
            }
        }

        public void Cancel()
        {
            if (!IsRunning) return;
            StopAllCoroutines();
            AstraInteractable cancelled = activeFacility;
            bool wasLaunchRepair = launchRepair;
            FinishVisuals();
            launchRepair = false;
            hud?.ShowMessage(wasLaunchRepair ? "Launch repair paused. The debrief is ready when you are." : "Maintenance paused. Return to the facility and interact to try again.");
            if (cancelled != null && !wasLaunchRepair) cancelled.isOnline = false;
            if (wasLaunchRepair) game?.CancelSpacecraftLaunchRepair();
        }

        private void FinishVisuals()
        {
            IsRunning = false;
            activeFacility = null;
            launchRepair = false;
            audioSource.Stop();
            motion?.SetWorkingMotion(false);
            if (spanner != null) spanner.gameObject.SetActive(false);
            if (screwdriver != null) screwdriver.gameObject.SetActive(false);
            if (overlay != null) overlay.SetActive(false);
        }

        private void BuildTools()
        {
            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            if (player == null) return;
            motion = player.GetComponent<AstraAstronautMotion>();
            Transform leftArm = FindChild(player.transform, "Left arm pivot");
            Transform rightArm = FindChild(player.transform, "Right arm pivot");
            if (leftArm == null || rightArm == null) return;
            spanner = MakeSpanner(leftArm).transform;
            screwdriver = MakeScrewdriver(rightArm).transform;
            spanner.gameObject.SetActive(false);
            screwdriver.gameObject.SetActive(false);
            toolsBuilt = true;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        private static GameObject MakeSpanner(Transform hand)
        {
            GameObject root = new GameObject("Junior spanner");
            root.transform.SetParent(hand, false);
            root.transform.localPosition = new Vector3(0f, -0.48f, 0.12f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            MakeToolPart(PrimitiveType.Cube, "Spanner handle", root.transform, new Vector3(0f, -0.13f, 0f), new Vector3(0.065f, 0.32f, 0.055f), new Color(0.68f, 0.75f, 0.78f));
            MakeToolPart(PrimitiveType.Cube, "Spanner head", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.17f, 0.07f, 0.06f), new Color(0.80f, 0.84f, 0.84f));
            MakeToolPart(PrimitiveType.Cube, "Spanner jaw left", root.transform, new Vector3(-0.065f, 0.12f, 0f), new Vector3(0.045f, 0.10f, 0.06f), new Color(0.80f, 0.84f, 0.84f));
            MakeToolPart(PrimitiveType.Cube, "Spanner jaw right", root.transform, new Vector3(0.065f, 0.12f, 0f), new Vector3(0.045f, 0.10f, 0.06f), new Color(0.80f, 0.84f, 0.84f));
            return root;
        }

        private static GameObject MakeScrewdriver(Transform hand)
        {
            GameObject root = new GameObject("Junior screwdriver");
            root.transform.SetParent(hand, false);
            root.transform.localPosition = new Vector3(0f, -0.48f, 0.15f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            MakeToolPart(PrimitiveType.Cylinder, "Screwdriver handle", root.transform, new Vector3(0f, -0.07f, 0f), new Vector3(0.075f, 0.12f, 0.075f), new Color(0.95f, 0.52f, 0.16f));
            MakeToolPart(PrimitiveType.Cylinder, "Screwdriver shaft", root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.022f, 0.11f, 0.022f), new Color(0.78f, 0.84f, 0.86f));
            MakeToolPart(PrimitiveType.Cube, "Screwdriver tip", root.transform, new Vector3(0f, 0.218f, 0f), new Vector3(0.045f, 0.018f, 0.028f), new Color(0.84f, 0.88f, 0.89f));
            return root;
        }

        private static void MakeToolPart(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.material.color = color;
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private void BuildOverlay()
        {
            if (overlay != null) return;
            GameObject canvasObject = new GameObject("ASTRA Maintenance UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            GameObject panel = new GameObject("Maintenance card", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 300f);
            panel.GetComponent<Image>().color = new Color(0.015f, 0.055f, 0.09f, 0.96f);
            overlay = panel;
            titleText = AddText(panel.transform, "FACILITY  /  MAINTENANCE", 23, new Vector2(0f, 0.54f), new Vector2(1f, 0.68f), Color.white, FontStyle.Bold);
            stepText = AddText(panel.transform, "STEP 1 OF 3", 17, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.40f), new Color(0.2f, 0.88f, 1f), FontStyle.Bold);
            helpText = AddText(panel.transform, "SPANNER + SCREWDRIVER  /  MECHANICAL REPAIR", 14, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.26f), new Color(0.72f, 0.82f, 0.9f), FontStyle.Normal);
            spannerVisual = BuildToolVisual(panel.transform, "Spanner illustration", new Vector2(-220f, 95f), true);
            screwdriverVisual = BuildToolVisual(panel.transform, "Screwdriver illustration", new Vector2(220f, 95f), false);
            GameObject track = new GameObject("Work progress", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(panel.transform, false);
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.08f, 0.45f);
            trackRect.anchorMax = new Vector2(0.92f, 0.50f);
            track.GetComponent<Image>().color = new Color(0.12f, 0.20f, 0.27f);
            GameObject fill = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            progressFill = fill.GetComponent<Image>();
            progressFill.color = new Color(0.12f, 0.48f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            continueButton = MakeButton(panel.transform, "CONTINUE", new Vector2(0.36f, 0.01f), new Vector2(0.64f, 0.13f));
            overlay.SetActive(false);
        }

        private Text AddText(Transform parent, string value, int size, Vector2 min, Vector2 max, Color color, FontStyle style)
        {
            GameObject item = new GameObject("Text", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(18f, 0f);
            rect.offsetMax = new Vector2(-18f, 0f);
            Text text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private Button MakeButton(Transform parent, string caption, Vector2 min, Vector2 max)
        {
            GameObject item = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(6f, 2f);
            rect.offsetMax = new Vector2(-6f, -2f);
            item.GetComponent<Image>().color = new Color(0.02f, 0.40f, 0.48f);
            Button button = item.GetComponent<Button>();
            button.onClick.AddListener(ConfirmStep);
            AddText(item.transform, caption, 15, Vector2.zero, Vector2.one, Color.white, FontStyle.Bold);
            return button;
        }

        private RectTransform BuildToolVisual(Transform parent, string name, Vector2 position, bool isSpanner)
        {
            GameObject group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform rect = group.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80f, 100f);
            rect.anchoredPosition = position;
            if (isSpanner)
            {
                AddToolShape(group.transform, "Handle", new Vector2(0f, -10f), new Vector2(13f, 55f), new Color(0.68f, 0.78f, 0.82f));
                AddToolShape(group.transform, "Head", new Vector2(0f, 18f), new Vector2(48f, 12f), new Color(0.82f, 0.88f, 0.9f));
                AddToolShape(group.transform, "Jaw left", new Vector2(-17f, 30f), new Vector2(12f, 22f), new Color(0.82f, 0.88f, 0.9f));
                AddToolShape(group.transform, "Jaw right", new Vector2(17f, 30f), new Vector2(12f, 22f), new Color(0.82f, 0.88f, 0.9f));
            }
            else
            {
                AddToolShape(group.transform, "Driver handle", new Vector2(0f, 21f), new Vector2(26f, 34f), new Color(1f, 0.53f, 0.15f));
                AddToolShape(group.transform, "Driver shaft", new Vector2(0f, -14f), new Vector2(8f, 42f), new Color(0.82f, 0.89f, 0.91f));
                AddToolShape(group.transform, "Driver tip", new Vector2(0f, -38f), new Vector2(16f, 8f), new Color(0.9f, 0.94f, 0.95f));
            }
            return rect;
        }

        private static void AddToolShape(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject shape = new GameObject(name, typeof(RectTransform), typeof(Image));
            shape.transform.SetParent(parent, false);
            RectTransform rect = shape.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            shape.GetComponent<Image>().color = color;
        }

        private static AudioClip MakeWorkLoop()
        {
            const int rate = 22050;
            const int length = rate;
            float[] samples = new float[length];
            System.Random random = new System.Random(2401);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float buzz = Mathf.Sin(2f * Mathf.PI * (92f + 18f * Mathf.Sin(t * 2f * Mathf.PI * 1.7f)) * t);
                float texture = (float)(random.NextDouble() * 2.0 - 1.0) * 0.17f;
                samples[i] = (buzz * 0.045f + texture * 0.03f) * (0.45f + 0.55f * Mathf.Sin(t * Mathf.PI));
            }
            AudioClip clip = AudioClip.Create("ASTRA generated mechanical tool loop", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakeToolClick()
        {
            const int rate = 22050;
            const int length = 3308;
            float[] samples = new float[length];
            System.Random random = new System.Random(713);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float decay = Mathf.Exp(-t * 55f);
                float tone = Mathf.Sin(2f * Mathf.PI * (430f - 160f * t) * t);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                samples[i] = decay * (tone * 0.28f + noise * 0.18f);
            }
            AudioClip clip = AudioClip.Create("ASTRA generated metallic tool click", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
