using UnityEngine;

namespace Astra
{
    /// <summary>Owns the welcome, accessibility setup, safe tutorial, and handoff to the mission board.</summary>
    public sealed class OnboardingManager : MonoBehaviour
    {
        public static OnboardingManager Instance { get; private set; }
        public bool AllowPlayerControl => TutorialManager.Instance != null && TutorialManager.Instance.IsActive && !TutorialManager.Instance.IsSuspended;
        public bool IsPracticeActive => AllowPlayerControl;
        public bool IsMissionSelectOpen { get; private set; }
        public int SelectedMissionIndex { get; private set; }

        private AstraOnboardingUI ui;
        private AstraGameManager game;
        private AstraEarthLaunchSequence earthLaunch;
        private TutorialManager tutorial;
        private AstroCompanionController astro;
        private bool continueToTutorialAfterSettings;
        private bool resumeMissionAfterTutorial;
        private bool pausedForSettings;
        private string previousScreen = "welcome";

        private static readonly string[] EquipmentKeys = { "equipment.oxygen", "equipment.water", "equipment.solar", "equipment.food", "equipment.comms" };
        private static readonly string[] EquipmentFallback = {
            "OXYGEN · This system adds breathable air in our game.",
            "WATER · The crew needs water to drink.",
            "SUN · Solar panels make game power from sunlight.",
            "FOOD · The greenhouse grows food for the crew.",
            "RADIO · Communications let the outpost talk to Earth."
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            ui = FindFirstObjectByType<AstraOnboardingUI>();
            game = AstraGameManager.Instance != null ? AstraGameManager.Instance : FindFirstObjectByType<AstraGameManager>();
            if (game != null)
            {
                earthLaunch = game.GetComponent<AstraEarthLaunchSequence>();
                if (earthLaunch == null) earthLaunch = game.gameObject.AddComponent<AstraEarthLaunchSequence>();
            }
            tutorial = GetComponent<TutorialManager>();
            astro = FindFirstObjectByType<AstroCompanionController>();
            // Mission IDs changed with the five-mission campaign; avoid interpreting
            // an old selection index as a different mission on the new board.
            SelectedMissionIndex = Mathf.Clamp(PlayerPrefs.GetInt("ASTRA_SELECTED_MISSION_V3", 0), 0, AstraMissionCatalog.Count - 1);
            if (tutorial != null) tutorial.StepChanged += TutorialStepChanged;
            Invoke(nameof(ShowWelcome), 0f);
        }

        private void OnDestroy() { if (tutorial != null) tutorial.StepChanged -= TutorialStepChanged; if (Instance == this) Instance = null; }

        private void ShowWelcome()
        {
            hud()?.SetMissionHudVisible(false);
            ui?.ShowWelcome(PlayerPrefs.GetInt("ASTRA_ONBOARDING_CONFIGURED", 0) == 1);
            astro?.Say("welcome", "Hello, astronaut! Welcome to ASTRA! I'm ASTRO, and I'll be your space companion. Before we explore the Moon, let me show you around!", "welcome");
        }

        public void BeginSetup()
        {
            continueToTutorialAfterSettings = true;
            ui?.ShowSettings("welcome");
            astro?.Say("settings.title", "Let's make ASTRA fit you.", "settings");
        }

        public void OpenSettings()
        {
            previousScreen = game != null && game.missionPaused && game.missionStarted ? "paused" : ui != null ? ui.CurrentScreen : "welcome";
            pausedForSettings = game != null && game.missionStarted && !game.missionPaused && !game.missionFinished;
            if (pausedForSettings) game.SetPausedFromHud(true);
            if (tutorial != null && tutorial.IsActive) tutorial.SetSuspended(true);
            ui?.ShowSettings(previousScreen);
            astro?.Say("settings.title", "Choose what helps you play.", "settings");
        }

        public void CloseSettings()
        {
            PlayerPrefs.SetInt("ASTRA_ONBOARDING_CONFIGURED", 1);
            PlayerPrefs.Save();
            if (continueToTutorialAfterSettings)
            {
                continueToTutorialAfterSettings = false;
                StartTutorial();
                return;
            }
            ui?.ShowScreen(previousScreen);
            if (previousScreen == "paused") hud()?.SetMissionHudVisible(true);
            if (pausedForSettings && game != null) game.SetPausedFromHud(false);
            if (tutorial != null && tutorial.IsActive) tutorial.SetSuspended(false);
            pausedForSettings = false;
        }

