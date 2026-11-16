using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Astra
{
    public enum AstraLanguage { English, isiZulu, Afrikaans }
    public enum ReadingSupport { VoiceGuided, VisualGuided, Combined }
    public enum AssistanceLevel { MoreGuidance, Balanced, Independent }
    public enum AstraBinding { MoveForward, MoveBack, MoveLeft, MoveRight, Interact }

    /// <summary>Small local profile shared by onboarding, HUD, audio and player controls.</summary>
    public sealed class PlayerSettingsManager : MonoBehaviour
    {
        public static PlayerSettingsManager Instance { get; private set; }
        public event Action<string> RebindFinished;
        public event Action SettingsChanged;

        public AstraLanguage Language { get; private set; }
        public ReadingSupport ReadingMode { get; private set; }
        public AssistanceLevel Assistance { get; private set; }
        public bool NoReadingMode { get; private set; }
        public bool Subtitles { get; private set; }
        public bool HighContrast { get; private set; }
        public bool ClickToMove { get; private set; }
        public float TextScale { get; private set; }
        public float VoiceVolume { get; private set; }
        public string PendingRebind { get; private set; }

        private const string Prefix = "ASTRA_ACCESS_";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        private void Load()
        {
            Language = (AstraLanguage)PlayerPrefs.GetInt(Prefix + "LANG", 0);
            ReadingMode = (ReadingSupport)PlayerPrefs.GetInt(Prefix + "READING", 2);
            Assistance = (AssistanceLevel)PlayerPrefs.GetInt(Prefix + "ASSIST", 0);
            NoReadingMode = PlayerPrefs.GetInt(Prefix + "NO_READ", 0) != 0;
            Subtitles = PlayerPrefs.GetInt(Prefix + "SUBTITLES", 1) != 0;
            HighContrast = PlayerPrefs.GetInt(Prefix + "CONTRAST", 0) != 0;
            ClickToMove = PlayerPrefs.GetInt(Prefix + "CLICK_MOVE", 1) != 0;
            TextScale = PlayerPrefs.GetFloat(Prefix + "TEXT_SCALE", 1f);
            VoiceVolume = PlayerPrefs.GetFloat(Prefix + "VOICE", 0.8f);
            if (NoReadingMode) { ReadingMode = ReadingSupport.Combined; Assistance = AssistanceLevel.MoreGuidance; }
        }

        public Key GetKey(AstraBinding binding)
        {
            string key = BindingKey(binding);
            Key fallback = DefaultKey(binding);
            return (Key)PlayerPrefs.GetInt(Prefix + key, (int)fallback);
        }

        public string GetKeyLabel(AstraBinding binding) => GetKey(binding).ToString().ToUpperInvariant();

        public void SetLanguage(AstraLanguage value) { Language = value; SaveInt("LANG", (int)value); }
        public void SetReadingMode(ReadingSupport value) { ReadingMode = value; if (NoReadingMode && value == ReadingSupport.VisualGuided) NoReadingMode = false; SaveInt("READING", (int)ReadingMode); Notify(); }
        public void SetAssistance(AssistanceLevel value) { Assistance = value; SaveInt("ASSIST", (int)value); Notify(); }
        public void SetNoReading(bool value)
        {
            NoReadingMode = value;
            if (value) { ReadingMode = ReadingSupport.Combined; Assistance = AssistanceLevel.MoreGuidance; }
            SaveInt("NO_READ", value ? 1 : 0); SaveInt("READING", (int)ReadingMode); SaveInt("ASSIST", (int)Assistance); Notify();
        }
        public void SetSubtitles(bool value) { Subtitles = value; SaveInt("SUBTITLES", value ? 1 : 0); Notify(); }
        public void SetHighContrast(bool value) { HighContrast = value; SaveInt("CONTRAST", value ? 1 : 0); Notify(); }
        public void SetClickToMove(bool value) { ClickToMove = value; SaveInt("CLICK_MOVE", value ? 1 : 0); Notify(); }
        public void SetTextScale(float value) { TextScale = Mathf.Clamp(value, 0.85f, 1.5f); PlayerPrefs.SetFloat(Prefix + "TEXT_SCALE", TextScale); Save(); }
        public void SetVoiceVolume(float value) { VoiceVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "VOICE", VoiceVolume); Save(); }

        public void BeginRebind(AstraBinding binding)
        {
            PendingRebind = BindingKey(binding);
            RebindFinished?.Invoke("Press a key for " + binding + ". Press Escape to cancel.");
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(PendingRebind) || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) { PendingRebind = null; RebindFinished?.Invoke(string.Empty); return; }
            foreach (KeyControl control in Keyboard.current.allKeys)
            {
                if (!control.wasPressedThisFrame || control.keyCode == Key.None || control.keyCode == Key.Escape ||
                    control.keyCode == Key.LeftShift || control.keyCode == Key.RightShift || control.keyCode == Key.LeftCtrl || control.keyCode == Key.RightCtrl)
                    continue;
                PlayerPrefs.SetInt(Prefix + PendingRebind, (int)control.keyCode);
                Save();
                PendingRebind = null;
                RebindFinished?.Invoke("Key saved: " + control.keyCode.ToString().ToUpperInvariant());
                break;
            }
        }

        private void SaveInt(string key, int value) { PlayerPrefs.SetInt(Prefix + key, value); Save(); }
        private static string BindingKey(AstraBinding binding) => "KEY_" + binding.ToString().ToUpperInvariant();
        private static Key DefaultKey(AstraBinding binding)
        {
            switch (binding)
            {
                case AstraBinding.MoveForward: return Key.W;
                case AstraBinding.MoveBack: return Key.S;
                case AstraBinding.MoveLeft: return Key.A;
                case AstraBinding.MoveRight: return Key.D;
                default: return Key.E;
            }
        }
        private void Notify() => SettingsChanged?.Invoke();
        private static void Save() => PlayerPrefs.Save();
    }
}
