using System;
using UnityEngine;

namespace Astra
{
    /// <summary>Applies the saved accessibility profile consistently across UI, input and audio.</summary>
    public sealed class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }
        public event Action AccessibilityChanged;
        public bool NoReadingMode => PlayerSettingsManager.Instance != null && PlayerSettingsManager.Instance.NoReadingMode;
        public bool VisualHintsEnabled => PlayerSettingsManager.Instance == null || PlayerSettingsManager.Instance.ReadingMode != ReadingSupport.VoiceGuided || NoReadingMode;
        public bool VoiceEnabled => PlayerSettingsManager.Instance == null || PlayerSettingsManager.Instance.ReadingMode != ReadingSupport.VisualGuided || NoReadingMode;

        private PlayerSettingsManager settings;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            settings = GetComponent<PlayerSettingsManager>();
            if (settings != null) settings.SettingsChanged += Apply;
        }

        private void Start() => Apply();
        private void OnDestroy() { if (settings != null) settings.SettingsChanged -= Apply; if (Instance == this) Instance = null; }

        public void Apply()
        {
            if (settings == null) settings = PlayerSettingsManager.Instance;
            if (settings == null) return;
            VoiceGuideManager.Instance?.ApplySettings();
            FindFirstObjectByType<AstraHUD>()?.ApplyAccessibilitySettings();
            FindFirstObjectByType<AstraOnboardingUI>()?.ApplyAccessibilitySettings();
            AccessibilityChanged?.Invoke();
        }
    }
}
