using UnityEngine;
using UnityEngine.UI;

namespace Astra
{
    /// <summary>Reusable world marker: a down arrow, spotlight and pulsing highlight.</summary>
    public sealed class VisualHintManager : MonoBehaviour
    {
        public static VisualHintManager Instance { get; private set; }
        private Transform target;
        private TextMesh arrow;
        private Light markerLight;
        private Camera viewCamera;
        private float pulse;
        private GameObject completionBadge;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameObject marker = new GameObject("ASTRO Visual Target Marker");
            arrow = marker.AddComponent<TextMesh>();
            arrow.text = "▼";
            arrow.anchor = TextAnchor.MiddleCenter;
            arrow.alignment = TextAlignment.Center;
            arrow.fontSize = 86;
            arrow.characterSize = 0.16f;
            arrow.color = new Color(0.20f, 0.95f, 1f);
            markerLight = marker.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.color = new Color(0.18f, 0.82f, 1f);
            markerLight.range = 4f;
            markerLight.intensity = 1.8f;
            marker.transform.SetParent(transform, false);
            marker.SetActive(false);
        }

        private void Update()
        {
            if (target == null || arrow == null) return;
            viewCamera = Camera.main;
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            float top = renderer != null ? renderer.bounds.max.y : target.position.y + 1.5f;
            float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.12f;
            arrow.transform.position = new Vector3(target.position.x, top + 1.05f + bob, target.position.z);
            if (viewCamera != null) arrow.transform.rotation = Quaternion.LookRotation(arrow.transform.position - viewCamera.transform.position);
            arrow.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.08f);
            if (markerLight != null) markerLight.intensity = 1.2f + Mathf.PingPong(Time.unscaledTime * 2.4f, 1.3f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (arrow != null) arrow.gameObject.SetActive(target != null);
        }

        public Transform FindPracticeTarget(bool solar)
        {
            AstraInteractable[] facilities = FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None);
            foreach (AstraInteractable item in facilities)
            {
                if (solar && item.facilityName.Contains("Practice Solar")) return item.transform;
                if (!solar && item.facilityName.Contains("Practice Communications")) return item.transform;
            }
            return null;
        }

        public Transform FindMissionTarget(int objectiveIndex)
        {
            FacilityType type;
            switch (objectiveIndex)
            {
                case 0: type = FacilityType.Communications; break;
                case 1: type = FacilityType.SolarArray; break;
                case 2: type = FacilityType.LifeSupport; break;
                case 3: type = FacilityType.RadiationShelter; break;
                case 4: type = FacilityType.Greenhouse; break;
                default: type = FacilityType.Rover; break;
            }
            foreach (AstraInteractable item in FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (item.facilityType == type && !item.facilityName.Contains("Practice")) return item.transform;
            return null;
        }

        public void RefreshMissionTarget(AstraGameManager game)
        {
            if (game == null || !game.missionStarted || game.missionFinished)
            {
                SetTarget(null);
                return;
            }

            foreach (MissionObjective objective in game.Objectives)
            {
                if (objective.complete) continue;
                FacilityType type;
                switch (objective.id)
                {
                    case AstraObjectiveId.Communications: type = FacilityType.Communications; break;
                    case AstraObjectiveId.Solar: type = FacilityType.SolarArray; break;
                    case AstraObjectiveId.LifeSupport: type = FacilityType.LifeSupport; break;
                    case AstraObjectiveId.Shelter: type = FacilityType.RadiationShelter; break;
                    case AstraObjectiveId.CropHarvest: type = FacilityType.Greenhouse; break;
                    case AstraObjectiveId.RoverSample: type = FacilityType.Rover; break;
                    case AstraObjectiveId.OxygenRestored: type = FacilityType.OxygenProcessor; break;
                    case AstraObjectiveId.WaterRestored: type = FacilityType.WaterRecycler; break;
                    case AstraObjectiveId.WeatherReportReviewed: type = FacilityType.SpaceWeatherStation; break;
                    case AstraObjectiveId.ProtectedAreaReadiness:
                        SetTarget(game.GetStormReadinessTarget());
                        return;
                    case AstraObjectiveId.PostDrillInspection:
                        SetTarget(game.GetStormInspectionTarget());
                        return;
                    case AstraObjectiveId.StormResponse:
                        if (game.CurrentMission == AstraMissionId.StormWatch) type = FacilityType.RadiationShelter;
                        else { SetTarget(null); return; }
                        break;
                    default: continue;
                }

                foreach (AstraInteractable facility in FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                {
                    if (facility.facilityType != type || facility.facilityName.Contains("Practice")) continue;
                    SetTarget(facility.transform);
                    return;
                }

                SetTarget(null);
                return;
            }
            SetTarget(null);
        }

        public void ShowCompletion()
        {
            if (completionBadge == null)
            {
                completionBadge = new GameObject("ASTRO Completion Spark");
                TextMesh text = completionBadge.AddComponent<TextMesh>();
                text.text = "★  ✓  ★";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 90;
                text.characterSize = 0.18f;
                text.color = new Color(1f, 0.78f, 0.27f);
            }
            Transform astronaut = FindFirstObjectByType<AstraPlayerController>()?.transform;
            completionBadge.transform.position = astronaut != null ? astronaut.position + Vector3.up * 3f : Vector3.up * 3f;
            if (Camera.main != null) completionBadge.transform.rotation = Quaternion.LookRotation(completionBadge.transform.position - Camera.main.transform.position);
            completionBadge.SetActive(true);
            pulse = 2f;
        }

        private void LateUpdate()
        {
            if (pulse <= 0f || completionBadge == null) return;
            pulse -= Time.unscaledDeltaTime;
            completionBadge.transform.localScale = Vector3.one * (1f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.45f));
            if (pulse <= 0f) completionBadge.SetActive(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
