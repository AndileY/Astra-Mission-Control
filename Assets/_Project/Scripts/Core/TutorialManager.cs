using System;
using UnityEngine;

namespace Astra
{
    [Serializable]
    public class TutorialActivity
    {
        public string titleKey;
        public string bodyKey;
        public string titleFallback;
        [TextArea] public string bodyFallback;
        public string voiceClip;
    }

    /// <summary>Guided, repeatable practice sequence; it never changes the real mission state.</summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }
        public event Action<int> StepChanged;
        public bool IsActive { get; private set; }
        public bool IsSuspended { get; private set; }
        public int CurrentStep { get; private set; }
        public int StepCount => activities.Length;
        public float PracticeDistance { get; private set; }
        public float LookPractice { get; private set; }
        public bool PracticeInteractionDone { get; private set; }
        public bool SolarPracticeDone { get; private set; }
        public TutorialActivity CurrentActivity => activities[Mathf.Clamp(CurrentStep, 0, activities.Length - 1)];

        [SerializeField] private TutorialActivity[] activities =
        {
            new TutorialActivity { titleKey="tutorial.meet.title", bodyKey="tutorial.meet.body", titleFallback="MEET YOUR ASTRONAUT", bodyFallback="This is your astronaut. ASTRO is here beside you. Take your time; you can try anything again.", voiceClip="meet" },
            new TutorialActivity { titleKey="tutorial.move.title", bodyKey="tutorial.move.body", titleFallback="LEARN TO MOVE", bodyFallback="Use WASD/arrow keys, a gamepad's left stick, or a joystick's main stick to walk. Click the ground to walk there.", voiceClip="move" },
            new TutorialActivity { titleKey="tutorial.look.title", bodyKey="tutorial.look.body", titleFallback="LOOK AROUND", bodyFallback="Use right mouse drag, a gamepad's right stick, or a joystick's twist control to look around.", voiceClip="look" },
            new TutorialActivity { titleKey="tutorial.interact.title", bodyKey="tutorial.interact.body", titleFallback="TRY AN INTERACTION", bodyFallback="Walk to the glowing practice panel. When the arrow points down, press E, gamepad X, or the joystick trigger.", voiceClip="interact" },
            new TutorialActivity { titleKey="tutorial.equipment.title", bodyKey="tutorial.equipment.body", titleFallback="MEET THE EQUIPMENT", bodyFallback="Tap an equipment picture. ASTRO will tell you what it does.", voiceClip="equipment" },
            new TutorialActivity { titleKey="tutorial.task.title", bodyKey="tutorial.task.body", titleFallback="POWER UP A PRACTICE PANEL", bodyFallback="Follow the arrow to the small practice solar panel and press E, gamepad X, or the joystick trigger. Watch it light up!", voiceClip="practice" }
        };