        public void StartTutorial()
        {
            hud()?.SetMissionHudVisible(false);
            resumeMissionAfterTutorial = game != null && game.missionStarted && !game.missionFinished;
            if (resumeMissionAfterTutorial && !game.missionPaused) game.SetPausedFromHud(true);
            IsMissionSelectOpen = false;
            tutorial?.Begin();
            ui?.ShowTutorial(0);
        }

        public void ReplayTutorial() => StartTutorial();

        public void SkipTutorial()
        {
            bool resumeMission = resumeMissionAfterTutorial;
            tutorial?.Stop();
            PlayerPrefs.SetString("ASTRA_TUTORIAL_STATUS", "skipped");
            PlayerPrefs.Save();
            resumeMissionAfterTutorial = false;
            if (resumeMission)
            {
                ui?.HideAll();
                game?.SetPausedFromHud(false);
                return;
            }
            ShowMissionSelect();
        }

        public void NextTutorialStep() => tutorial?.Advance();
        public void PreviousTutorialStep() => tutorial?.Previous();
        public void RepeatCurrentStep()
        {
            if (tutorial == null) return;
            TutorialActivity activity = tutorial.CurrentActivity;
            astro?.Say(activity.bodyKey, activity.bodyFallback, activity.voiceClip, true);
            ui?.RefreshTutorialStep(tutorial.CurrentStep);
        }

        public void ShowEquipment(int index)
        {
            index = Mathf.Clamp(index, 0, EquipmentKeys.Length - 1);
            string key = EquipmentKeys[index];
            astro?.Say(key, EquipmentFallback[index], "equipment_" + index);
            ui?.ShowEquipmentCard(index, LanguageManager.Get(key, EquipmentFallback[index]));
        }

        public void HideEquipment() => ui?.HideEquipmentCard();

        public void GiveStepHint()
        {
            if (tutorial == null) return;
            string key = tutorial.CurrentStep == 1 ? "hint.move" : tutorial.CurrentStep == 2 ? "hint.look" : "tutorial.interact.body";
            string fallback = tutorial.CurrentStep == 1 ? "Try a gentle step. There is no rush." : tutorial.CurrentStep == 2 ? "Try holding the right mouse button while moving the mouse." : "Follow ASTRO's arrow to the practice object. You can try again.";
            string clip = tutorial.CurrentStep == 1 ? "hint_move" : tutorial.CurrentStep == 2 ? "hint_look" : "hint_interact";
            astro?.Say(key, fallback, clip, true);
            ui?.ShowBuddyHint(LanguageManager.Get(key, fallback));
            if (VisualHintManager.Instance != null && tutorial.CurrentStep == 3)
                VisualHintManager.Instance.SetTarget(VisualHintManager.Instance.FindPracticeTarget(false));
            if (VisualHintManager.Instance != null && tutorial.CurrentStep == 5)
                VisualHintManager.Instance.SetTarget(VisualHintManager.Instance.FindPracticeTarget(true));
        }

        public void PracticeActionCompleted(string key, string fallback)
        {
            astro?.Say("hint.completed", "Nice exploring! Every try helps you learn.", "success");
            ui?.ShowBuddyHint(LanguageManager.Get(key, fallback));
        }

        public void CompleteTutorial()
        {
            PlayerPrefs.SetString("ASTRA_TUTORIAL_STATUS", "completed");
            PlayerPrefs.SetInt("ASTRA_ONBOARDING_CONFIGURED", 1);
            PlayerPrefs.Save();
            if (!resumeMissionAfterTutorial)
            {
                ui?.ShowTutorialComplete();
                astro?.Say("ready", "Great work, astronaut! You're ready for your first mission. Let's head to the Moon!", "ready");
            }
            else
            {
                resumeMissionAfterTutorial = false;
                ui?.HideAll();
                game?.SetPausedFromHud(false);
            }
        }

