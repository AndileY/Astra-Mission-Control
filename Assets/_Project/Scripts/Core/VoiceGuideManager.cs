using System;
using UnityEngine;

namespace Astra
{
    /// <summary>Audio-ready guide. Add recordings in Resources/Audio/Astro/{english|isizulu|afrikaans}/.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceGuideManager : MonoBehaviour
    {
        public static VoiceGuideManager Instance { get; private set; }
        public event Action<string> LineChanged;
        public string LastLine { get; private set; } = string.Empty;
        public bool LastVoiceAvailable { get; private set; }
        public bool IsSpeaking => source != null && source.isPlaying;

        private AudioSource source;
        private PlayerSettingsManager settings;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            settings = GetComponent<PlayerSettingsManager>();
        }

        private void Start() => ApplySettings();
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void ApplySettings()
        {
            if (settings == null) settings = PlayerSettingsManager.Instance;
            if (source != null) source.volume = settings != null ? settings.VoiceVolume : 0.8f;
        }

        public void Say(string key, string fallbackEnglish, string clipName = null, bool repeat = false)
        {
            string localized = LanguageManager.Get(key, fallbackEnglish);
            LastLine = localized;
            if (settings == null) settings = PlayerSettingsManager.Instance;
            bool voiceEnabled = AccessibilityManager.Instance == null || AccessibilityManager.Instance.VoiceEnabled;
            string baseName = string.IsNullOrEmpty(clipName) ? key : clipName;
            string locale = settings == null ? "english" : LocaleFolder(settings.Language);
            AudioClip clip = Resources.Load<AudioClip>("Audio/Astro/" + locale + "/" + baseName);
            // English voice is supplied as an example voice pack. Other locales stay audio-ready for native recordings.
            if (clip == null && (settings == null || settings.Language == AstraLanguage.English))
                clip = Resources.Load<AudioClip>("Audio/Astro/english/" + baseName);
            LastVoiceAvailable = clip != null;
            LineChanged?.Invoke(localized);
            if (!voiceEnabled || (settings != null && settings.ReadingMode == ReadingSupport.VisualGuided && !settings.NoReadingMode)) return;
            if (!repeat && source != null && source.isPlaying) source.Stop();
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, settings != null ? settings.VoiceVolume : 0.8f);
            }
        }

        public void Stop() { if (source != null) source.Stop(); }
        public static string LocaleFolder(AstraLanguage language)
        {
            switch (language)
            {
                case AstraLanguage.isiZulu: return "isizulu";
                case AstraLanguage.Afrikaans: return "afrikaans";
                default: return "english";
            }
        }
    }
}
