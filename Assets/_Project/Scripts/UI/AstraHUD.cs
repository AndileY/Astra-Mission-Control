using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Astra
{
    /// <summary>UI Toolkit flight HUD and mission overlays. Public methods are kept compatible with game systems.</summary>
    public sealed class AstraHUD : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0.12f, 0.84f, 1f, 1f);
        private static readonly Color Muted = new Color(0.72f, 0.83f, 0.91f, 1f);
        private static readonly Color Mint = new Color(0.45f, 1f, 0.75f, 1f);

        private AstraGameManager game;
        private UIDocument document;
        private PanelSettings runtimePanelSettings;
        private Volume runtimeLookVolume;
        private VolumeProfile runtimeLookProfile;
        private VisualElement root, hudLayer, topBar, resourceBar, objectivesPanel, objectiveListPanel, touchControls, missionMap, waypointPanel;
        private readonly Dictionary<Transform, VisualElement> missionMapMarkers = new Dictionary<Transform, VisualElement>();
        private VisualElement bottomBar, facilityPanel, resourceAlertPanel, messageBanner;
        private VisualElement introPanel, tutorialPanel, pausePanel, reportPanel, briefingPanel, eventPanel, stormPanel;
        private readonly Dictionary<TextElement, float> baseTextSizes = new Dictionary<TextElement, float>();
        private readonly Dictionary<string, VisualElement> resourceFills = new Dictionary<string, VisualElement>();
        private Label missionCodeText, clockText, statusText, missionTitleText, missionDescriptionText, waypointText;
        private Label objectiveText, objectiveChecklistText, activeObjectiveText, progressText, facilityText, resourceAlertText, messageText;
        private Label briefingBodyText, scienceDataText, eventCopyText;
        private Label stormTitleText, stormBodyText, reportTitleText, reportScoreText, reportBodyText, reportLearningText;
        private Label tutorialTitleText, tutorialBodyText, tutorialCounterText, tutorialKeysText;
        private Button eventPrimaryButton, eventSecondaryButton, stormLinkButton, stormActionButton, launchDestinationButton;
        private Button pauseButton, touchUseButton, touchExitRoverButton;
        private Action stormLinkAction, stormActionAction;
        private bool stormPanelShowsDonki, objectiveListVisible, built, resumeMissionAfterTutorial;
        private int tutorialStep;
        private float refreshClock;
        private static readonly string[] TutorialTitles = { "Move around the outpost.", "Walk up to a facility.", "Watch your resources." };
        private static readonly string[] TutorialBodies =
        {
            "Use the keys to explore the landing site. Take your time; there is no rush.",
            "Visit communications, solar power, and life support. When a facility is close, press E, gamepad X, or the joystick trigger to interact.",
            "Check oxygen, water, food, and power. Follow the objectives and choose a response when an alert appears."
        };
        private static readonly string[] TutorialKeys =
        {
            "WASD / LEFT STICK MOVE     SHIFT / LB SPRINT     SPACE / A HOP",
            "E / X / JOYSTICK TRIGGER INTERACT     MOUSE / RIGHT STICK LOOK",
            "OBJECTIVES     RESOURCE BAR     CHOOSE A RESPONSE"
        };

        private void Awake() => Build();

        private void Start()
        {
            game = AstraGameManager.Instance != null ? AstraGameManager.Instance : FindAnyObjectByType<AstraGameManager>();
            SetupCinematicLook();
            Refresh(game);
            SetCursorForMission(false);
        }

        private void Update()
        {
            if (!built || game == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame) ToggleObjectiveList();
            refreshClock += Time.unscaledDeltaTime;
            if (refreshClock < 0.2f) return;
            refreshClock = 0f;
            Refresh(game);
            UpdateFacilityPrompt();
            UpdateMissionMap();
            UpdateWaypointGuide();
        }

        private void OnDestroy()
        {
            if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
            if (runtimeLookProfile != null) Destroy(runtimeLookProfile);
        }

        private void SetupCinematicLook()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;

            GameObject volumeObject = new GameObject("ASTRA Cinematic Look");
            volumeObject.layer = camera.gameObject.layer;
            volumeObject.transform.SetParent(camera.transform, false);
            runtimeLookVolume = volumeObject.AddComponent<Volume>();
            runtimeLookVolume.isGlobal = true;
            runtimeLookVolume.priority = 2f;
            runtimeLookVolume.weight = 1f;
            cameraData.volumeLayerMask |= 1 << volumeObject.layer;

            runtimeLookProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeLookProfile.name = "ASTRA Runtime Bloom and Color Grade";
            runtimeLookVolume.sharedProfile = runtimeLookProfile;

            Bloom bloom = runtimeLookProfile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.28f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.55f;

            ColorAdjustments color = runtimeLookProfile.Add<ColorAdjustments>(true);
            color.active = true;
            color.contrast.overrideState = true;
            color.contrast.value = 8f;
            color.saturation.overrideState = true;
            color.saturation.value = 6f;
            color.colorFilter.overrideState = true;
            color.colorFilter.value = new Color(0.94f, 0.98f, 1f, 1f);

            Tonemapping tonemapping = runtimeLookProfile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
        }

        private void Build()
        {
            if (built) return;
            built = true;
            EventSystem uiEventSystem = EventSystem.current;
            if (uiEventSystem == null)
                uiEventSystem = new GameObject("ASTRA Event System", typeof(EventSystem)).GetComponent<EventSystem>();
            // A scene may contain an EventSystem configured for legacy uGUI only.
            // UI Toolkit still needs the Input System UI module for pointer clicks,
            // controller submit, and the in-game touch buttons.
            InputSystemUIInputModule uiInput = uiEventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiInput == null) uiInput = uiEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (uiInput.actionsAsset == null) uiInput.AssignDefaultActions();
            foreach (BaseInputModule inputModule in uiEventSystem.GetComponents<BaseInputModule>())
                if (inputModule != uiInput) inputModule.enabled = false;
            GameObject documentObject = new GameObject("ASTRA Gameplay UI Toolkit");
            documentObject.transform.SetParent(transform, false);
            document = documentObject.AddComponent<UIDocument>();
            runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanelSettings.name = "ASTRA Gameplay Panel Settings";
            runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            runtimePanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            runtimePanelSettings.match = 0.5f;
            runtimePanelSettings.sortingOrder = 10;
            document.panelSettings = runtimePanelSettings;
            document.sortingOrder = 10;

            root = document.rootVisualElement;
            root.name = "astra-gameplay-root";
            root.AddToClassList("astra-root");
            StyleSheet theme = Resources.Load<StyleSheet>("AstraOnboardingTheme");
            if (theme != null) root.styleSheets.Add(theme);
            StyleSheet hudTheme = Resources.Load<StyleSheet>("AstraHudTheme");
            if (hudTheme != null) root.styleSheets.Add(hudTheme);

            hudLayer = new VisualElement { name = "flight-hud" };
            hudLayer.AddToClassList("hud-layer");
            root.Add(hudLayer);
            BuildFlightHud();
            BuildMissionOverlays();
            ApplyAccessibilitySettings();
        }

        private Label AddText(VisualElement parent, string value, string className, float size)
        {
            Label label = new Label(value);
            AddClasses(label, className);
            label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            baseTextSizes[label] = size;
            return label;
        }

        private Button AddButton(VisualElement parent, string caption, Action action, bool primary = false, string name = null)
        {
            Button button = new Button(action) { text = caption, name = name ?? caption };
            button.AddToClassList("astra-button");
            button.AddToClassList(primary ? "button-primary" : "button-secondary");
            parent.Add(button);
            baseTextSizes[button] = 16f;
            return button;
        }

        private static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrWhiteSpace(classes)) return;
            foreach (string token in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                element.AddToClassList(token);
        }

        private VisualElement Card(VisualElement parent, string className)
        {
            VisualElement shell = new VisualElement();
            shell.AddToClassList("glass-shell");
            shell.AddToClassList(className + "-shell");
            VisualElement glow = new VisualElement();
            glow.AddToClassList("glass-glow");
            shell.Add(glow);
            VisualElement panel = new VisualElement();
            panel.AddToClassList("glass-panel");
            AddClasses(panel, className);
            shell.Add(panel);
            parent.Add(shell);
            return panel;
        }

        private void BuildFlightHud()
        {
            topBar = new VisualElement();
            topBar.AddToClassList("hud-topbar");
            AddText(topBar, "ASTRA  /  MISSION CONTROL", "hud-brand", 24);
            missionCodeText = AddText(topBar, "MOON OUTPOST  ·  MISSION 01 / 05", "hud-mission-code", 16);
            statusText = AddText(topBar, "OUTPOST  /  INITIALIZING", "hud-status", 16);
            clockText = AddText(topBar, "00:00", "hud-clock", 23);
            hudLayer.Add(topBar);

            resourceBar = new VisualElement();
            resourceBar.AddToClassList("hud-resourcebar");
            BuildResource(resourceBar, "O₂   OXYGEN", "oxygen");
            BuildResource(resourceBar, "H₂O   WATER", "water");
            BuildResource(resourceBar, "▤   FOOD", "food");
            BuildResource(resourceBar, "ϟ   POWER", "power");
            hudLayer.Add(resourceBar);
            BuildMissionMap();

            objectivesPanel = Card(hudLayer, "hud-objectives");
            VisualElement missionTitleRow = new VisualElement();
            missionTitleRow.AddToClassList("hud-title-row");
            AddText(missionTitleRow, "ACTIVE OBJECTIVES", "panel-kicker", 15);
            AddButton(missionTitleRow, "MISSION BRIEF  ↗", () => SetOverlayVisible(briefingPanel, true), false, "hud-mission-brief");
            objectivesPanel.Add(missionTitleRow);
            missionTitleText = AddText(objectivesPanel, "MISSION 01", "hud-mission-title", 29);
            missionDescriptionText = AddText(objectivesPanel, "Bring the outpost online and keep critical systems stable.", "body-copy", 18);
            objectiveText = AddText(objectivesPanel, "", "hud-objectives-list", 18);
            activeObjectiveText = AddText(objectivesPanel, "", "hud-active-objective", 15);
            VisualElement progressRow = new VisualElement();
            progressRow.AddToClassList("hud-progress-row");
            AddText(progressRow, "MISSION PROGRESS", "panel-kicker", 14);
            progressText = AddText(progressRow, "0 / 0", "hud-progress-value", 15);
            objectivesPanel.Add(progressRow);
            VisualElement progressTrack = new VisualElement();
            progressTrack.AddToClassList("hud-progress-track");
            progressFillElement = new VisualElement();
            progressFillElement.AddToClassList("hud-progress-fill");
            progressTrack.Add(progressFillElement);
            objectivesPanel.Add(progressTrack);
            AddButton(objectivesPanel, "OPEN CHECKLIST  /  V", ToggleObjectiveList, false, "hud-open-checklist");

            bottomBar = new VisualElement();
            bottomBar.AddToClassList("hud-bottombar");
            AddText(bottomBar, "WASD / LEFT STICK MOVE    SHIFT / LB SPRINT    SPACE / A HOP    E / X USE    MOUSE / RIGHT STICK LOOK", "hud-controls", 13);
            AddText(bottomBar, "COMMS  /  STANDBY", "hud-comms", 14);
            pauseButton = AddButton(bottomBar, "Ⅱ  PAUSE", () => game?.TogglePause(), false, "hud-pause");
            hudLayer.Add(bottomBar);

            facilityPanel = Card(hudLayer, "hud-facility");
            AddText(facilityPanel, "FACILITY IN RANGE", "panel-kicker", 14);
            facilityText = AddText(facilityPanel, "", "body-copy", 17);
            facilityPanel.style.display = DisplayStyle.None;

            resourceAlertPanel = new VisualElement();
            resourceAlertPanel.AddToClassList("hud-resource-alert");
            resourceAlertText = AddText(resourceAlertPanel, "", "hud-warning-copy", 16);
            hudLayer.Add(resourceAlertPanel);
            resourceAlertPanel.style.display = DisplayStyle.None;

            messageBanner = new VisualElement();
            messageBanner.AddToClassList("hud-message");
            messageText = AddText(messageBanner, "", "hud-message-copy", 18);
            hudLayer.Add(messageBanner);
            messageBanner.style.display = DisplayStyle.None;

            objectiveListPanel = Card(hudLayer, "hud-objective-list-card");
            AddText(objectiveListPanel, "MISSION CHECKLIST  /  V TO CLOSE", "panel-kicker", 15);
            objectiveChecklistText = AddText(objectiveListPanel, "", "hud-objectives-list", 17);
            objectiveListPanel.style.display = DisplayStyle.None;

            touchControls = new VisualElement();
            touchControls.AddToClassList("hud-touch-controls");
            AddButton(touchControls, "HOP", () => FindAnyObjectByType<AstraPlayerController>()?.QueueTouchJump(), false, "hud-hop");
            touchUseButton = AddButton(touchControls, "USE", () => FindAnyObjectByType<AstraPlayerController>()?.QueueTouchInteract(), true, "hud-use");
            touchExitRoverButton = AddButton(touchControls, "EXIT ROVER", () => FindAnyObjectByType<AstraPlayerController>()?.QueueTouchRoverExit(), false, "hud-exit-rover");
            touchControls.name = "touch-controls";
            hudLayer.Add(touchControls);
            touchControls.style.display = DisplayStyle.None;

            AddButton(objectivesPanel, "OPEN CHECKLIST  /  V", ToggleObjectiveList, false, "hud-checklist");
        }

        private VisualElement progressFillElement;

        private void BuildMissionMap()
        {
            missionMap = new VisualElement { name = "mission-radar" };
            missionMap.AddToClassList("mission-radar");
            AddText(missionMap, "N", "radar-north", 12);
            AddText(missionMap, "E", "radar-east", 12);
            AddText(missionMap, "S", "radar-south", 12);
            AddText(missionMap, "W", "radar-west", 12);
            VisualElement vertical = new VisualElement();
            vertical.AddToClassList("radar-axis-vertical");
            missionMap.Add(vertical);
            VisualElement horizontal = new VisualElement();
            horizontal.AddToClassList("radar-axis-horizontal");
            missionMap.Add(horizontal);
            VisualElement playerDot = new VisualElement();
            playerDot.AddToClassList("radar-player");
            missionMap.Add(playerDot);
            AddText(missionMap, "MISSION EQUIPMENT", "radar-legend", 9);
            hudLayer.Add(missionMap);
            missionMap.style.display = DisplayStyle.None;

            waypointPanel = new VisualElement { name = "objective-waypoint" };
            waypointPanel.AddToClassList("hud-waypoint-guide");
            AddText(waypointPanel, "CURRENT OBJECTIVE TARGET", "hud-waypoint-title", 12);
            waypointText = AddText(waypointPanel, "Follow the direction to your next station.", "hud-waypoint-copy", 16);
            hudLayer.Add(waypointPanel);
            waypointPanel.style.display = DisplayStyle.None;
        }

        private void UpdateMissionMap()
        {
            if (missionMap == null || game == null) return;
            bool show = game.missionStarted && !game.missionFinished;
            SetVisible(missionMap, show);
            if (!show) return;

            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            if (player == null) return;
            AstraInteractable[] facilities = FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None);
            HashSet<Transform> active = new HashSet<Transform>();
            foreach (AstraInteractable facility in facilities)
            {
                if (facility == null || !IsMissionMapFacility(facility.facilityType, game.CurrentMission)) continue;
                SetMissionMapMarker(facility.transform, FacilityMapCode(facility.facilityType), player.transform, active);
            }

            AstraRoverDrive rover = FindAnyObjectByType<AstraRoverDrive>();
            if (rover != null && rover.SampleSite != null && (game.CurrentMission == AstraMissionId.FirstLight || game.CurrentMission == AstraMissionId.ShadowScout))
                SetMissionMapMarker(rover.SampleSite, "SMP", player.transform, active);

            Transform activeTarget = game.GetActiveObjectiveTarget();
            foreach (KeyValuePair<Transform, VisualElement> pair in missionMapMarkers)
                pair.Value.EnableInClassList("radar-target", pair.Key == activeTarget);

            List<Transform> stale = null;
            foreach (KeyValuePair<Transform, VisualElement> pair in missionMapMarkers)
            {
                if (pair.Key != null && active.Contains(pair.Key)) continue;
                pair.Value.RemoveFromHierarchy();
                if (stale == null) stale = new List<Transform>();
                stale.Add(pair.Key);
            }
            if (stale != null)
                foreach (Transform item in stale) missionMapMarkers.Remove(item);
        }

        private void SetMissionMapMarker(Transform target, string code, Transform player, HashSet<Transform> active)
        {
            if (target == null) return;
            active.Add(target);
            if (!missionMapMarkers.TryGetValue(target, out VisualElement marker))
            {
                marker = new VisualElement { name = "radar-" + code };
                marker.AddToClassList("radar-facility-marker");
                Label tag = new Label(code);
                tag.AddToClassList("radar-facility-code");
                marker.Add(tag);
                missionMap.Add(marker);
                missionMapMarkers.Add(target, marker);
            }
            Vector3 delta = target.position - player.position;
            const float range = 45f;
            marker.style.left = Length.Percent(50f + Mathf.Clamp(delta.x / range, -1f, 1f) * 38f);
            marker.style.top = Length.Percent(50f - Mathf.Clamp(delta.z / range, -1f, 1f) * 38f);
            marker.style.display = new Vector2(delta.x, delta.z).magnitude <= range ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsMissionMapFacility(FacilityType type, AstraMissionId mission)
        {
            switch (mission)
            {
                case AstraMissionId.FirstLight:
                    return type == FacilityType.Communications || type == FacilityType.SolarArray || type == FacilityType.LifeSupport || type == FacilityType.Rover || type == FacilityType.RadiationShelter;
                case AstraMissionId.AirAndWater:
                    return type == FacilityType.LifeSupport || type == FacilityType.SolarArray || type == FacilityType.WaterRecycler || type == FacilityType.OxygenProcessor;
                case AstraMissionId.ShadowScout:
                    return type == FacilityType.Rover || type == FacilityType.ResearchLab;
                case AstraMissionId.StormWatch:
                    return type == FacilityType.SpaceWeatherStation || type == FacilityType.RadiationShelter || type == FacilityType.Communications || type == FacilityType.LifeSupport || type == FacilityType.SolarArray;
                case AstraMissionId.FoodForTheLongStay:
                    return type == FacilityType.Greenhouse || type == FacilityType.LifeSupport || type == FacilityType.SolarArray || type == FacilityType.Habitat;
                default: return false;
            }
        }

        private static string FacilityMapCode(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return "COM";
                case FacilityType.SolarArray: return "SOL";
                case FacilityType.LifeSupport: return "LIFE";
                case FacilityType.Rover: return "RVR";
                case FacilityType.RadiationShelter: return "SAFE";
                case FacilityType.WaterRecycler: return "H2O";
                case FacilityType.OxygenProcessor: return "O2";
                case FacilityType.SpaceWeatherStation: return "WX";
                case FacilityType.Greenhouse: return "GH";
                case FacilityType.Habitat: return "HAB";
                case FacilityType.ResearchLab: return "LAB";
                default: return "OBJ";
            }
        }

        private void UpdateWaypointGuide()
        {
            if (waypointPanel == null || game == null) return;
            Transform target = game.GetActiveObjectiveTarget();
            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            bool show = game.CanControlPlayer && target != null && player != null;
            SetVisible(waypointPanel, show);
            if (!show) return;

            AstraInteractable targetFacility = target.GetComponent<AstraInteractable>();
            if (targetFacility == null) targetFacility = target.GetComponentInParent<AstraInteractable>();
            AstraInteractable nearest = player.GetNearestFacility();
            AstraRoverDrive rover = player.IsMountedInRover ? player.MountedRover : null;
            bool atSample = rover != null && rover.IsAtSample && target == rover.SampleSite;
            bool inRange = atSample || (targetFacility != null && nearest == targetFacility);
            if (inRange)
            {
                waypointText.text = (atSample ? "SAMPLE SITE REACHED" : targetFacility.facilityName.ToUpperInvariant()) + "  ·  PRESS E / X TO INTERACT";
                return;
            }

            Vector3 offset = target.position - player.transform.position;
            Vector3 flatOffset = new Vector3(offset.x, 0f, offset.z);
            Vector3 flatForward = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            float angle = Vector3.SignedAngle(flatForward.normalized, flatOffset.normalized, Vector3.up);
            string turn = Mathf.Abs(angle) < 22.5f ? "AHEAD" :
                Mathf.Abs(angle) < 67.5f ? (angle > 0f ? "AHEAD-RIGHT" : "AHEAD-LEFT") :
                Mathf.Abs(angle) < 112.5f ? (angle > 0f ? "RIGHT" : "LEFT") :
                Mathf.Abs(angle) < 157.5f ? (angle > 0f ? "BEHIND-RIGHT" : "BEHIND-LEFT") : "TURN AROUND";
            float bearingAngle = Mathf.Repeat(Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg, 360f);
            string[] bearings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            string bearing = bearings[Mathf.RoundToInt(bearingAngle / 45f) % bearings.Length];
            string targetName = target == (rover != null ? rover.SampleSite : null) ? "VIRTUAL SAMPLE SITE" :
                targetFacility != null ? targetFacility.facilityName.ToUpperInvariant() : "MISSION OBJECTIVE";
            waypointText.text = targetName + "  ·  " + turn + "  ·  " + bearing + "  ·  " + Mathf.CeilToInt(flatOffset.magnitude) + " m";
        }

        private void BuildResource(VisualElement parent, string title, string key)
        {
            VisualElement tile = new VisualElement();
            tile.AddToClassList("hud-resource-tile");
            VisualElement line = new VisualElement();
            line.AddToClassList("hud-resource-heading");
            AddText(line, title, "hud-resource-name", 15);
            Label value = AddText(line, "100%", "hud-resource-value", 17);
            tile.Add(line);
            VisualElement track = new VisualElement();
            track.AddToClassList("hud-resource-track");
            VisualElement fill = new VisualElement();
            fill.AddToClassList("hud-resource-fill");
            track.Add(fill);
            tile.Add(track);
            parent.Add(tile);
            resourceFills[key] = fill;
            resourceLabels[key] = value;
        }

        private readonly Dictionary<string, Label> resourceLabels = new Dictionary<string, Label>();

        private void BuildMissionOverlays()
        {
            introPanel = Overlay("mission-intro", "JUNIOR ASTRONAUT MISSION TRAINER", "ASTRA  /  FIRST LIGHT",
                "Your crew has arrived at Shackleton Outpost. Bring communications and solar power online, inspect life support, then make a call when the array output drops.");
            if (PlayerPrefs.HasKey("ASTRA_TUTORIAL_STATUS"))
                AddText(introPanel, "Welcome back — choose the guide or jump straight in.", "body-copy", 18);
            VisualElement introActions = Row(introPanel);
            AddButton(introActions, "SHOW ME HOW TO PLAY", () => ShowTutorialStep(0), true);
            AddButton(introActions, "I KNOW HOW TO PLAY — SKIP GUIDE", SkipTutorial, false);
            AddText(introPanel, "KEYBOARD: WASD move · Shift sprint · Space hop · E use · right-drag look\nGAMEPAD: left stick move · right stick look · LB sprint · A hop · X use · Start pause\nJOYSTICK: main stick move · twist look · trigger use", "hud-overlay-footnote", 14);

            tutorialPanel = Overlay("quick-guide", "ASTRA  /  QUICK GUIDE", "", "");
            tutorialCounterText = AddText(tutorialPanel, "STEP 1 OF 3", "panel-kicker", 16);
            tutorialTitleText = AddText(tutorialPanel, "", "hud-overlay-title", 36);
            tutorialBodyText = AddText(tutorialPanel, "", "body-copy", 22);
            tutorialKeysText = AddText(tutorialPanel, "", "hud-guide-keys", 17);
            VisualElement guideActions = Row(tutorialPanel);
            AddButton(guideActions, "BACK", () => ShowTutorialStep(tutorialStep - 1), false);
            AddButton(guideActions, "SKIP GUIDE", SkipTutorial, false);
            guideNextButton = AddButton(guideActions, "NEXT  →", AdvanceTutorial, true);
            tutorialPanel.style.display = DisplayStyle.None;

            pausePanel = Overlay("pause-overlay", "ASTRA  /  FLIGHT PAUSED", "MISSION PAUSED", "Your mission is paused. Resume when you are ready.");
            VisualElement pauseActions = Column(pausePanel);
            AddButton(pauseActions, "RESUME MISSION", () => game?.TogglePause(), true);
            AddButton(pauseActions, "RESTART MISSION", () => game?.RestartMission(), false);
            AddButton(pauseActions, "ACCESSIBILITY SETTINGS", () => OnboardingManager.Instance?.ShowSettingsFromPause(), false);
            pausePanel.style.display = DisplayStyle.None;

            reportPanel = Overlay("debrief-overlay", "ASTRA  /  FLIGHT DEBRIEF", "MISSION COMPLETE", "");
            reportTitleText = reportPanel.Q<Label>(className: "hud-overlay-title");
            reportScoreText = AddText(reportPanel, "MISSION SCORE  000", "hud-score", 21);
            reportBodyText = AddText(reportPanel, "", "body-copy", 20);
            reportLearningText = AddText(reportPanel, "WHAT YOU LEARNED", "hud-learning", 18);
            VisualElement reportActions = Column(reportPanel);
            launchDestinationButton = AddButton(reportActions, "REPAIR SHIP & LAUNCH", () => game?.StartNextDestinationLaunch(), true);
            launchDestinationButton.style.display = DisplayStyle.None;
            AddButton(reportActions, "REPLAY MISSION", () => game?.RestartMission(), false);
            AddButton(reportActions, "MISSION BOARD", ReturnToMissionBoard, false);
            reportPanel.style.display = DisplayStyle.None;

            briefingPanel = Overlay("briefing-overlay", "SCIENCE  /  CHALLENGE DATA", "MISSION BRIEFING", "");
            briefingBodyText = AddText(briefingPanel, "Mission learning note appears here.", "body-copy", 19);
            scienceDataText = AddText(briefingPanel, "Loading science context…", "hud-science-copy", 17);
            VisualElement briefingActions = Row(briefingPanel);
            AddButton(briefingActions, "OPEN MISSION SOURCE", OpenMissionSource, false);
            AddButton(briefingActions, "OPEN CHALLENGE PAGE", () => Application.OpenURL("https://www.spaceappschallenge.org/2026/challenges/junior-astronaut-mission-trainer/?tab=resources"), false);
            AddButton(briefingActions, "BACK TO MISSION", () => SetOverlayVisible(briefingPanel, false), true);
            AddButton(briefingPanel, "REPLAY GUIDE", () => ShowTutorialStep(0), false);
            briefingPanel.style.display = DisplayStyle.None;

            eventPanel = Overlay("event-overlay", "TRAINING SCENARIO  /  NOT A LIVE WARNING", "SPACE-WEATHER DRILL", "");
            eventCopyText = AddText(eventPanel, "", "body-copy", 19);
            VisualElement eventActions = Column(eventPanel);
            eventPrimaryButton = AddButton(eventActions, "SHELTER CREW", () => game?.ResolveRadiationDrill(true), true);
            eventSecondaryButton = AddButton(eventActions, "KEEP OUTSIDE OPERATIONS", () => game?.ResolveRadiationDrill(false), false);
            AddText(eventPanel, "FICTIONAL EXPOSURE INDEX  ·  NOT A DOSE OR MEDICAL MEASUREMENT", "hud-overlay-footnote", 14);
            eventPanel.style.display = DisplayStyle.None;

            stormPanel = Overlay("storm-overlay", "SPACE-WEATHER REPORT  /  DONKI", "SPACE-WEATHER REPORT", "");
            stormTitleText = stormPanel.Q<Label>(className: "hud-overlay-title");
            stormBodyText = AddText(stormPanel, "Loading event information…", "body-copy hud-storm-copy", 19);
            VisualElement stormActions = Row(stormPanel);
            stormLinkButton = AddButton(stormActions, "OPEN NASA DONKI EVENT", () => stormLinkAction?.Invoke(), false);
            stormActionButton = AddButton(stormActions, "I HAVE REVIEWED THIS REPORT", () => stormActionAction?.Invoke(), true);
            AddButton(stormPanel, "CLOSE", HideStormPanel, false);
            stormPanel.style.display = DisplayStyle.None;
        }

        private Button guideNextButton;

        private VisualElement Overlay(string name, string kicker, string title, string body)
        {
            VisualElement shade = new VisualElement { name = name };
            shade.AddToClassList("hud-overlay-shade");
            VisualElement panel = Card(shade, "hud-overlay-card");
            AddText(panel, kicker, "panel-kicker", 15);
            AddText(panel, title, "hud-overlay-title", 36);
            if (!string.IsNullOrEmpty(body)) AddText(panel, body, "body-copy", 20);
            hudLayer.Add(shade);
            shade.style.display = DisplayStyle.None;
            return panel;
        }

        private VisualElement Row(VisualElement parent)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("button-row");
            parent.Add(row);
            return row;
        }

        private VisualElement Column(VisualElement parent)
        {
            VisualElement column = new VisualElement();
            column.AddToClassList("hud-action-stack");
            parent.Add(column);
            return column;
        }

        public void Refresh(AstraGameManager state)
        {
            if (state == null || !built) return;
            int missionNumber = (int)state.CurrentMission + 1;
            missionCodeText.text = AstraMissionCatalog.DestinationName(state.MissionDefinition.Destination) + " OUTPOST  ·  MISSION " + missionNumber.ToString("00") + " / " + AstraMissionCatalog.Count.ToString("00");
            missionTitleText.text = "MISSION " + missionNumber.ToString("00") + "  /  " + AstraMissionCatalog.DestinationName(state.MissionDefinition.Destination);
            clockText.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(state.missionSeconds / 60f), Mathf.FloorToInt(state.missionSeconds % 60f));
            statusText.text = "OUTPOST  /  " + state.GetOutpostStatus() + "   ·   HAB " + Mathf.RoundToInt(state.habitatCondition) + "%";
            SetResource("oxygen", state.oxygen);
            SetResource("water", state.water);
            SetResource("food", state.food);
            SetResource("power", state.power);
            missionDescriptionText.text = state.MissionDefinition.Subtitle;
            briefingBodyText.text = state.MissionDefinition.Lesson;
            if (state.CurrentMission == AstraMissionId.FirstLight)
                scienceDataText.text = state.MissionDefinition.SourceLabel + " is the reference for this mission. Communications, solar power, and habitat readings are simplified game systems.";
            else if (state.CurrentMission == AstraMissionId.AirAndWater)
                scienceDataText.text = "NASA ECLSS links water recovery and oxygen generation. Recover water before running electrolysis; all ASTRA readings are fictional game values.";
            else if (state.CurrentMission == AstraMissionId.StormWatch)
                scienceDataText.text = state.DonkiSummary + "\nDONKI SEP context is not a Mars dust forecast. The local storm drill and its exposure index are fictional; the index is not dose.";
            else
                scienceDataText.text = state.MissionDefinition.SourceLabel + " is the reference for this mission. All local resource amounts are simplified game values.";

            string resourceWarning = state.Resources != null ? state.Resources.GetWarning() : string.Empty;
            bool showWarning = state.missionStarted && !state.missionFinished && !string.IsNullOrEmpty(resourceWarning);
            SetVisible(resourceAlertPanel, showWarning);
            if (showWarning)
            {
                bool critical = resourceWarning.StartsWith("CRITICAL") || resourceWarning.StartsWith("HABITAT CONDITION LOW");
                resourceAlertPanel.EnableInClassList("warning-critical", critical);
                resourceAlertText.text = (critical ? "⚠  CRITICAL  ·  " : "⚠  RESOURCE ADVISORY  ·  ") + resourceWarning;
            }

            StringBuilder list = new StringBuilder();
            int complete = 0;
            string nextObjective = "Mission objectives complete. Return to Mission Control.";
            bool activeShown = false;
            bool noRead = PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.NoReadingMode;
            string[] icons = { "◉", "☀", "◎", "ϟ", "◉" };
            for (int i = 0; i < state.Objectives.Count; i++)
            {
                MissionObjective objective = state.Objectives[i];
                if (objective.complete) complete++;
                string item = noRead ? icons[Mathf.Min(i, icons.Length - 1)] : objective.title;
                list.Append(objective.complete ? "✓  " : !activeShown ? "▶  " : "○  ").Append(item);
                if (!objective.complete && !activeShown) { activeShown = true; nextObjective = objective.title; }
                if (i < state.Objectives.Count - 1) list.Append('\n');
            }
            objectiveText.text = list.ToString();
            activeObjectiveText.text = noRead ? "CURRENT TASK  ·  " + nextObjective : nextObjective;
            progressText.text = complete + " / " + state.Objectives.Count;
            progressFillElement.style.width = Length.Percent(state.Objectives.Count == 0 ? 0f : 100f * complete / state.Objectives.Count);

            SetVisible(touchControls, state.missionStarted && !state.missionFinished);
            if (pauseButton != null) pauseButton.SetEnabled(state.missionStarted && !state.missionFinished);
            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            bool mounted = player != null && player.IsMountedInRover;
            SetVisible(touchExitRoverButton, mounted && state.missionStarted && !state.missionFinished);
            if (touchUseButton != null) touchUseButton.text = mounted ? "COLLECT SAMPLE" : "INTERACT";
        }

        private void SetResource(string key, float amount)
        {
            if (resourceLabels.TryGetValue(key, out Label value)) value.text = Mathf.RoundToInt(amount) + "%";
            if (resourceFills.TryGetValue(key, out VisualElement fill))
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(amount / 100f) * 100f);
                fill.EnableInClassList("resource-low", amount < 35f);
                fill.EnableInClassList("resource-critical", amount < 15f);
            }
        }

        private void UpdateFacilityPrompt()
        {
            AstraPlayerController player = FindAnyObjectByType<AstraPlayerController>();
            if (player != null && player.IsMountedInRover)
            {
                AstraRoverDrive drive = player.MountedRover;
                SetVisible(facilityPanel, game != null && game.CanControlPlayer);
                if (drive != null)
                    facilityText.text = "ROVER NAVIGATION  ·  W/S DRIVE  ·  A/D STEER  ·  Q/B EXIT\n" + (drive.IsAtSample
                        ? "Sample site reached. Press E / X to collect the virtual sample."
                        : "Drive to the orange beacon: " + Mathf.CeilToInt(drive.DistanceToSample) + " m remaining.");
                return;
            }
            AstraInteractable nearest = player != null ? player.GetNearestFacility() : null;
            SetVisible(facilityPanel, game != null && game.CanControlPlayer && nearest != null);
            if (nearest != null) facilityText.text = nearest.facilityName + "  ·  Press E / X to inspect, choose, and act";
        }

        private void ShowTutorialStep(int step)
        {
            if (step == 0 && !resumeMissionAfterTutorial && game != null && game.CanControlPlayer)
            {
                resumeMissionAfterTutorial = true;
                game.TogglePause();
                SetOverlayVisible(pausePanel, false);
            }
            tutorialStep = Mathf.Clamp(step, 0, TutorialTitles.Length - 1);
            tutorialTitleText.text = TutorialTitles[tutorialStep];
            tutorialBodyText.text = TutorialBodies[tutorialStep];
            tutorialKeysText.text = TutorialKeys[tutorialStep];
            tutorialCounterText.text = "STEP " + (tutorialStep + 1) + " OF " + TutorialTitles.Length;
            guideNextButton.text = tutorialStep == TutorialTitles.Length - 1 ? "I'M READY — START" : "NEXT  →";
            SetOverlayVisible(introPanel, false);
            SetOverlayVisible(briefingPanel, false);
            SetOverlayVisible(tutorialPanel, true);
        }

        private void SkipTutorial()
        {
            PlayerPrefs.SetString("ASTRA_TUTORIAL_STATUS", "skipped");
            PlayerPrefs.Save();
            SetOverlayVisible(tutorialPanel, false);
            SetOverlayVisible(introPanel, false);
            FinishTutorialAndContinue();
        }

        private void AdvanceTutorial()
        {
            if (tutorialStep < TutorialTitles.Length - 1) ShowTutorialStep(tutorialStep + 1);
            else
            {
                PlayerPrefs.SetString("ASTRA_TUTORIAL_STATUS", "completed");
                PlayerPrefs.Save();
                SetOverlayVisible(tutorialPanel, false);
                FinishTutorialAndContinue();
            }
        }

        private void FinishTutorialAndContinue()
        {
            if (resumeMissionAfterTutorial)
            {
                resumeMissionAfterTutorial = false;
                game?.TogglePause();
            }
            else if (game != null) game.BeginMission(game.CurrentMission);
        }

        public void ToggleObjectiveList()
        {
            objectiveListVisible = !objectiveListVisible;
            SetVisible(objectiveListPanel, objectiveListVisible);
            if (objectiveListVisible) objectiveChecklistText.text = objectiveText.text;
        }

        public void SetIntroVisible(bool visible) => SetOverlayVisible(introPanel, visible);
        public void SetPauseVisible(bool visible) => SetOverlayVisible(pausePanel, visible);

        public void SetEventVisible(bool visible)
        {
            SetOverlayVisible(eventPanel, visible);
            bool stormWatch = visible && game != null && game.CurrentMission == AstraMissionId.StormWatch;
            eventPrimaryButton.text = stormWatch ? "PAUSE OUTDOOR WORK NOW" : "SHELTER CREW  ·  SPEND POWER, LOWER SIMULATED EXPOSURE";
            if (stormWatch) eventSecondaryButton.text = "DELAY THE PAUSE  ·  10 SEC";
            SetVisible(eventSecondaryButton, visible);
            if (visible && game != null)
            {
                string destination = AstraMissionCatalog.DestinationName(game.MissionDefinition.Destination).ToLowerInvariant();
                eventCopyText.text = stormWatch
                    ? "Fictional Mars dust-storm drill, not a live forecast. Pause rover work now, or delay 10 seconds and spend 4 game battery units. Mission time keeps running either way; then guide the astronaut to shelter."
                    : "Training scenario only; this is not a live warning. NASA DONKI solar-particle records give event context, not this " + destination + " crew's dose. Shelter costs game power and adds less to ASTRA's fictional exposure index. Staying outside keeps power but adds more to that index.";
            }
        }

        public void ShowDonkiBriefing(string date, string type, string description, string url, bool dataReady, Action reviewed)
        {
            stormPanelShowsDonki = true;
            bool stormWatch = game != null && game.CurrentMission == AstraMissionId.StormWatch;
            stormTitleText.text = "SPACE-WEATHER CONTEXT  /  NASA DONKI";
            stormBodyText.text = DonkiBriefing(date, type, description) + (stormWatch
                ? "\n\nNEXT: ASTRA runs a separate fictional Mars dust-storm drill. DONKI does not forecast Mars dust storms."
                : string.Empty);
            stormActionButton.text = dataReady ? "I HAVE REVIEWED THIS CONTEXT" : "WAITING FOR DONKI DATA…";
            stormActionButton.SetEnabled(dataReady);
            stormActionAction = () => reviewed?.Invoke();
            bool hasLink = !string.IsNullOrWhiteSpace(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            stormLinkButton.text = "OPEN NASA DONKI EVENT";
            stormLinkButton.style.display = hasLink ? DisplayStyle.Flex : DisplayStyle.None;
            stormLinkAction = hasLink ? (Action)(() => Application.OpenURL(url)) : null;
            SetOverlayVisible(stormPanel, true);
        }

        public void ShowActivityChoice(string title, string body, string optionOne, string optionTwo, Action<int> selected)
        {
            stormPanelShowsDonki = false;
            stormTitleText.text = title;
            stormBodyText.text = body;
            stormLinkButton.text = optionOne;
            stormLinkButton.style.display = DisplayStyle.Flex;
            stormLinkButton.SetEnabled(true);
            stormLinkAction = () => selected?.Invoke(0);
            stormActionButton.text = optionTwo;
            stormActionButton.style.display = DisplayStyle.Flex;
            stormActionButton.SetEnabled(true);
            stormActionAction = () => selected?.Invoke(1);
            SetOverlayVisible(stormPanel, true);
        }

        public void RefreshDonkiBriefing(string date, string type, string description, string url, bool dataReady)
        {
            if (!stormPanelShowsDonki || stormPanel == null || !IsOverlayVisible(stormPanel)) return;
            bool marsDustScenario = game != null && game.CurrentMission == AstraMissionId.StormWatch;
            stormBodyText.text = marsDustScenario ? MarsDustStormBriefing(date, type, description) : DonkiBriefing(date, type, description);
            stormActionButton.SetEnabled(dataReady);
            stormActionButton.text = dataReady ? "I HAVE REVIEWED THIS REPORT" : marsDustScenario ? "LOADING SCENARIO…" : "WAITING FOR DONKI DATA…";
            bool hasLink = !string.IsNullOrWhiteSpace(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            stormLinkButton.style.display = hasLink ? DisplayStyle.Flex : DisplayStyle.None;
            stormLinkAction = hasLink ? (Action)(() => Application.OpenURL(url)) : null;
        }

        private static string DonkiBriefing(string date, string type, string description) =>
            "EVENT DATE (UTC)\n" + date + "\n\nEVENT TYPE\n" + type + "\n\nDESCRIPTION\n" + description +
            "\n\nWHY MONITOR IT?\nSpace-weather teams observe solar events so mission planners can assess changing conditions. DONKI records are event context; they do not state radiation dose or predict crew health at Mars. This drill is fictional.";

        private static string MarsDustStormBriefing(string date, string type, string description) =>
            "REPORT\n" + date + "\n\nSCENARIO\n" + type + "\n\nBRIEFING\n" + description +
            "\n\nDECISION\nChoose whether to pause the outdoor rover traverse now or after a 10-second delay. ASTRA displays the resulting fictional mission-time and battery changes; it is not an operational Mars forecast.";

        public void ShowStormInspectionReport(string checklist, bool ready, Action submit)
        {
            stormPanelShowsDonki = false;
            stormTitleText.text = "POST-DRILL INSPECTION REPORT";
            stormBodyText.text = "Visit each marked facility and check its status. When all five checks are complete, return here to submit.\n\n" + checklist +
                "\n\nThis training report records simulated system status; it is not a real Mars safety or radiation report.";
            stormActionButton.text = ready ? "SUBMIT POST-DRILL REPORT" : "INSPECTION INCOMPLETE";
            stormActionButton.SetEnabled(ready);
            stormActionAction = ready ? (Action)(() => submit?.Invoke()) : null;
            stormLinkButton.style.display = DisplayStyle.None;
            SetOverlayVisible(stormPanel, true);
        }

        public void HideStormPanel() => SetOverlayVisible(stormPanel, false);

        public void SetMissionHudVisible(bool visible) => SetVisible(root, visible);

        public void ShowMessage(string message)
        {
            if (messageText == null) return;
            messageText.text = message;
            SetVisible(messageBanner, true);
            StopCoroutine(nameof(HideMessage));
            StartCoroutine(HideMessage());
        }

        private IEnumerator HideMessage()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            if (messageBanner != null) SetVisible(messageBanner, false);
        }

        public void ShowReport(int score, bool success, string learning, AstraMissionDefinition definition, int bestScore, bool nextUnlocked)
        {
            reportTitleText.text = success ? definition.Title + " complete" : "Mission ended early";
            reportScoreText.text = "MISSION SCORE  " + score.ToString("000") + "     ·     BEST " + Mathf.Max(score, bestScore).ToString("000");
            bool launchAvailable = success && game != null && game.CanLaunchNextDestination;
            reportBodyText.text = success
                ? launchAvailable
                    ? "Mission cleared. Repair the spacecraft to launch to " + AstraMissionCatalog.DestinationName(AstraMissionCatalog.Get((AstraMissionId)((int)game.CurrentMission + 1)).Destination) + "."
                    : nextUnlocked ? "Mission cleared. The next campaign mission is unlocked on the Moon." : "Campaign complete. You can replay any mission from the board."
                : "Critical reserves were exhausted before this mission's goals were complete. Review your choices and try again.";
            reportLearningText.text = "WHAT YOU LEARNED\n" + learning;
            launchDestinationButton.style.display = launchAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            if (launchAvailable)
            {
                AstraMissionId next = (AstraMissionId)((int)game.CurrentMission + 1);
                launchDestinationButton.text = "REPAIR SHIP & LAUNCH TO " + AstraMissionCatalog.DestinationName(AstraMissionCatalog.Get(next).Destination);
            }
            SetOverlayVisible(reportPanel, true);
        }

        public void HideReport() => SetOverlayVisible(reportPanel, false);

        private void ReturnToMissionBoard()
        {
            SetOverlayVisible(reportPanel, false);
            OnboardingManager.Instance?.ShowMissionSelect();
        }

        private void OpenMissionSource()
        {
            if (game != null && !string.IsNullOrEmpty(game.MissionDefinition.SourceUrl)) Application.OpenURL(game.MissionDefinition.SourceUrl);
        }

        public void SetCursorForMission(bool missionActive)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void ApplyAccessibilitySettings()
        {
            if (root == null) return;
            PlayerSettingsManager settings = PlayerSettingsManager.Instance;
            float scale = settings != null ? settings.TextScale : 1f;
            foreach (KeyValuePair<TextElement, float> pair in baseTextSizes)
                if (pair.Key != null) pair.Key.style.fontSize = Mathf.Clamp(pair.Value * scale, 14f, 54f);
            root.EnableInClassList("high-contrast", settings != null && settings.HighContrast);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetOverlayVisible(VisualElement panel, bool visible)
        {
            if (panel == null) return;
            VisualElement shade = panel.parent != null && panel.parent.parent != null ? panel.parent.parent : panel;
            SetVisible(shade, visible);
        }

        private static bool IsOverlayVisible(VisualElement panel)
        {
            VisualElement shade = panel != null && panel.parent != null && panel.parent.parent != null ? panel.parent.parent : panel;
            return shade != null && shade.style.display != DisplayStyle.None;
        }
    }
}
