using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Astra
{
    /// <summary>Runtime UI Toolkit screens for ASTRA onboarding, settings, tutorial, and mission selection.</summary>
    public sealed class AstraOnboardingUI : MonoBehaviour
    {
        public string CurrentScreen { get; private set; } = "welcome";

        [SerializeField] private Texture2D menuBackground;

        private UIDocument document;
        private PanelSettings runtimePanelSettings;
        private VisualElement root;
        private VisualElement welcomePage, settingsPage, controlsPage, tutorialPage, completePage, missionPage;
        private VisualElement equipmentCard;
        private Label welcomeTitle, welcomeBody, setupLabel;
        private Button languageValue, readingValue, assistanceValue, noReadingValue, textSizeValue;
        private Button subtitlesValue, contrastValue, volumeValue, clickMoveValue;
        private Label controlsStatus;
        private Label tutorialStepText, tutorialTitle, tutorialBody, tutorialIcon, tutorialSpeech, tutorialHint, tutorialNextLabel;
        private Label equipmentIcon, equipmentBody, completeTitle, completeSpeech;
        private Label missionDetailTitle, missionDestinationText, missionDetailSummary, missionDetailStatus, missionBriefingText;
        private Button missionSourceButton, launchSelectedMissionButton;
        private Camera menuCamera;
        private Transform menuAstronaut;
        private Vector3 menuCameraPosition = new Vector3(0f, 4.8f, -14f);
        private Vector3 menuCameraFocus = new Vector3(0f, 2.2f, 6f);
        private Quaternion menuAstronautRestRotation;
        private Vector3 menuAstronautRestPosition;
        private readonly Button[] missionButtons = new Button[AstraMissionCatalog.Count];
        private readonly Label[] missionButtonLabels = new Label[AstraMissionCatalog.Count];
        private readonly Dictionary<AstraBinding, Button> bindingButtons = new Dictionary<AstraBinding, Button>();
        private readonly Dictionary<TextElement, float> baseSizes = new Dictionary<TextElement, float>();
        private string missionBriefingUrl;
        private string settingsReturn = "welcome";
        private int equipmentIndex;

        private static readonly string[] EquipmentKeys =
        {
            "equipment.oxygen", "equipment.water", "equipment.solar", "equipment.food", "equipment.comms"
        };

        private static readonly string[] EquipmentFallback =
        {
            "OXYGEN · This system adds breathable air in our game.",
            "WATER · The crew needs water to drink.",
            "SUN · Solar panels make game power from sunlight.",
            "FOOD · The greenhouse grows food for the crew.",
            "RADIO · Communications let the outpost talk to Earth."
        };

        private void Awake() => Build();

        private void OnEnable()
        {
            if (VoiceGuideManager.Instance != null) VoiceGuideManager.Instance.LineChanged += SetSpeech;
            if (PlayerSettingsManager.Instance != null) PlayerSettingsManager.Instance.RebindFinished += SetControlsStatus;
        }

        private void OnDisable()
        {
            if (VoiceGuideManager.Instance != null) VoiceGuideManager.Instance.LineChanged -= SetSpeech;
            if (PlayerSettingsManager.Instance != null) PlayerSettingsManager.Instance.RebindFinished -= SetControlsStatus;
        }

        private void OnDestroy()
        {
            if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
        }

        private void Update()
        {
            if (CurrentScreen == "controls") RefreshBindingCaptions();
            // Keep the title screen alive with a slow camera drift and a real NASA
            // astronaut model turning gently in the lunar scene.
            if (CurrentScreen != "welcome") return;
            if (menuCamera == null) menuCamera = Camera.main;
            if (menuCamera != null)
            {
                float orbit = Mathf.Sin(Time.unscaledTime * 0.12f) * 1.1f;
                float lift = Mathf.Sin(Time.unscaledTime * 0.18f) * 0.12f;
                Vector3 driftedPosition = menuCameraPosition + new Vector3(orbit, lift, 0f);
                menuCamera.transform.position = Vector3.Lerp(menuCamera.transform.position, driftedPosition, Time.unscaledDeltaTime * 2f);
                Vector3 focus = menuCameraFocus + new Vector3(0f, lift * 0.25f, 0f);
                menuCamera.transform.rotation = Quaternion.Slerp(menuCamera.transform.rotation,
                    Quaternion.LookRotation(focus - menuCamera.transform.position), Time.unscaledDeltaTime * 2f);
            }
            if (menuAstronaut != null)
            {
                float idleTurn = Mathf.Sin(Time.unscaledTime * 0.28f) * 5.5f;
                float idleSway = Mathf.Sin(Time.unscaledTime * 0.55f) * 0.025f;
                menuAstronaut.localRotation = menuAstronautRestRotation * Quaternion.Euler(0f, idleTurn, 0f);
                Vector3 position = menuAstronautRestPosition;
                position.y += idleSway;
                menuAstronaut.localPosition = position;
            }
        }

        private void Build()
        {
            GameObject documentObject = new GameObject("ASTRA UI Toolkit Document");
            documentObject.transform.SetParent(transform, false);
            document = documentObject.AddComponent<UIDocument>();

            runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanelSettings.name = "ASTRA Runtime Panel Settings";
            runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            runtimePanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            runtimePanelSettings.match = 0.5f;
            runtimePanelSettings.sortingOrder = 30;
            runtimePanelSettings.targetDisplay = 0;
            document.panelSettings = runtimePanelSettings;
            document.sortingOrder = 30;

            root = document.rootVisualElement;
            root.Clear();
            root.name = "astra-root";
            root.AddToClassList("astra-root");
            StyleSheet theme = Resources.Load<StyleSheet>("AstraOnboardingTheme");
            if (theme != null) root.styleSheets.Add(theme);
            else Debug.LogWarning("ASTRA UI Toolkit theme not found at Assets/Resources/AstraOnboardingTheme.uss; using built-in fallback styles.");

            VisualElement sceneBackdrop = new VisualElement { name = "scene-background" };
            sceneBackdrop.AddToClassList("scene-background");
            if (menuBackground != null) sceneBackdrop.style.backgroundImage = new StyleBackground(menuBackground);
            root.Add(sceneBackdrop);

            VisualElement shade = new VisualElement { name = "world-shade" };
            shade.AddToClassList("world-shade");
            root.Add(shade);

            BuildWelcome();
            BuildSettings();
            BuildControls();
            BuildTutorial();
            BuildComplete();
            BuildMissionSelect();
            BuildMenuAstronaut();
            HideAll();
            ApplyAccessibilitySettings();
        }

        private VisualElement NewPage(string name, string kicker, string title, out VisualElement content)
        {
            VisualElement page = new VisualElement { name = name };
            page.AddToClassList("page");
            page.AddToClassList(name);
            VisualElement header = new VisualElement();
            header.AddToClassList("page-header");
            AddLabel(header, kicker, "kicker", 16);
            AddLabel(header, title, "page-title", 42);
            page.Add(header);
            content = new VisualElement();
            content.AddToClassList("page-content");
            page.Add(content);
            root.Add(page);
            return page;
        }

        private VisualElement Glass(VisualElement parent, string extraClass = null)
        {
            VisualElement shell = new VisualElement();
            shell.AddToClassList("glass-shell");
            if (!string.IsNullOrEmpty(extraClass)) shell.AddToClassList(extraClass + "-shell");
            VisualElement glow = new VisualElement();
            glow.AddToClassList("glass-glow");
            shell.Add(glow);
            VisualElement panel = new VisualElement();
            panel.AddToClassList("glass-panel");
            if (!string.IsNullOrEmpty(extraClass)) panel.AddToClassList(extraClass);
            shell.Add(panel);
            parent.Add(shell);
            return panel;
        }

        private Label AddLabel(VisualElement parent, string value, string className, float size)
        {
            Label label = new Label(value);
            if (!string.IsNullOrEmpty(className))
            {
                string[] classes = className.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string classToken in classes) label.AddToClassList(classToken);
            }
            label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            baseSizes[label] = size;
            return label;
        }

        private Button AddButton(VisualElement parent, string caption, Action action, bool primary = false, string name = null)
        {
            Button button = new Button { text = caption, name = name ?? caption };
            button.AddToClassList("astra-button");
            button.AddToClassList(primary ? "button-primary" : "button-secondary");
            baseSizes[button] = 18f;
            button.clicked += () => action?.Invoke();
            parent.Add(button);
            return button;
        }

        private void BuildWelcome()
        {
            welcomePage = new VisualElement { name = "welcome-page" };
            welcomePage.AddToClassList("page");
            VisualElement header = new VisualElement();
            header.AddToClassList("brand-row");
            AddLabel(header, "ASTRA  /  MISSION CONTROL", "brand", 20);
            AddLabel(header, "EXPLORE  ·  LEARN  ·  ACHIEVE", "brand-tagline", 14);
            welcomePage.Add(header);

            VisualElement columns = new VisualElement();
            columns.AddToClassList("welcome-columns");
            VisualElement intro = new VisualElement();
            intro.AddToClassList("welcome-copy");
            AddLabel(intro, "NASA-INSPIRED JUNIOR ASTRONAUT TRAINER", "kicker", 16);
            welcomeTitle = AddLabel(intro, "ASTRA", "hero-title", 104);
            welcomeBody = AddLabel(intro, "EXPLORE  ·  LEARN  ·  ACHIEVE", "hero-copy", 24);
            setupLabel = AddLabel(intro, "MISSION 01   /   EARTH TO THE MOON", "hero-caption", 16);
            AddLabel(intro, "TRAIN FOR YOUR FIRST LUNAR EXPEDITION", "hero-subcaption", 15);
            columns.Add(intro);

            VisualElement menu = Glass(columns, "welcome-card");
            AddLabel(menu, "MISSION CONTROL   /   FLIGHT READY", "panel-kicker", 14);
            AddLabel(menu, "Your first mission", "panel-title", 32);
            AddLabel(menu, "Prepare at the launch site, travel to the Moon, and bring Shackleton Outpost online. ASTRO can guide you, or you can jump straight in.", "body-copy", 18);
            VisualElement actions = new VisualElement();
            actions.AddToClassList("button-stack");
            AddButton(actions, "LET'S GET READY", () => OnboardingManager.Instance?.BeginSetup(), true, "lets-get-ready");
            AddButton(actions, "SKIP TUTORIAL", () => OnboardingManager.Instance?.SkipTutorial(), false, "skip-tutorial");
            AddButton(actions, "ACCESSIBILITY SETTINGS", () => OnboardingManager.Instance?.OpenSettings(), false, "open-accessibility");
            AddButton(actions, "LAUNCH FROM EARTH  /  MISSION 01", () => OnboardingManager.Instance?.LaunchFromEarth(), true, "launch-from-earth");
            menu.Add(actions);
            welcomePage.Add(columns);
            root.Add(welcomePage);
        }

        private void BuildMenuAstronaut()
        {
            GameObject display = new GameObject("ASTRA title scene - NASA astronaut");
            display.transform.position = new Vector3(-5.7f, 0f, 2.7f);
            menuAstronaut = display.transform;

            menuCamera = Camera.main;
            if (menuCamera != null)
            {
                menuCameraPosition = new Vector3(0f, 4.8f, -14f);
                menuCameraFocus = new Vector3(0f, 2.2f, 6f);
                menuCamera.transform.position = menuCameraPosition;
                menuCamera.transform.rotation = Quaternion.LookRotation(menuCameraFocus - menuCameraPosition);
            }
            display.transform.LookAt(new Vector3(menuCameraPosition.x, 1.05f, menuCameraPosition.z));
            menuAstronautRestRotation = display.transform.localRotation;
            menuAstronautRestPosition = display.transform.localPosition;

            AstraRuntimeModelLoader loader = AstraRuntimeModelLoader.Instance;
            if (loader == null) loader = FindFirstObjectByType<AstraRuntimeModelLoader>();
            if (loader == null) loader = new GameObject("ASTRA NASA model loader").AddComponent<AstraRuntimeModelLoader>();
            loader.LoadInto("Z2Spacesuit", menuAstronaut, 3.1f, null);
        }

        private void BuildSettings()
        {
            VisualElement content;
            settingsPage = NewPage("settings-page", "ASTRO  /  ACCESSIBILITY", "MAKE ASTRA FIT YOU", out content);
            VisualElement panel = Glass(content, "settings-card");
            AddLabel(panel, "Choose what helps. You can change these options whenever you like.", "body-copy", 18);
            VisualElement rows = new VisualElement();
            rows.AddToClassList("settings-rows");
            languageValue = AddSettingRow(rows, "LANGUAGE", CycleLanguage, "setting-language");
            readingValue = AddSettingRow(rows, "HOW ASTRO HELPS", CycleReadingMode, "setting-reading");
            assistanceValue = AddSettingRow(rows, "HELP LEVEL", CycleAssistance, "setting-assistance");
            noReadingValue = AddSettingRow(rows, "NO-READING MODE", ToggleNoReading, "setting-no-reading");
            textSizeValue = AddSettingRow(rows, "TEXT SIZE", () => AdjustText(0.15f), "setting-text-size");
            subtitlesValue = AddSettingRow(rows, "SUBTITLES", ToggleSubtitles, "setting-subtitles");
            contrastValue = AddSettingRow(rows, "HIGH CONTRAST", ToggleContrast, "setting-contrast");
            volumeValue = AddSettingRow(rows, "VOICE VOLUME", () => AdjustVolume(0.1f), "setting-volume");
            clickMoveValue = AddSettingRow(rows, "CLICK-TO-WALK", ToggleClickMove, "setting-click-move");
            panel.Add(rows);
            VisualElement footer = new VisualElement();
            footer.AddToClassList("button-row");
            AddButton(footer, "KEYBOARD / GAMEPAD CONTROLS", ShowControls, false, "open-controls");
            AddButton(footer, "BACK", () => OnboardingManager.Instance?.CloseSettings(), false, "settings-back");
            AddButton(footer, "SAVE AND CONTINUE", () => OnboardingManager.Instance?.CloseSettings(), true, "settings-save");
            panel.Add(footer);
            UpdateSettingsValues();
        }

        private Button AddSettingRow(VisualElement parent, string title, Action action, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("setting-row");
            AddLabel(row, title, "setting-label", 17);
            Button valueButton = AddButton(row, "...", action, false, name);
            valueButton.AddToClassList("setting-value");
            valueButton.style.fontSize = 16f;
            baseSizes[valueButton] = 16f;
            parent.Add(row);
            return valueButton;
        }

        private void BuildControls()
        {
            VisualElement content;
            controlsPage = NewPage("controls-page", "ASTRO  /  CHANGE CONTROLS", "KEYBOARD, GAMEPAD & JOYSTICK", out content);
            VisualElement panel = Glass(content, "controls-card");
            AddLabel(panel, "Gamepad: left stick move · right stick look · LB sprint · A hop · X use · Start pause.\nJoystick: main stick move · twist look · trigger use.", "body-copy", 17);
            AddBindingRow(panel, "WALK FORWARD", AstraBinding.MoveForward);
            AddBindingRow(panel, "WALK BACK", AstraBinding.MoveBack);
            AddBindingRow(panel, "WALK LEFT", AstraBinding.MoveLeft);
            AddBindingRow(panel, "WALK RIGHT", AstraBinding.MoveRight);
            AddBindingRow(panel, "USE / INTERACT", AstraBinding.Interact);
            controlsStatus = AddLabel(panel, "W A S D and E are ready.", "status-copy", 16);
            VisualElement footer = new VisualElement();
            footer.AddToClassList("button-row");
            AddButton(footer, "BACK TO ACCESSIBILITY", () => ShowSettings(settingsReturn), false, "controls-back");
            panel.Add(footer);
        }

        private void AddBindingRow(VisualElement parent, string title, AstraBinding binding)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("setting-row");
            AddLabel(row, title, "setting-label", 16);
            Button key = AddButton(row, "...", () => PlayerSettingsManager.Instance?.BeginRebind(binding), false, "binding-" + binding);
            key.AddToClassList("setting-value");
            bindingButtons[binding] = key;
            parent.Add(row);
        }

        private void BuildTutorial()
        {
            VisualElement content;
            tutorialPage = NewPage("tutorial-page", "ASTRO  /  PRACTICE GUIDE", "LEARN THE OUTPOST", out content);
            VisualElement left = Glass(content, "tutorial-card");
            left.AddToClassList("tutorial-left");
            tutorialStepText = AddLabel(left, "ACTIVITY 1 / 6", "panel-kicker", 14);
            tutorialTitle = AddLabel(left, "MEET YOUR ASTRONAUT", "panel-title", 28);
            tutorialBody = AddLabel(left, "Take your time. Every try helps you learn.", "body-copy", 19);
            tutorialHint = AddLabel(left, "", "hint-copy", 16);
            VisualElement navTop = new VisualElement();
            navTop.AddToClassList("button-row");
            AddButton(navTop, "BACK", () => OnboardingManager.Instance?.PreviousTutorialStep(), false, "tutorial-back");
            AddButton(navTop, "REPEAT ASTRO", () => OnboardingManager.Instance?.RepeatCurrentStep(), false, "tutorial-repeat");
            left.Add(navTop);
            tutorialNextLabel = AddLabel(left, "I DID IT — NEXT", "panel-kicker", 14);
            VisualElement navBottom = new VisualElement();
            navBottom.AddToClassList("button-row");
            AddButton(navBottom, "SKIP TUTORIAL", () => OnboardingManager.Instance?.SkipTutorial(), false, "tutorial-skip");
            AddButton(navBottom, "I DID IT — NEXT", () => OnboardingManager.Instance?.NextTutorialStep(), true, "tutorial-next");
            left.Add(navBottom);

            VisualElement right = Glass(content, "tutorial-visual-card");
            right.AddToClassList("tutorial-right");
            AddLabel(right, "FIELD GUIDE  /  CURRENT ACTIVITY", "panel-kicker", 14);
            tutorialIcon = AddLabel(right, "◉\nASTRO", "tutorial-icon", 54);
            tutorialSpeech = AddLabel(right, "", "speech-copy", 19);
            equipmentCard = new VisualElement();
            equipmentCard.AddToClassList("equipment-card");
            AddLabel(equipmentCard, "EQUIPMENT", "panel-kicker", 13);
            equipmentIcon = AddLabel(equipmentCard, "O₂", "equipment-icon", 40);
            equipmentBody = AddLabel(equipmentCard, "", "body-copy", 17);
            VisualElement equipmentButtons = new VisualElement();
            equipmentButtons.AddToClassList("button-row");
            AddButton(equipmentButtons, "PREVIOUS", () => OnboardingManager.Instance?.ShowEquipment((equipmentIndex + 4) % 5), false, "equipment-previous");
            AddButton(equipmentButtons, "NEXT", () => OnboardingManager.Instance?.ShowEquipment((equipmentIndex + 1) % 5), false, "equipment-next");
            equipmentCard.Add(equipmentButtons);
            right.Add(equipmentCard);
        }

        private void BuildComplete()
        {
            VisualElement content;
            completePage = NewPage("complete-page", "ASTRO  /  PRACTICE COMPLETE", "GREAT WORK, ASTRONAUT!", out content);
            VisualElement panel = Glass(content, "complete-card");
            AddLabel(panel, "✓", "complete-mark", 72);
            completeTitle = AddLabel(panel, "YOU'RE READY", "panel-title", 32);
            completeSpeech = AddLabel(panel, "Great work! You're ready for your first mission. Let's head to the Moon!", "body-copy", 20);
            AddButton(panel, "CHOOSE YOUR MISSION", () => OnboardingManager.Instance?.CloseCompletionToMissionSelect(), true, "choose-mission");
        }

        private void BuildMissionSelect()
        {
            VisualElement content;
            missionPage = NewPage("mission-page", "ASTRA  /  MISSION CONTROL", "MISSION BOARD", out content);
            content.AddToClassList("mission-columns");
            VisualElement list = Glass(content, "mission-list-card");
            AddLabel(list, "CAMPAIGN MISSIONS", "panel-kicker", 16);
            for (int i = 0; i < AstraMissionCatalog.Count; i++)
            {
                int index = i;
                Button option = AddButton(list, "", () => OnboardingManager.Instance?.SelectMission(index), false, "mission-option-" + index);
                option.AddToClassList("mission-option");
                option.style.fontSize = 15f;
                baseSizes[option] = 15f;
                missionButtons[i] = option;
                missionButtonLabels[i] = new Label();
                missionButtonLabels[i].AddToClassList("mission-option-label");
                option.Add(missionButtonLabels[i]);
            }

            VisualElement details = Glass(content, "mission-detail-card");
            missionDetailTitle = AddLabel(details, "01  /  FIRST LIGHT", "panel-title", 31);
            missionDestinationText = AddLabel(details, "DESTINATION  /  MOON", "panel-kicker", 16);
            missionDetailSummary = AddLabel(details, "", "mission-subtitle", 20);
            missionDetailStatus = AddLabel(details, "", "status-copy", 16);
            AddLabel(details, "MISSION BRIEFING", "panel-kicker", 14);
            missionBriefingText = AddLabel(details, "", "body-copy briefing-copy", 17);
            missionSourceButton = AddButton(details, "LEARN MORE  /  NASA SOURCE ↗", OpenSelectedMissionSource, false, "mission-source");

            VisualElement footer = new VisualElement();
            footer.AddToClassList("button-row");
            footer.style.width = Length.Percent(100f);
            AddButton(footer, "PRACTISE WITH ASTRO", () => OnboardingManager.Instance?.ReplayTutorial(), false, "mission-practise");
            AddButton(footer, "ACCESSIBILITY", () => OnboardingManager.Instance?.OpenSettings(), false, "mission-accessibility");
            launchSelectedMissionButton = AddButton(footer, "LAUNCH MISSION", () => OnboardingManager.Instance?.LaunchMission(), true, "mission-launch");
            content.Add(footer);
        }

        private void CycleLanguage() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s == null) return; s.SetLanguage((AstraLanguage)(((int)s.Language + 1) % 3)); UpdateSettingsValues(); }
        private void CycleReadingMode() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s == null) return; s.SetReadingMode((ReadingSupport)(((int)s.ReadingMode + 1) % 3)); UpdateSettingsValues(); }
        private void CycleAssistance() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s == null) return; s.SetAssistance((AssistanceLevel)(((int)s.Assistance + 1) % 3)); UpdateSettingsValues(); }
        private void ToggleNoReading() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetNoReading(!s.NoReadingMode); UpdateSettingsValues(); }
        private void AdjustText(float delta) { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetTextScale(s.TextScale >= 1.45f ? 0.85f : s.TextScale + delta); UpdateSettingsValues(); ApplyAccessibilitySettings(); }
        private void ToggleSubtitles() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetSubtitles(!s.Subtitles); UpdateSettingsValues(); }
        private void ToggleContrast() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetHighContrast(!s.HighContrast); UpdateSettingsValues(); ApplyAccessibilitySettings(); }
        private void AdjustVolume(float delta) { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetVoiceVolume(s.VoiceVolume >= 0.95f ? 0f : s.VoiceVolume + delta); UpdateSettingsValues(); }
        private void ToggleClickMove() { PlayerSettingsManager s = PlayerSettingsManager.Instance; if (s != null) s.SetClickToMove(!s.ClickToMove); UpdateSettingsValues(); }

        private void UpdateSettingsValues()
        {
            PlayerSettingsManager s = PlayerSettingsManager.Instance;
            if (s == null || languageValue == null) return;
            languageValue.text = new[] { "LANGUAGE  /  ENGLISH  ›", "ULIMI  /  ISIZULU  ›", "TAAL  /  AFRIKAANS  ›" }[(int)s.Language];
            string[][] reading =
            {
                new[] { "VOICE", "IZWI", "STEM" },
                new[] { "PICTURES", "IZITHOMBE", "PRENTE" },
                new[] { "VOICE + PICTURES", "IZWI + IZITHOMBE", "STEM + PRENTE" }
            };
            readingValue.text = reading[(int)s.ReadingMode][(int)s.Language] + "  ›";
            string[][] help =
            {
                new[] { "MORE GUIDANCE", "USIZO OLWENGEZIWE", "MEER LEIDING" },
                new[] { "SOME HINTS", "AMACEBO", "SOMMIGE WENKE" },
                new[] { "EXPLORE", "ZIHLOLELE", "VERKEN" }
            };
            assistanceValue.text = help[(int)s.Assistance][(int)s.Language] + "  ›";
            noReadingValue.text = s.NoReadingMode ? "ON  /  ICON HELP" : "OFF  /  ICON HELP";
            textSizeValue.text = Mathf.RoundToInt(s.TextScale * 100f) + "%  /  CHANGE";
            subtitlesValue.text = s.Subtitles ? "ON  /  ENABLED" : "OFF  /  DISABLED";
            contrastValue.text = s.HighContrast ? "ON  /  HIGH CONTRAST" : "OFF  /  STANDARD";
            volumeValue.text = Mathf.RoundToInt(s.VoiceVolume * 100f) + "%  /  CHANGE";
            clickMoveValue.text = s.ClickToMove ? "ON  /  ENABLED" : "OFF  /  DISABLED";
            ApplyAccessibilitySettings();
        }

        private void ShowControls()
        {
            settingsReturn = CurrentScreen == "settings" ? settingsReturn : CurrentScreen;
            CurrentScreen = "controls";
            HideAll();
            ShowPage(controlsPage);
            RefreshBindingCaptions();
        }

        public void ShowWelcome(bool returning)
        {
            CurrentScreen = "welcome";
            if (menuAstronaut != null) menuAstronaut.gameObject.SetActive(true);
            HideAll();
            ShowPage(welcomePage);
            welcomeTitle.text = "ASTRA";
            welcomeBody.text = "EXPLORE  ·  LEARN  ·  ACHIEVE";
            setupLabel.text = LanguageManager.Get(returning ? "welcome.returning" : "welcome.setup", returning
                ? "WELCOME BACK   /   YOUR NEXT FLIGHT IS READY"
                : "MISSION 01   /   EARTH TO THE MOON");
        }

        public void ShowSettings(string returnScreen)
        {
            if (menuAstronaut != null) menuAstronaut.gameObject.SetActive(false);
            settingsReturn = string.IsNullOrEmpty(returnScreen) ? "welcome" : returnScreen;
            CurrentScreen = "settings";
            HideAll();
            ShowPage(settingsPage);
            UpdateSettingsValues();
        }

        public void ShowScreen(string name)
        {
            switch (name)
            {
                case "tutorial": ShowTutorial(TutorialManager.Instance != null ? TutorialManager.Instance.CurrentStep : 0); break;
                case "mission": ShowMissionSelect(); break;
                case "paused": CurrentScreen = "paused"; HideAll(); break;
                case "welcome": ShowWelcome(true); break;
                default: ShowWelcome(true); break;
            }
        }

        public void ShowTutorial(int step)
        {
            if (menuAstronaut != null) menuAstronaut.gameObject.SetActive(false);
            CurrentScreen = "tutorial";
            HideAll();
            ShowPage(tutorialPage);
            ApplyTutorialStep(step);
        }

        public void RefreshTutorialStep(int step) => ApplyTutorialStep(step);

        private void ApplyTutorialStep(int step)
        {
            TutorialManager manager = TutorialManager.Instance;
            if (tutorialPage == null || manager == null) return;
            TutorialActivity activity = manager.CurrentActivity;
            tutorialStepText.text = LanguageManager.Get("tutorial.step", "ACTIVITY") + "  " + (step + 1) + " / " + manager.StepCount;
            tutorialTitle.text = LanguageManager.Get(activity.titleKey, activity.titleFallback);
            tutorialBody.text = LanguageManager.Get(activity.bodyKey, activity.bodyFallback);
            tutorialHint.text = "";
            bool noRead = PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.NoReadingMode;
            tutorialBody.style.display = noRead ? DisplayStyle.None : DisplayStyle.Flex;
            tutorialIcon.text = step == 0 ? "◉\nASTRO" : step == 1 ? "W\nA   S   D\n↑   ↓   ←   →" :
                step == 2 ? "RIGHT STICK\n↻ LOOK" : step == 3 ? "MOVE TO TARGET\n[E] / [X]" :
                step == 4 ? "O₂   H₂O\n☀   FOOD\nRADIO" : "☀\nPOWER UP";
            string nextCaption = step == manager.StepCount - 1
                ? LanguageManager.Get("tutorial.hint.button", "SHOW ME WHAT TO DO")
                : LanguageManager.Get("tutorial.next", "I DID IT — NEXT");
            tutorialNextLabel.text = nextCaption;
            Button next = root.Q<Button>("tutorial-next");
            if (next != null) next.text = nextCaption;
            equipmentCard.style.display = step == 4 ? DisplayStyle.Flex : DisplayStyle.None;
            if (step == 4) ShowEquipmentCard(equipmentIndex, LanguageManager.Get(EquipmentKeys[0], EquipmentFallback[0]));
        }

        public void ShowEquipmentCard(int index, string copy)
        {
            equipmentIndex = (index + EquipmentKeys.Length) % EquipmentKeys.Length;
            if (equipmentCard == null) return;
            string[] icons = { "O₂", "H₂O", "☀", "FOOD", "RADIO" };
            equipmentIcon.text = icons[equipmentIndex];
            equipmentBody.text = copy;
            bool noRead = PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.NoReadingMode;
            equipmentBody.style.display = noRead ? DisplayStyle.None : DisplayStyle.Flex;
            equipmentCard.style.display = CurrentScreen == "tutorial" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void HideEquipmentCard()
        {
            if (equipmentCard != null) equipmentCard.style.display = DisplayStyle.None;
        }

        public void ShowBuddyHint(string message)
        {
            if (tutorialHint == null || CurrentScreen != "tutorial") return;
            tutorialHint.text = PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.NoReadingMode ? "" : message;
        }

        public void ShowTutorialComplete()
        {
            if (menuAstronaut != null) menuAstronaut.gameObject.SetActive(false);
            CurrentScreen = "complete";
            HideAll();
            ShowPage(completePage);
            completeTitle.text = LanguageManager.Get("tutorial.complete", "GREAT WORK!");
            completeSpeech.text = LanguageManager.Get("ready", "Great work, astronaut! You're ready for your first mission. Let's head to the Moon!");
        }

        public void ShowMissionSelect()
        {
            int selected = OnboardingManager.Instance != null ? OnboardingManager.Instance.SelectedMissionIndex : 0;
            ShowMissionSelect(selected, AstraCampaignProgress.HighestUnlocked);
        }

        public void ShowMissionSelect(int selectedIndex, int highestUnlocked)
        {
            if (menuAstronaut != null) menuAstronaut.gameObject.SetActive(false);
            CurrentScreen = "mission";
            HideAll();
            ShowPage(missionPage);
            UpdateMissionSelection(selectedIndex, highestUnlocked);
        }

        public void UpdateMissionSelection(int selectedIndex, int highestUnlocked)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, AstraMissionCatalog.Count - 1);
            AstraMissionDefinition mission = AstraMissionCatalog.Get(selectedIndex);
            missionDetailTitle.text = (selectedIndex + 1).ToString("00") + "  /  " + mission.Title.ToUpperInvariant();
            missionDestinationText.text = "DESTINATION  /  " + AstraMissionCatalog.DestinationName(mission.Destination);
            missionDetailSummary.text = mission.Subtitle;
            missionBriefingText.text = mission.Briefing;
            int best = AstraCampaignProgress.BestScore(mission.Id);
            bool unlocked = AstraCampaignProgress.IsMissionAvailable(mission.Id);
            bool missionUnlocked = AstraCampaignProgress.IsUnlocked(mission.Id);
            missionDetailStatus.text = unlocked
                ? (best > 0 ? AstraMissionCatalog.DestinationName(mission.Destination) + "  /  PERSONAL BEST " + best.ToString("000") : AstraMissionCatalog.DestinationName(mission.Destination) + "  /  UNLOCKED")
                : missionUnlocked ? "LOCKED  /  REPAIR THE SHIP TO REACH " + AstraMissionCatalog.DestinationName(mission.Destination)
                : "LOCKED  /  COMPLETE MISSION " + selectedIndex.ToString("00") + " TO UNLOCK";
            missionSourceButton.text = "LEARN MORE  /  " + mission.SourceLabel.ToUpperInvariant() + " ↗";
            missionBriefingUrl = mission.SourceUrl;
            launchSelectedMissionButton.SetEnabled(unlocked);
            launchSelectedMissionButton.text = unlocked
                ? (selectedIndex == (int)AstraMissionId.FirstLight ? "LAUNCH FROM EARTH" : "LAUNCH " + mission.Title.ToUpperInvariant())
                : missionUnlocked ? "DESTINATION LOCKED" : "MISSION LOCKED";

            for (int i = 0; i < missionButtons.Length; i++)
            {
                AstraMissionDefinition item = AstraMissionCatalog.Get(i);
                bool itemUnlocked = AstraCampaignProgress.IsMissionAvailable(item.Id);
                bool slotUnlocked = AstraCampaignProgress.IsUnlocked(item.Id);
                missionButtonLabels[i].text = (i + 1).ToString("00") + "   " + AstraMissionCatalog.DestinationName(item.Destination) + "  /  " + item.Title.ToUpperInvariant() +
                    (itemUnlocked ? "" : slotUnlocked ? "  /  SHIP REPAIR" : "  /  LOCKED");
                missionButtons[i].EnableInClassList("mission-selected", i == selectedIndex);
                missionButtons[i].EnableInClassList("mission-locked", !itemUnlocked);
            }
        }

        public void ShowMissionLockedMessage()
        {
            if (missionDetailStatus != null)
                missionDetailStatus.text = "LOCKED  /  COMPLETE EARLIER MISSIONS TO CONTINUE";
        }

        public void HideAll()
        {
            SetVisible(welcomePage, false);
            SetVisible(settingsPage, false);
            SetVisible(controlsPage, false);
            SetVisible(tutorialPage, false);
            SetVisible(completePage, false);
            SetVisible(missionPage, false);
        }

        public void ApplyAccessibilitySettings()
        {
            if (root == null) return;
            PlayerSettingsManager settings = PlayerSettingsManager.Instance;
            float scale = settings != null ? settings.TextScale : 1f;
            foreach (KeyValuePair<TextElement, float> pair in baseSizes)
                if (pair.Key != null) pair.Key.style.fontSize = pair.Value * scale;
            root.EnableInClassList("high-contrast", settings != null && settings.HighContrast);
        }

        private void SetSpeech(string line)
        {
            if (tutorialSpeech == null || CurrentScreen != "tutorial") return;
            bool voiceMissing = VoiceGuideManager.Instance == null || !VoiceGuideManager.Instance.LastVoiceAvailable;
            bool show = PlayerSettingsManager.Instance == null || PlayerSettingsManager.Instance.Subtitles ||
                PlayerSettingsManager.Instance.ReadingMode != ReadingSupport.VoiceGuided || voiceMissing;
            tutorialSpeech.text = show ? line : "";
        }

        private void SetControlsStatus(string value)
        {
            if (controlsStatus != null)
                controlsStatus.text = string.IsNullOrEmpty(value) ? "Choose a control above, then press a key. Escape cancels." : value;
            RefreshBindingCaptions();
        }

        private void RefreshBindingCaptions()
        {
            PlayerSettingsManager settings = PlayerSettingsManager.Instance;
            if (settings == null) return;
            foreach (KeyValuePair<AstraBinding, Button> pair in bindingButtons)
                pair.Value.text = settings.PendingRebind == "KEY_" + pair.Key.ToString().ToUpperInvariant()
                    ? "PRESS A KEY..." : settings.GetKeyLabel(pair.Key);
        }

        private void UpdateControlCaptions()
        {
            if (CurrentScreen == "controls") RefreshBindingCaptions();
        }

        private void ShowPage(VisualElement page)
        {
            if (page != null) page.style.display = DisplayStyle.Flex;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OpenSelectedMissionSource()
        {
            if (!string.IsNullOrEmpty(missionBriefingUrl)) Application.OpenURL(missionBriefingUrl);
        }

    }
}