        public void ShowMissionSelect()
        {
            IsMissionSelectOpen = true;
            if (!AstraCampaignProgress.IsMissionAvailable((AstraMissionId)SelectedMissionIndex))
            {
                for (int i = Mathf.Min(AstraCampaignProgress.HighestUnlocked, AstraMissionCatalog.Count - 1); i >= 0; i--)
                    if (AstraCampaignProgress.IsMissionAvailable((AstraMissionId)i)) { SelectedMissionIndex = i; break; }
            }
            hud()?.SetMissionHudVisible(false);
            ui?.ShowMissionSelect(SelectedMissionIndex, AstraCampaignProgress.HighestUnlocked);
            astro?.PointAt(null);
        }

        public void SelectMission(int index)
        {
            SelectedMissionIndex = Mathf.Clamp(index, 0, AstraMissionCatalog.Count - 1);
            PlayerPrefs.SetInt("ASTRA_SELECTED_MISSION_V3", SelectedMissionIndex);
            PlayerPrefs.Save();
            ui?.UpdateMissionSelection(SelectedMissionIndex, AstraCampaignProgress.HighestUnlocked);
        }

        public void LaunchMission()
        {
            if (!AstraCampaignProgress.IsMissionAvailable((AstraMissionId)SelectedMissionIndex))
            {
                ui?.ShowMissionLockedMessage();
                return;
            }
            if (SelectedMissionIndex == (int)AstraMissionId.FirstLight)
            {
                LaunchFromEarth();
                return;
            }
            if (ui != null) ui.HideAll();
            hud()?.SetMissionHudVisible(true);
            IsMissionSelectOpen = false;
            if (tutorial != null) tutorial.Stop();
            FindFirstObjectByType<AstraPlayerController>()?.ResetForMission();
            game?.BeginMission((AstraMissionId)SelectedMissionIndex);
        }

        /// <summary>Starts First Light with the preserved Earth launch and lunar arrival sequence.</summary>
        public void LaunchFromEarth()
        {
            if (!AstraCampaignProgress.IsMissionAvailable(AstraMissionId.FirstLight))
            {
                ui?.ShowMissionLockedMessage();
                return;
            }
            if (game != null && game.missionStarted && !game.missionFinished) return;

            SelectedMissionIndex = (int)AstraMissionId.FirstLight;
            PlayerPrefs.SetInt("ASTRA_SELECTED_MISSION_V3", SelectedMissionIndex);
            PlayerPrefs.Save();
            IsMissionSelectOpen = false;
            ui?.HideAll();
            hud()?.SetMissionHudVisible(false);
            if (tutorial != null) tutorial.Stop();
            FindFirstObjectByType<AstraPlayerController>()?.ResetForMission();

            if (earthLaunch == null && game != null)
                earthLaunch = game.GetComponent<AstraEarthLaunchSequence>() ?? game.gameObject.AddComponent<AstraEarthLaunchSequence>();

            if (earthLaunch == null)
            {
                hud()?.SetMissionHudVisible(true);
                game?.BeginMission(AstraMissionId.FirstLight);
                return;
            }

            earthLaunch.Begin(
                () =>
                {
                    hud()?.SetMissionHudVisible(true);
                    game?.BeginMission(AstraMissionId.FirstLight);
                },
                ReturnToMissionBoard);
        }

        public void ReturnToMissionBoard()
        {
            IsMissionSelectOpen = true;
            hud()?.SetMissionHudVisible(false);
            ui?.ShowMissionSelect(SelectedMissionIndex, AstraCampaignProgress.HighestUnlocked);
        }

        public bool HandlePracticeInteraction(AstraInteractable facility) => tutorial != null && tutorial.HandlePracticeInteraction(facility);
        public void CloseCompletionToMissionSelect() => ShowMissionSelect();

        public void TutorialStepChanged(int step) => ui?.RefreshTutorialStep(step);

        public void ShowSettingsFromPause()
        {
            previousScreen = "paused";
            pausedForSettings = false;
            ui?.ShowSettings(previousScreen);
        }

        private AstraHUD hud() => game != null ? FindFirstObjectByType<AstraHUD>() : null;
    }
}