        private float idleSeconds;
        private float hintCooldown;
        private OnboardingManager onboarding;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            onboarding = GetComponent<OnboardingManager>();
        }

        private void Update()
        {
            if (!IsActive || IsSuspended) return;
            idleSeconds += Time.unscaledDeltaTime;
            hintCooldown -= Time.unscaledDeltaTime;
            if (idleSeconds > 24f && hintCooldown <= 0f && PlayerSettingsManager.Instance != null &&
                PlayerSettingsManager.Instance.Assistance != AssistanceLevel.Independent)
            {
                hintCooldown = PlayerSettingsManager.Instance.Assistance == AssistanceLevel.MoreGuidance ? 18f : 32f;
                idleSeconds = 0f;
                onboarding?.GiveStepHint();
            }
        }

        public void Begin()
        {
            PracticeDistance = 0f; LookPractice = 0f; PracticeInteractionDone = false; SolarPracticeDone = false;
            IsActive = true;
            IsSuspended = false;
            ShowStep(0);
        }

        public void SetSuspended(bool value) => IsSuspended = value;

        public void ShowStep(int step)
        {
            if (!IsActive || IsSuspended) return;
            int nextStep = Mathf.Clamp(step, 0, activities.Length - 1);
            // Keep the equipment card scoped to its activity, including when the player goes back.
            if (CurrentStep == 4 && nextStep != 4) onboarding?.HideEquipment();
            CurrentStep = nextStep;
            idleSeconds = 0f;
            hintCooldown = PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.Assistance == AssistanceLevel.MoreGuidance ? 14f : 25f;
            StepChanged?.Invoke(CurrentStep);
            SetStepTarget();
            TutorialActivity activity = CurrentActivity;
            string spoken = LanguageManager.Get(activity.bodyKey, activity.bodyFallback);
            FindFirstObjectByType<AstroCompanionController>()?.Say(activity.bodyKey, activity.bodyFallback, activity.voiceClip);
            if (CurrentStep == 0) FindFirstObjectByType<AstroCompanionController>()?.PointAt(FindFirstObjectByType<AstraPlayerController>()?.transform);
            if (CurrentStep == 4) onboarding?.ShowEquipment(0);
        }

        private void SetStepTarget()
        {
            VisualHintManager visual = VisualHintManager.Instance;
            if (visual == null) return;
            if (CurrentStep == 0) visual.SetTarget(FindFirstObjectByType<AstraPlayerController>()?.transform);
            else if (CurrentStep == 3) visual.SetTarget(visual.FindPracticeTarget(false));
            else if (CurrentStep == 5) visual.SetTarget(visual.FindPracticeTarget(true));
            else visual.SetTarget(null);
            FindFirstObjectByType<AstroCompanionController>()?.PointAt(visualTarget);
        }

        private Transform visualTarget => VisualHintManager.Instance != null ?
            (CurrentStep == 0 ? FindFirstObjectByType<AstraPlayerController>()?.transform :
             CurrentStep == 3 ? VisualHintManager.Instance.FindPracticeTarget(false) :
             CurrentStep == 5 ? VisualHintManager.Instance.FindPracticeTarget(true) : null) : null;

        public void Advance()
        {
            if (!IsActive) return;
            if (CurrentStep >= activities.Length - 1)
            {
                onboarding?.GiveStepHint();
                return;
            }
            ShowStep(CurrentStep + 1);
        }

        public void Previous() { if (CurrentStep > 0) ShowStep(CurrentStep - 1); }
        public void RecordMovement(float distance) { if (IsActive && distance > 0f) { PracticeDistance += distance; idleSeconds = 0f; } }
        public void RecordLook(float degrees) { if (IsActive && degrees > 0f) { LookPractice += degrees; idleSeconds = 0f; } }

        public bool HandlePracticeInteraction(AstraInteractable facility)
        {
            if (!IsActive || facility == null) return false;
            if (CurrentStep == 3 && facility.facilityName.Contains("Practice Communications"))
            {
                facility.isOnline = true;
                PracticeInteractionDone = true;
                idleSeconds = 0f;
                TurnOnPracticeVisuals(facility.transform, new Color(0.25f, 1f, 0.73f));
                FindFirstObjectByType<AstroCompanionController>()?.Cheer();
                onboarding?.PracticeActionCompleted("tutorial.interact.title", "You did it! You used the practice panel.");
                Advance();
                return true;
            }
            if (CurrentStep == 5 && facility.facilityName.Contains("Practice Solar"))
            {
                facility.isOnline = true;
                SolarPracticeDone = true;
                idleSeconds = 0f;
                TurnOnPracticeVisuals(facility.transform, new Color(1f, 0.8f, 0.25f));
                FindFirstObjectByType<AstroCompanionController>()?.Cheer();
                VisualHintManager.Instance?.SetTarget(null);
                VisualHintManager.Instance?.ShowCompletion();
                IsActive = false;
                onboarding?.CompleteTutorial();
                return true;
            }
            return false;
        }

        private static void TurnOnPracticeVisuals(Transform target, Color glow)
        {
            if (target == null) return;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glow);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", glow);
                if (material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", glow * 1.8f); }
            }
        }

        public void Stop() { IsActive = false; IsSuspended = false; VisualHintManager.Instance?.SetTarget(null); FindFirstObjectByType<AstroCompanionController>()?.PointAt(null); }
    }
}
