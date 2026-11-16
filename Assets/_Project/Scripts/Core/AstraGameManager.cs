using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Astra
{
    public class AstraGameManager : MonoBehaviour
    {
        public static AstraGameManager Instance { get; private set; }

        private AstraResourceManager resources;
        public float oxygen => resources != null ? resources.Oxygen : 100f;
        public float water => resources != null ? resources.Water : 100f;
        public float food => resources != null ? resources.Food : 100f;
        public float power => resources != null ? resources.Power : 82f;
        public float habitatCondition => resources != null ? resources.HabitatCondition : 100f;
        public float radiationExposureIndex { get; private set; }
        public bool shelterPrepared { get; private set; }
        public bool foodHarvested { get; private set; }
        public bool CropReady => foodBatchReady;
        public string DonkiSummary { get; private set; } = "Loading NASA DONKI solar-particle event context…";
        public string DonkiEventDate { get; private set; } = "Loading latest event…";
        public string DonkiEventType { get; private set; } = "Solar Energetic Particle (SEP)";
        public string DonkiEventDescription { get; private set; } = "Contacting NASA DONKI.";
        public string DonkiEventUrl { get; private set; } = "https://ccmc.gsfc.nasa.gov/DONKI/";
        public bool DonkiDataReady { get; private set; }

        public bool communicationsOnline { get; private set; }
        public bool solarOnline { get; private set; }
        public bool lifeSupportChecked { get; private set; }
        public bool eventResolved { get; private set; }
        public bool sampleCollected { get; private set; }
        public bool missionStarted { get; private set; }
        public bool missionPaused { get; private set; }
        public bool missionFinished { get; private set; }
        public float missionSeconds { get; private set; }

        public IReadOnlyList<MissionObjective> Objectives => objectives;
        public AstraMissionId CurrentMission { get; private set; } = AstraMissionId.FirstLight;
        public AstraMissionDefinition MissionDefinition => AstraMissionCatalog.Get(CurrentMission);
        public int MissionBestScore => AstraCampaignProgress.BestScore(CurrentMission);
        public AstraResourceManager Resources => resources;
        public bool CanControlPlayer =>
            (missionStarted && !missionPaused && !missionFinished && !eventOpen && (maintenance == null || !maintenance.IsRunning)) ||
            (OnboardingManager.Instance != null && OnboardingManager.Instance.AllowPlayerControl) ||
            (AstraEarthLaunchSequence.Instance != null && AstraEarthLaunchSequence.Instance.AllowPlayerControl);
        public bool EventOpen => eventOpen;
        public string LatestMessage { get; private set; } = "Outpost systems awaiting crew inspection.";
        public string EventFeedback { get; private set; } = string.Empty;

        private readonly List<MissionObjective> objectives = new List<MissionObjective>();

        private AstraHUD hud;
        private AstraMaintenanceSequence maintenance;
        private AstraGreenhouseDisplay greenhouseDisplay;
        private AstraRoverDrive roverDrive;
        private float resourceTick;
        private bool eventOpen;
        private bool safeDecision;
        private bool failure;
        private bool foodCycleStarted;
        private bool foodBatchReady;
        private int foodHarvestCount;
        private int oxygenRepairCount;
        private int waterRepairCount;
        private float foodCycleElapsed;
        private float foodCycleDuration = 20f;
        private float cropYield = 18f;
        private int selectedCrop = -1;
        private bool roverLaunchPaid;
        private bool roverRouteChosen;
        private bool sampleAnalysisPending;
        private bool spacecraftLaunchAvailable;
        private bool stormPowerChecked, stormLifeSupportChecked, stormCommsChecked, stormShelterChecked;
        private bool stormDrillActive, stormCrewSheltered;
        private float stormDrillElapsed;
        private float stormOutdoorPauseElapsed;
        private bool stormPauseWasDelayed, stormOutdoorWorkPaused;
        private int stormLastDisplayedSecond = -1;
        private float firstGreenhouseYield;
        private readonly HashSet<FacilityType> stormInspectionChecked = new HashSet<FacilityType>();
        public bool CanLaunchNextDestination => missionFinished && !failure && GetNextMission(out AstraMissionId next) &&
            AstraMissionCatalog.Get(next).Destination != MissionDefinition.Destination;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            resources = GetComponent<AstraResourceManager>();
            if (resources == null) resources = gameObject.AddComponent<AstraResourceManager>();
            maintenance = GetComponent<AstraMaintenanceSequence>();
            if (maintenance == null) maintenance = gameObject.AddComponent<AstraMaintenanceSequence>();
        }

        private void Start()
        {
            hud = FindFirstObjectByType<AstraHUD>();
            AstraMissionWorldSetup.RepairInvalidRenderMaterials();
            AstraMissionWorldSetup.EnsureMissionFacilities(AstraMissionId.FirstLight);
            AstraMissionWorldSetup.EnsureRadiationShelter();
            greenhouseDisplay = AstraMissionWorldSetup.EnsureGreenhouseDisplay();
            roverDrive = AstraMissionWorldSetup.EnsureRoverDrive();
            AstraMissionWorldSetup.EnsureSpacecraft();
            AstraRuntimeModelLoader nasaModelLoader = AstraRuntimeModelLoader.Instance;
            if (nasaModelLoader == null) nasaModelLoader = GetComponent<AstraRuntimeModelLoader>();
            if (nasaModelLoader == null) nasaModelLoader = gameObject.AddComponent<AstraRuntimeModelLoader>();
            int modeledFacilities = AstraMissionWorldSetup.ApplyNASAHardwareModels(nasaModelLoader);
            if (modeledFacilities > 0) Debug.Log("ASTRA loaded NASA 3D hardware models for " + modeledFacilities + " interactive facilities.");
            AstraMissionWorldSetup.ApplyNASALunarTerrain(nasaModelLoader);
            AstraMissionWorldSetup.RepairInvalidRenderMaterials();
            // Imported or generated renderers can appear later than the first startup scan.
            // Re-run once the scene's startup components have completed their first frame.
            StartCoroutine(RepairLateLoadedMaterials());
            hud?.Refresh(this);
        }

        private System.Collections.IEnumerator RepairLateLoadedMaterials()
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                AstraMissionWorldSetup.RepairInvalidRenderMaterials();
            }
        }

        private void Update()
        {
            if (!missionStarted || missionPaused || missionFinished || (maintenance != null && maintenance.IsRunning)) return;
            missionSeconds += Time.deltaTime;
            if (CurrentMission == AstraMissionId.StormWatch && stormDrillActive)
            {
                if (stormCrewSheltered) UpdateStormCrewShelterPresence();
                stormDrillElapsed += Time.deltaTime;
                int remaining = Mathf.Max(0, Mathf.CeilToInt(15f - stormDrillElapsed));
                if (remaining != stormLastDisplayedSecond)
                {
                    stormLastDisplayedSecond = remaining;
                    hud?.ShowMessage("SIMULATED MARS DUST STORM  ·  " + (stormCrewSheltered ? "CREW SHELTERED" : "MOVE TO THE PROTECTED AREA") + "  ·  " + remaining + "s");
                }
                if (stormPauseWasDelayed && !stormOutdoorWorkPaused)
                {
                    stormOutdoorPauseElapsed += Time.deltaTime;
                    if (stormOutdoorPauseElapsed >= 10f)
                    {
                        roverDrive?.SuspendScheduledTraverse();
                        stormOutdoorWorkPaused = true;
                        resources.AddPower(-4f);
                        LatestMessage = "Outdoor work paused after a 10-second delay. Scenario result: mission time +10 sec, battery −4 game units.";
                        hud?.ShowMessage(LatestMessage);
                        hud?.Refresh(this);
                    }
                }
                if (stormDrillElapsed >= 15f && stormCrewSheltered) CompleteStormResponse();
            }
            resourceTick += Time.deltaTime;
            if (resourceTick >= 10f)
            {
                resourceTick -= 10f;
                TickResources();
            }
            if (foodCycleStarted && !foodBatchReady && foodHarvestCount < MissionDefinition.CropHarvestsRequired)
            {
                foodCycleElapsed += Time.deltaTime;
                greenhouseDisplay?.SetGrowth(foodCycleElapsed / Mathf.Max(1f, foodCycleDuration));
                if (foodCycleElapsed >= foodCycleDuration)
                {
                    foodBatchReady = true;
                    greenhouseDisplay?.ShowReady();
                    LatestMessage = "Greenhouse crop cycle is ready. Return to the greenhouse to harvest it. This is a shortened game cycle.";
                    hud?.ShowMessage(LatestMessage);
                }
            }
            // The storm drill is a Storm Watch activity only. First Light is limited
            // to comms, solar positioning, and the initial life-support inspection.
            if (CurrentMission == AstraMissionId.StormWatch && HasObjective(AstraObjectiveId.ProtectedAreaReadiness) &&
                !IsObjectiveComplete(AstraObjectiveId.ProtectedAreaReadiness) && !eventOpen && !eventResolved && !stormDrillActive)
                OpenRadiationDrill();
            if (oxygen <= 0f || water <= 0f || habitatCondition <= 0f)
                FailMission();
        }

        public void BeginMission(AstraMissionId missionId)
        {
            StopCoroutine(nameof(LoadDonkiContext));
            AstraMissionWorldSetup.EnsureMissionFacilities(missionId);
            AstraMissionWorldSetup.EnsureRadiationShelter();
            AstraMissionWorldSetup.ApplyDestination(AstraMissionCatalog.Get(missionId).Destination);
            AstraMissionWorldSetup.EnsureSpacecraft();
            if (missionId == AstraMissionId.StormWatch) AstraMissionWorldSetup.EnsureSpaceWeatherStation();
            AstraRuntimeModelLoader nasaModelLoader = AstraRuntimeModelLoader.Instance;
            if (nasaModelLoader == null) nasaModelLoader = GetComponent<AstraRuntimeModelLoader>();
            if (nasaModelLoader == null) nasaModelLoader = gameObject.AddComponent<AstraRuntimeModelLoader>();
            AstraMissionWorldSetup.ApplyNASAHardwareModels(nasaModelLoader);
            AstraMissionWorldSetup.ApplyNASALunarTerrain(nasaModelLoader);
            AstraMissionWorldSetup.RepairInvalidRenderMaterials();
            CurrentMission = missionId;
            missionStarted = true;
            missionFinished = false;
            missionPaused = false;
            missionSeconds = 0f;
            resourceTick = 0f;
            eventOpen = false;
            safeDecision = false;
            failure = false;
            communicationsOnline = false;
            solarOnline = false;
            lifeSupportChecked = false;
            eventResolved = false;
            sampleCollected = false;
            shelterPrepared = false;
            foodHarvested = false;
            foodCycleStarted = false;
            foodBatchReady = false;
            foodHarvestCount = 0;
            oxygenRepairCount = 0;
            waterRepairCount = 0;
            roverLaunchPaid = false;
            stormPowerChecked = stormLifeSupportChecked = stormCommsChecked = stormShelterChecked = false;
            stormDrillActive = stormCrewSheltered = false;
            stormOutdoorPauseElapsed = 0f;
            stormPauseWasDelayed = stormOutdoorWorkPaused = false;
            stormWeatherReportReviewed = false;
            stormDrillElapsed = 0f;
            stormLastDisplayedSecond = -1;
            stormInspectionChecked.Clear();
            foodCycleElapsed = 0f;
            firstGreenhouseYield = 0f;
            foodCycleDuration = 20f;
            cropYield = 18f;
            selectedCrop = -1;
            roverRouteChosen = false;
            sampleAnalysisPending = false;
            radiationExposureIndex = 0f;
            EventFeedback = string.Empty;
            DonkiDataReady = false;
            DonkiEventDate = "Loading latest event…";
            DonkiEventType = "Solar Energetic Particle (SEP)";
            DonkiEventDescription = "Contacting NASA DONKI.";
            DonkiEventUrl = "https://ccmc.gsfc.nasa.gov/DONKI/";
            DonkiSummary = "NASA DONKI event context is loading.";
            objectives.Clear();
            foreach (AstraObjectiveId objective in MissionDefinition.Objectives)
                objectives.Add(new MissionObjective(objective));
            SetMissionStartingResources();
            foreach (AstraInteractable facility in FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
            {
                facility.isOnline = false;
                facility.ResetCompletionFeedback();
            }
            greenhouseDisplay = AstraMissionWorldSetup.EnsureGreenhouseDisplay();
            greenhouseDisplay?.ResetVisuals();
            roverDrive = AstraMissionWorldSetup.EnsureRoverDrive();
            roverDrive?.ResetForMission();
            if (CurrentMission == AstraMissionId.StormWatch) roverDrive?.BeginScheduledTraverse();
            FindFirstObjectByType<AstraPlayerController>()?.ResetForMission();
            missionStarted = true;
            if (CurrentMission == AstraMissionId.StormWatch)
                StartCoroutine(LoadDonkiContext());
            LatestMessage = "Mission clock started. " + MissionDefinition.Briefing;
            hud?.SetIntroVisible(false);
            hud?.SetPauseVisible(false);
            hud?.SetMissionHudVisible(true);
            hud?.HideReport();
            hud?.ShowMessage(LatestMessage);
            hud?.SetCursorForMission(true);
            hud?.Refresh(this);
            VisualHintManager.Instance?.RefreshMissionTarget(this);
        }

        private void SetMissionStartingResources()
        {
            switch (CurrentMission)
            {
                case AstraMissionId.FirstLight: resources.SetMissionResources(82f, 48f, 72f, 48f, 96f); break;
                case AstraMissionId.AirAndWater: resources.SetMissionResources(62f, 36f, 78f, 72f, 100f); break;
                case AstraMissionId.StormWatch: resources.SetMissionResources(82f, 78f, 72f, 48f, 96f); break;
                case AstraMissionId.FoodForTheLongStay: resources.SetMissionResources(82f, 66f, 34f, 62f, 96f); break;
                default: resources.SetMissionResources(100f, 100f, 100f, 82f, 100f); break;
            }
        }

        public void TogglePause()
        {
            if (!missionStarted || missionFinished || eventOpen || (maintenance != null && maintenance.IsRunning)) return;
            missionPaused = !missionPaused;
            hud?.SetPauseVisible(missionPaused);
            hud?.SetCursorForMission(!missionPaused);
        }

        public void TryInteract(Vector3 playerPosition)
        {
            if (AstraEarthLaunchSequence.Instance != null && AstraEarthLaunchSequence.Instance.IsRunning)
            {
                AstraEarthLaunchSequence.Instance.TryInteract(playerPosition);
                return;
            }

            AstraPlayerController player = FindFirstObjectByType<AstraPlayerController>();
            AstraInteractable nearest = player != null ? player.GetNearestFacility() : null;
            if (OnboardingManager.Instance != null && OnboardingManager.Instance.IsPracticeActive)
            {
                if (nearest == null)
                {
                    VoiceGuideManager.Instance?.Say("tutorial.interact.body", "Walk to the glowing practice panel and press the interaction key.", "hint_interact", true);
                    return;
                }
                OnboardingManager.Instance.HandlePracticeInteraction(nearest);
                return;
            }
            if (nearest == null)
            {
                hud?.ShowMessage("No object in interaction range. Follow the highlighted waypoint; press E when the object is in range.");
                return;
            }
            nearest.Interact(this);
        }

        public void InteractWith(AstraInteractable facility)
        {
            if (facility == null || !missionStarted || missionPaused || eventOpen) return;
            if (ShouldRunMaintenance(facility))
            {
                maintenance?.Begin(facility);
                return;
            }
            ApplyInteraction(facility);
        }

        public void CompleteMaintenance(AstraInteractable facility)
        {
            if (facility == null || !missionStarted || missionPaused || eventOpen) return;
            ApplyInteraction(facility);
            facility.PlayCompletionFeedback();
            hud?.Refresh(this);
        }

        private bool ShouldRunMaintenance(AstraInteractable facility)
        {
            if (facility.facilityType == FacilityType.Communications || facility.facilityType == FacilityType.SolarArray ||
                facility.facilityType == FacilityType.LifeSupport || facility.facilityType == FacilityType.Greenhouse ||
                facility.facilityType == FacilityType.RadiationShelter || facility.facilityType == FacilityType.OxygenProcessor ||
                facility.facilityType == FacilityType.WaterRecycler)
                return false;
            if (CurrentMission == AstraMissionId.StormWatch &&
                (facility.facilityType == FacilityType.Communications || facility.facilityType == FacilityType.SolarArray ||
                 facility.facilityType == FacilityType.LifeSupport || facility.facilityType == FacilityType.RadiationShelter))
                return false;
            switch (facility.facilityType)
            {
                case FacilityType.Communications: return !communicationsOnline;
                case FacilityType.SolarArray: return !solarOnline;
                case FacilityType.LifeSupport: return !lifeSupportChecked;
                case FacilityType.Habitat: return habitatCondition < 100f;
                case FacilityType.Greenhouse:
                    return HasObjective(AstraObjectiveId.CropHarvest) && !foodCycleStarted && foodHarvestCount < MissionDefinition.CropHarvestsRequired && resources.Power >= 8f && resources.Water >= 8f;
                case FacilityType.RadiationShelter: return !shelterPrepared && resources.Power >= 8f;
                case FacilityType.OxygenProcessor:
                    return resources.Power >= 4f && (CurrentMission != AstraMissionId.AirAndWater ||
                        (waterRepairCount > 0 && resources.Water >= 4f));
                case FacilityType.WaterRecycler:
                    return resources.Power >= 4f;
                case FacilityType.Rover: return false;
                default: return false;
            }
        }

        private bool HasObjective(AstraObjectiveId id)
        {
            foreach (MissionObjective objective in objectives) if (objective.id == id) return true;
            return false;
        }

        private bool IsObjectiveComplete(AstraObjectiveId id)
        {
            foreach (MissionObjective objective in objectives)
                if (objective.id == id) return objective.complete;
            return false;
        }

        private bool IsStormReadinessPhase => CurrentMission == AstraMissionId.StormWatch &&
            HasObjective(AstraObjectiveId.ProtectedAreaReadiness) && !IsObjectiveComplete(AstraObjectiveId.ProtectedAreaReadiness);

        private bool IsStormInspectionPhase => CurrentMission == AstraMissionId.StormWatch &&
            eventResolved && HasObjective(AstraObjectiveId.PostDrillInspection) && !IsObjectiveComplete(AstraObjectiveId.PostDrillInspection);

        private void CheckStormReadinessComplete()
        {
            if (!IsStormReadinessPhase || !stormWeatherReportReviewed || !stormPowerChecked || !stormLifeSupportChecked ||
                !stormCommsChecked || !stormShelterChecked) return;
            shelterPrepared = true;
            AstraInteractable shelter = FindFacility(FacilityType.RadiationShelter)?.GetComponent<AstraInteractable>();
            if (shelter != null) shelter.isOnline = true;
            CompleteObjective(AstraObjectiveId.ProtectedAreaReadiness);
            LatestMessage = "Mars dust-storm equipment is ready. Choose when to pause the rover traverse; the delay changes mission time and the fictional battery level.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
        }

        private bool stormWeatherReportReviewed;

        public Transform GetStormReadinessTarget()
        {
            if (!stormWeatherReportReviewed) return FindFacility(FacilityType.SpaceWeatherStation);
            if (!stormPowerChecked) return FindFacility(FacilityType.SolarArray);
            if (!stormLifeSupportChecked) return FindFacility(FacilityType.LifeSupport);
            if (!stormCommsChecked) return FindFacility(FacilityType.Communications);
            if (!stormShelterChecked) return FindFacility(FacilityType.RadiationShelter);
            return FindFacility(FacilityType.RadiationShelter);
        }

        public Transform GetStormInspectionTarget()
        {
            if (!stormInspectionChecked.Contains(FacilityType.SolarArray)) return FindFacility(FacilityType.SolarArray);
            if (!stormInspectionChecked.Contains(FacilityType.LifeSupport)) return FindFacility(FacilityType.LifeSupport);
            if (!stormInspectionChecked.Contains(FacilityType.Communications)) return FindFacility(FacilityType.Communications);
            if (!stormInspectionChecked.Contains(FacilityType.SpaceWeatherStation)) return FindFacility(FacilityType.SpaceWeatherStation);
            if (!stormInspectionChecked.Contains(FacilityType.RadiationShelter)) return FindFacility(FacilityType.RadiationShelter);
            return FindFacility(FacilityType.SpaceWeatherStation);
        }

        public Transform GetActiveObjectiveTarget()
        {
            MissionObjective active = null;
            foreach (MissionObjective objective in objectives)
                if (!objective.complete) { active = objective; break; }
            if (active == null) return null;

            switch (active.id)
            {
                case AstraObjectiveId.Communications: return FindFacility(FacilityType.Communications);
                case AstraObjectiveId.Solar: return FindFacility(FacilityType.SolarArray);
                case AstraObjectiveId.LifeSupport: return FindFacility(FacilityType.LifeSupport);
                case AstraObjectiveId.Shelter: return FindFacility(FacilityType.RadiationShelter);
                case AstraObjectiveId.CropHarvest: return FindFacility(FacilityType.Greenhouse);
                case AstraObjectiveId.StormResponse: return FindFacility(FacilityType.RadiationShelter);
                case AstraObjectiveId.RoverSample:
                case AstraObjectiveId.EuropaSiteInspected:
                    return roverDrive != null && roverDrive.SampleSite != null ? roverDrive.SampleSite : FindFacility(FacilityType.Rover);
                case AstraObjectiveId.OxygenRestored: return FindFacility(FacilityType.OxygenProcessor);
                case AstraObjectiveId.WaterRestored: return FindFacility(FacilityType.WaterRecycler);
                case AstraObjectiveId.WeatherReportReviewed: return FindFacility(FacilityType.SpaceWeatherStation);
                case AstraObjectiveId.ProtectedAreaReadiness: return GetStormReadinessTarget();
                case AstraObjectiveId.PostDrillInspection: return GetStormInspectionTarget();
                case AstraObjectiveId.EuropaRoutePlanned: return FindFacility(FacilityType.Rover);
                default: return null;
            }
        }

        private static Transform FindFacility(FacilityType type)
        {
            foreach (AstraInteractable facility in FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (facility.facilityType == type && !facility.facilityName.Contains("Practice")) return facility.transform;
            return null;
        }

        private void RecordStormInspection(FacilityType type)
        {
            if (!IsStormInspectionPhase) return;
            stormInspectionChecked.Add(type);
            foreach (MissionObjective objective in objectives)
                if (objective.id == AstraObjectiveId.PostDrillInspection)
                    objective.title = "Inspect outpost systems (" + stormInspectionChecked.Count + "/5) and submit the report";
            VisualHintManager.Instance?.RefreshMissionTarget(this);
            hud?.Refresh(this);
        }

        public string BuildStormInspectionChecklist()
        {
            return CheckLine(FacilityType.SolarArray, "Power system", "battery " + Mathf.RoundToInt(power) + " game units") + "\n" +
                   CheckLine(FacilityType.LifeSupport, "Life support", "O₂ " + Mathf.RoundToInt(oxygen) + " · H₂O " + Mathf.RoundToInt(water) + " · habitat " + Mathf.RoundToInt(habitatCondition) + "% (game values)") + "\n" +
                   CheckLine(FacilityType.Communications, "Communications link", communicationsOnline ? "connected to Mission Control" : "not connected") + "\n" +
                   CheckLine(FacilityType.SpaceWeatherStation, "Monitoring equipment", DonkiEventType + " · " + DonkiEventDate) + "\n" +
                   CheckLine(FacilityType.RadiationShelter, "Crew and protected area", stormCrewSheltered ? "crew accounted for" : "area inspected after drill");
        }

        private string CheckLine(FacilityType type, string label, string status) =>
            (stormInspectionChecked.Contains(type) ? "✓  " : "○  ") + label + " — " + status;

        public bool CanSubmitStormInspection => IsStormInspectionPhase &&
            stormInspectionChecked.Contains(FacilityType.SolarArray) && stormInspectionChecked.Contains(FacilityType.LifeSupport) &&
            stormInspectionChecked.Contains(FacilityType.Communications) && stormInspectionChecked.Contains(FacilityType.SpaceWeatherStation) &&
            stormInspectionChecked.Contains(FacilityType.RadiationShelter);

        public void ConfirmWeatherReportReviewed()
        {
            if (CurrentMission != AstraMissionId.StormWatch || !DonkiDataReady || stormWeatherReportReviewed) return;
            stormWeatherReportReviewed = true;
            hud?.HideStormPanel();
            CompleteObjective(AstraObjectiveId.WeatherReportReviewed);
            LatestMessage = "Mars dust-storm scenario report reviewed. This is a fictional local-weather exercise, not a live forecast.";
            hud?.ShowMessage(LatestMessage);
            CheckStormReadinessComplete();
        }

        public void SubmitStormInspectionReport()
        {
            if (!CanSubmitStormInspection) return;
            hud?.HideStormPanel();
            CompleteObjective(AstraObjectiveId.PostDrillInspection);
            LatestMessage = "Post-drill report submitted. The crew, protected area, and essential outpost systems are accounted for.";
            hud?.ShowMessage(LatestMessage);
            TryFinishMissionIfReady();
        }

        private bool GetNextMission(out AstraMissionId next)
        {
            int index = (int)CurrentMission + 1;
            next = (AstraMissionId)Mathf.Clamp(index, 0, AstraMissionCatalog.Count - 1);
            return index < AstraMissionCatalog.Count;
        }

        public void StartNextDestinationLaunch()
        {
            if (!CanLaunchNextDestination || maintenance == null) return;
            GetNextMission(out AstraMissionId next);
            AstraInteractable spacecraft = AstraMissionWorldSetup.EnsureSpacecraft();
            if (spacecraft == null) return;
            spacecraftLaunchAvailable = true;
            hud?.HideReport();
            maintenance.BeginLaunchRepair(spacecraft, next);
        }

        public void CompleteSpacecraftLaunch(AstraMissionId nextMission)
        {
            if (!spacecraftLaunchAvailable || !AstraCampaignProgress.IsUnlocked(nextMission)) return;
            spacecraftLaunchAvailable = false;
            AstraCampaignProgress.MarkDestinationReached(AstraMissionCatalog.Get(nextMission).Destination);
            AstraMissionWorldSetup.SetSpacecraftRepaired(true);
            BeginMission(nextMission);
            hud?.ShowMessage("Spacecraft repaired. Launch complete: arriving at " + AstraMissionCatalog.DestinationName(MissionDefinition.Destination) + ".");
        }

        public void CancelSpacecraftLaunchRepair()
        {
            spacecraftLaunchAvailable = false;
            hud?.ShowReport(CalculateScore(), true, BuildLearningSummary(), MissionDefinition, MissionBestScore,
                (int)CurrentMission < AstraMissionCatalog.Count - 1);
        }

        private void ApplyInteraction(AstraInteractable facility)
        {
            if (CurrentMission == AstraMissionId.StormWatch && stormDrillActive && facility.facilityType != FacilityType.RadiationShelter)
            {
                LatestMessage = "The simulated event is active. Leave the rover and move directly to the protected area.";
                hud?.ShowMessage(LatestMessage);
                return;
            }

            switch (facility.facilityType)
            {
                case FacilityType.Communications:
                    if (CurrentMission == AstraMissionId.StormWatch)
                    {
                        if (IsStormReadinessPhase)
                        {
                            hud?.ShowActivityChoice("TEST THE MARS RELAY LINK", "Inspect the communication status, then select the link test. Signal quality is a fictional training value.", "PRIMARY RELAY\n98% signal · uses 2 power", "BACKUP BEACON\n86% signal · saves power", choice => CompleteCommunicationsInteraction(facility, choice, true));
                            LatestMessage = "Choose a relay test to verify the outpost link.";
                        }
                        else if (IsStormInspectionPhase)
                        {
                            RecordStormInspection(FacilityType.Communications);
                            facility.PlayCompletionFeedback();
                            LatestMessage = "Post-drill communications check passed. Signal quality: 98% (fictional exercise reading).";
                        }
                        else LatestMessage = "Communications status is available on the mission console.";
                        break;
                    }
                    if (CurrentMission != AstraMissionId.FirstLight)
                    {
                        LatestMessage = "Communications commissioning is a First Light activity. Storm Watch uses its separate readiness check.";
                        break;
                    }
                    if (!communicationsOnline)
                    {
                        hud?.ShowActivityChoice("CALIBRATE THE COMMUNICATIONS DISH", "The dish is not aligned. Compare the stronger relay signal with the lower-power backup, then choose how to bring Mission Control online.", "TRACK LUNAR RELAY\n98% signal · uses 2 power", "LOW-POWER BEACON\n86% signal · saves power", choice => CompleteCommunicationsInteraction(facility, choice, false));
                        LatestMessage = "Select a communication strategy to commission the station.";
                    }
                    else
                    {
                        LatestMessage = "Communications are online. Mission Control confirms signal quality is nominal.";
                        CompleteObjective(AstraObjectiveId.Communications);
                    }
                    break;
                case FacilityType.SolarArray:
                    if (CurrentMission == AstraMissionId.StormWatch)
                    {
                        if (IsStormReadinessPhase)
                        {
                            solarOnline = true;
                            facility.isOnline = true;
                            resources.AddPower(4f);
                            facility.PlayCompletionFeedback();
                            stormPowerChecked = true;
                            LatestMessage = "Power check passed. Solar generation is online; battery reserve is " + Mathf.RoundToInt(power) + " game units.";
                            CheckStormReadinessComplete();
                        }
                        else if (IsStormInspectionPhase)
                        {
                            RecordStormInspection(FacilityType.SolarArray);
                            facility.PlayCompletionFeedback();
                            LatestMessage = "Post-drill power inspection passed. Battery reserve: " + Mathf.RoundToInt(power) + " game units; solar array online.";
                        }
                        else LatestMessage = "Power system status is available on the mission console.";
                        break;
                    }
                    if (CurrentMission != AstraMissionId.FirstLight)
                    {
                        LatestMessage = "Solar-array repositioning is a First Light activity. Storm Watch checks the array only during its drill preparation and debrief.";
                        break;
                    }
                    if (CurrentMission == AstraMissionId.FirstLight && !solarOnline)
                    {
                        hud?.ShowActivityChoice("REPOSITION THE SOLAR ARRAY", "BEFORE: " + Mathf.RoundToInt(power) + " battery units. Compare the projected readings, reposition the array, then inspect the new reading. These are fictional game units.", "TRACK THE SUN\nUp to +10 power", "PROTECT THE ARRAY\nUp to +5 power", ChooseSolarAlignment);
                        LatestMessage = "Review the two power strategies and select an array operating plan.";
                        break;
                    }
                    if (!solarOnline)
                    {
                        solarOnline = true;
                        facility.isOnline = true;
                        resources.AddPower(8f);
                        LatestMessage = "Solar array is tracking the Sun. The outpost battery is charging.";
                    }
                    else LatestMessage = "Solar array is online and supplying the outpost.";
                    CompleteObjective(AstraObjectiveId.Solar);
                    break;
                case FacilityType.LifeSupport:
                    if (CurrentMission == AstraMissionId.StormWatch)
                    {
                        if (IsStormReadinessPhase)
                        {
                            hud?.ShowActivityChoice("INSPECT LIFE-SUPPORT READINGS", "Review the habitat meters, then choose which loop to test before the Mars drill. Values are fictional game readings.", "CHECK WATER LOOP\nverify recycler reserve", "CHECK AIR LOOP\nverify oxygen reserve", choice => CompleteLifeSupportInteraction(facility, choice, true));
                            LatestMessage = "Choose a life-support loop to test.";
                        }
                        else if (IsStormInspectionPhase)
                        {
                            RecordStormInspection(FacilityType.LifeSupport);
                            facility.PlayCompletionFeedback();
                            LatestMessage = "Post-drill life-support inspection: O₂ " + Mathf.RoundToInt(oxygen) + ", H₂O " + Mathf.RoundToInt(water) + ", habitat " + Mathf.RoundToInt(habitatCondition) + "% (game values).";
                        }
                        else LatestMessage = "Life-support status is available on the mission console.";
                        break;
                    }
                    if (CurrentMission != AstraMissionId.FirstLight)
                    {
                        LatestMessage = "The life-support reading is shown on the mission resource display. The inspection task belongs to First Light.";
                        break;
                    }
                    if (!lifeSupportChecked)
                    {
                        hud?.ShowActivityChoice("RUN A LIFE-SUPPORT DIAGNOSTIC", "Current readings: O₂ " + Mathf.RoundToInt(oxygen) + " · H₂O " + Mathf.RoundToInt(water) + " · habitat " + Mathf.RoundToInt(habitatCondition) + "%. Choose which subsystem to inspect first.", "WATER LOOP\ncheck recovery and storage", "AIR LOOP\ncheck filters and oxygen", choice => CompleteLifeSupportInteraction(facility, choice, false));
                        LatestMessage = "Choose a subsystem and inspect its response.";
                    }
                    else
                    {
                        LatestMessage = "Life-support loop is stable. Keep an eye on the resource dashboard.";
                        CompleteObjective(AstraObjectiveId.LifeSupport);
                    }
                    break;
                case FacilityType.Habitat:
                    resources.RepairHabitat(3f);
                    LatestMessage = "Habitat seal inspection complete. Condition is " + Mathf.RoundToInt(habitatCondition) + "%.";
                    break;
                case FacilityType.Greenhouse:
                    if (!HasObjective(AstraObjectiveId.CropHarvest))
                        LatestMessage = "Crop planning and harvests are part of the Food for the Long Stay mission.";
                    else if (foodHarvestCount >= MissionDefinition.CropHarvestsRequired)
                        LatestMessage = "The greenhouse batch has been harvested. The CSA learning link is in BRIEFING / SCIENCE.";
                    else if (foodBatchReady)
                    {
                        resources.AddFood(cropYield);
                        foodHarvested = true;
                        foodHarvestCount++;
                        foodBatchReady = false;
                        int needed = MissionDefinition.CropHarvestsRequired;
                        greenhouseDisplay?.ShowHarvest(foodHarvestCount);
                        if (foodHarvestCount >= needed) CompleteObjective(AstraObjectiveId.CropHarvest);
                        LatestMessage = "Crop harvest " + foodHarvestCount + "/" + needed + ": food +" + Mathf.RoundToInt(cropYield) + " game units. Crop cycle costs and yields are fictional.";
                        if (CurrentMission == AstraMissionId.FoodForTheLongStay)
                        {
                            if (foodHarvestCount == 1) firstGreenhouseYield = cropYield;
                            else LatestMessage += " Compare recorded yields: cycle 1 " + Mathf.RoundToInt(firstGreenhouseYield) + " vs cycle 2 " + Mathf.RoundToInt(cropYield) + " food units.";
                        }
                        foodCycleStarted = false;
                        selectedCrop = -1;
                        if (CurrentMission == AstraMissionId.FoodForTheLongStay && foodHarvestCount < needed)
                            LatestMessage += " Start the next crop cycle at the greenhouse.";
                    }
                    else if (foodCycleStarted)
                    {
                        LatestMessage = "The crop is growing. Return when the greenhouse indicator says the batch is ready.";
                    }
                    else
                    {
                        hud?.ShowActivityChoice("CROP + GROWING CONDITIONS", "Choose a crop and light/water plan. After two harvests, compare recorded yields and resource costs. All values are fictional game settings.", "LEAFY GREENS\nBright light · 8 water · 8 power · 20 sec · +18 food", "DWARF BEANS\nModerate light · 12 water · 8 power · 30 sec · +24 food", ChooseGreenhouseCrop);
                        LatestMessage = "Choose a crop and its light/water conditions, then watch the shortened growth cycle.";
                    }
                    break;
                case FacilityType.RadiationShelter:
                    if (CurrentMission != AstraMissionId.StormWatch)
                    {
                        LatestMessage = "The protected-area drill is part of the Storm Watch mission.";
                        break;
                    }
                    if (CurrentMission == AstraMissionId.StormWatch)
                    {
                        if (stormDrillActive)
                        {
                            stormCrewSheltered = true;
                            LatestMessage = "Crew accounted for in the protected area. Stay here while the simulated event is monitored.";
                            if (stormDrillElapsed >= 15f) CompleteStormResponse();
                        }
                        else if (IsStormReadinessPhase)
                        {
                            hud?.ShowActivityChoice("PREPARE THE PROTECTED AREA", "Choose how to prepare. Testing the pressure door gives stronger confidence but spends a little simulated power.", "TEST PRESSURE DOOR\nspend 2 power", "CLEAR SHELTER ACCESS\nsave power", choice => CompleteShelterPreparation(facility, choice, true));
                            LatestMessage = "Choose a shelter preparation check.";
                        }
                        else if (IsStormInspectionPhase)
                        {
                            RecordStormInspection(FacilityType.RadiationShelter);
                            facility.PlayCompletionFeedback();
                            LatestMessage = "Post-drill shelter inspection passed. Crew count and protected-area status are confirmed.";
                        }
                        else LatestMessage = "The protected area is ready for the space-weather exercise.";
                        break;
                    }
                    if (shelterPrepared)
                        LatestMessage = "Shelter is prepared. Using it during the simulated solar-particle drill costs less power and lowers ASTRA's fictional exposure index.";
                    else if (!TrySpendPower(8f))
                        LatestMessage = "Shelter preparation needs 8 game power. Bring the solar array online first.";
                    else
                    {
                        CompleteShelterPreparation(facility, 1, false);
                    }
                    break;
                case FacilityType.OxygenProcessor:
                    if (CurrentMission != AstraMissionId.AirAndWater)
                        LatestMessage = "Oxygen generation is an activity in the Air & Water mission.";
                    else if (waterRepairCount == 0)
                        LatestMessage = "Recover clean water first. NASA's oxygen-generation process uses water as an input.";
                    else if (resources.Water < 2f)
                        LatestMessage = "The oxygen generator needs at least 2 game water. Recover water first.";
                    else if (oxygenRepairCount > 0)
                        LatestMessage = "The oxygen-generation activity is complete. Current reserve: " + Mathf.RoundToInt(oxygen) + "% (fictional game value).";
                    else
                    {
                        hud?.ShowActivityChoice("SET THE OXYGEN GENERATOR", "Current O₂: " + Mathf.RoundToInt(oxygen) + " · water: " + Mathf.RoundToInt(water) + " · power: " + Mathf.RoundToInt(power) + ". Choose an output plan; both use water and electricity.", "BOOST OUTPUT\n+10 O₂ · −4 water · −4 power", "ECONOMY MODE\n+6 O₂ · −2 water · −2 power", ChooseOxygenPlan);
                        LatestMessage = "Choose an oxygen-generation plan and compare its resource cost.";
                    }
                    break;
                case FacilityType.WaterRecycler:
                    if (CurrentMission != AstraMissionId.AirAndWater)
                        LatestMessage = "Water recovery is an activity in the Air & Water mission.";
                    else if (waterRepairCount > 0)
                        LatestMessage = "The recycler cycle is complete. Current water reserve: " + Mathf.RoundToInt(water) + "% (fictional game value).";
                    else if (resources.Power < 2f)
                        LatestMessage = "The recycler needs at least 2 game power. Restore power before running a cycle.";
                    else
                    {
                        hud?.ShowActivityChoice("RUN THE WATER RECYCLER", "Inspect the available reserves, then choose a cycle. Output and costs are simplified game units.", "FULL RECLAMATION\n+10 water · −4 power", "ECO CYCLE\n+6 water · −2 power", ChooseWaterCycle);
                        LatestMessage = "Choose a recycler cycle; its water and power meters will update.";
                    }
                    break;
                case FacilityType.ResearchLab:
                    LatestMessage = "Research note: lunar samples can reveal how the Moon formed and changed. This mission sample is virtual.";
                    break;
                case FacilityType.Rover:
                    if (CurrentMission == AstraMissionId.StormWatch)
                    {
                        LatestMessage = !eventResolved
                            ? "The scheduled rover traverse is in progress. Suspend outdoor work from the Storm Watch alert."
                            : "The Storm Watch traverse is complete. Review the outpost checks at the weather console.";
                        break;
                    }
                    if (CurrentMission != AstraMissionId.ShadowScout)
                    {
                        LatestMessage = "Rover sample survey is part of the Shadow Scout mission.";
                        break;
                    }
                    if (CurrentMission == AstraMissionId.ShadowScout && !roverRouteChosen)
                    {
                        hud?.ShowActivityChoice("EUROPA SURVEY ROUTE", "Choose a fictional surface traverse. The route affects rover travel and lighting in this simulation; neither choice claims to identify subsurface ocean material.", "ILLUMINATED RIDGE\nLonger · clear terrain", "SHADOW EDGE\nShorter · low light", ChooseEuropaRoute);
                        LatestMessage = "Plan the survey route before deploying the rover.";
                        break;
                    }
                    if (!sampleCollected)
                    {
                        if (!roverLaunchPaid && !TrySpendPower(6f))
                        {
                            LatestMessage = "Rover launch needs 6 power units. Restore power before collecting the sample.";
                            break;
                        }
                        roverLaunchPaid = true;
                        roverDrive = facility.GetComponent<AstraRoverDrive>();
                        if (roverDrive == null) roverDrive = facility.gameObject.AddComponent<AstraRoverDrive>();
                        roverDrive.BeginDrive();
                        LatestMessage = "Rover deployed. Drive to the orange beacon and press E to collect the virtual sample. Press Q to leave the rover.";
                    }
                    else LatestMessage = "Sample container sealed and ready for lab analysis.";
                    break;
                case FacilityType.Spacecraft:
                    LatestMessage = "The spacecraft is available for repair and launch from the mission debrief after this mission is complete.";
                    break;
                case FacilityType.SpaceWeatherStation:
                    if (CurrentMission != AstraMissionId.StormWatch)
                    {
                        LatestMessage = "The space-weather console is reserved for the Mars Storm Watch mission.";
                    }
                    else if (!stormWeatherReportReviewed)
                    {
                        hud?.ShowDonkiBriefing(DonkiEventDate, DonkiEventType, DonkiEventDescription, DonkiEventUrl,
                            DonkiDataReady, ConfirmWeatherReportReviewed);
                        LatestMessage = DonkiDataReady ? "Review the event date, type, and description, then confirm the report." : "Waiting for NASA DONKI data before the report can be reviewed.";
                    }
                    else if (IsStormInspectionPhase)
                    {
                        RecordStormInspection(FacilityType.SpaceWeatherStation);
                        facility.PlayCompletionFeedback();
                        hud?.ShowStormInspectionReport(BuildStormInspectionChecklist(), CanSubmitStormInspection, SubmitStormInspectionReport);
                        LatestMessage = CanSubmitStormInspection ? "All inspection checks are complete. Submit the post-drill report." : "Review the remaining outpost checks, then return here to submit the report.";
                    }
                    else LatestMessage = "DONKI event details and drill status are available at this station.";
                    break;
            }
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        public void ShowMessage(string message)
        {
            LatestMessage = message;
            hud?.ShowMessage(message);
        }

        private void TickResources()
        {
            string oldWarning = resources.GetWarning();
            resources.AdvanceTimeStep(solarOnline, lifeSupportChecked);
            string warning = resources.GetWarning();
            if (!string.IsNullOrEmpty(warning) && warning != oldWarning)
            {
                LatestMessage = warning;
                hud?.ShowMessage(warning);
            }
            else if (string.IsNullOrEmpty(warning) && !string.IsNullOrEmpty(oldWarning))
            {
                LatestMessage = "Resource reserves have recovered to a stable game range.";
                hud?.ShowMessage(LatestMessage);
            }
        }

        private bool TrySpendPower(float amount) => resources.TrySpendPower(amount);

        private void OpenRadiationDrill()
        {
            eventOpen = true;
            missionPaused = true;
            EventFeedback = string.Empty;
            LatestMessage = "Storm Watch alert: choose when to pause the Mars rover traverse, then guide the astronaut to the protected area.";
            hud?.SetEventVisible(true);
            hud?.SetCursorForMission(false);
        }

        public void ResolveRadiationDrill(bool shelterCrew)
        {
            if (!eventOpen || CurrentMission != AstraMissionId.StormWatch) return;
            stormPauseWasDelayed = !shelterCrew;
            stormOutdoorWorkPaused = shelterCrew;
            stormOutdoorPauseElapsed = 0f;
            if (shelterCrew) roverDrive?.SuspendScheduledTraverse();
            stormDrillActive = true;
            stormCrewSheltered = false;
            stormDrillElapsed = 0f;
            stormLastDisplayedSecond = -1;
            eventOpen = false;
            missionPaused = false;
            LatestMessage = shelterCrew
                ? "Rover paused immediately. Guide the astronaut to the protected area; compare the mission clock and battery after the drill."
                : "You chose to delay the pause. The rover will continue for 10 seconds; mission time advances and the scenario will deduct 4 fictional battery units before outdoor work stops.";
            hud?.SetEventVisible(false);
            hud?.SetCursorForMission(true);
            hud?.ShowMessage(LatestMessage);
            VisualHintManager.Instance?.RefreshMissionTarget(this);
        }

        private void CompleteStormResponse()
        {
            if (!stormDrillActive || !stormCrewSheltered) return;
            stormDrillActive = false;
            eventResolved = true;
            safeDecision = true;
            radiationExposureIndex = 5f;
            EventFeedback = "The rover traverse was suspended, the crew was accounted for in the protected area, and the simulated event was monitored to completion. The exposure index is fictional, not a dose measurement.";
            CompleteObjective(AstraObjectiveId.StormResponse);
            LatestMessage = "Drill complete. Begin the post-event inspection at the power system.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            VisualHintManager.Instance?.RefreshMissionTarget(this);
        }

        private void UpdateStormCrewShelterPresence()
        {
            AstraPlayerController player = FindFirstObjectByType<AstraPlayerController>();
            Transform shelter = FindFacility(FacilityType.RadiationShelter);
            if (player == null || shelter == null) { stormCrewSheltered = false; return; }
            AstraInteractable interactable = shelter.GetComponent<AstraInteractable>();
            float safeRadius = interactable != null ? interactable.interactionRange + 1f : 7f;
            stormCrewSheltered = Vector3.Distance(player.transform.position, shelter.position) <= safeRadius;
        }

        private void CompleteObjective(AstraObjectiveId id)
        {
            foreach (MissionObjective objective in objectives)
                if (objective.id == id) objective.complete = true;
            if (id == AstraObjectiveId.ProtectedAreaReadiness)
                foreach (MissionObjective objective in objectives)
                    if (objective.id == id) objective.title = "Protected-area readiness checks passed";
            if (id == AstraObjectiveId.PostDrillInspection)
                foreach (MissionObjective objective in objectives)
                    if (objective.id == id) objective.title = "Post-drill inspection report submitted";
            VisualHintManager.Instance?.RefreshMissionTarget(this);
        }

        public void CollectRoverSample(AstraRoverDrive drive)
        {
            if (drive == null || sampleCollected || !roverLaunchPaid) return;
            if (CurrentMission != AstraMissionId.ShadowScout)
            {
                LatestMessage = "Virtual rover sample collection is only available during Shadow Scout.";
                hud?.ShowMessage(LatestMessage);
                return;
            }
            sampleCollected = true;
            sampleAnalysisPending = true;
            CompleteObjective(AstraObjectiveId.EuropaSiteInspected);
            hud?.ShowActivityChoice("EUROPA SAMPLE CLASSIFICATION", "The rover collected exposed surface material. What can this sample establish? A surface sample cannot directly prove the composition of Europa's subsurface ocean.", "SURFACE WATER ICE\nClassify the visible material", "SUBSURFACE OCEAN\nClaim it came from below", ChooseSampleAnalysis);
            LatestMessage = "Virtual surface sample secured. Analyze and classify it at the mission console.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
        }

        private void ChooseSolarAlignment(int choice)
        {
            AstraInteractable array = FindFacility(FacilityType.SolarArray)?.GetComponent<AstraInteractable>();
            if (array == null) return;
            int powerBefore = Mathf.RoundToInt(power);
            solarOnline = true;
            array.isOnline = true;
            if (choice == 0)
            {
                resources.AddPower(10f);
                int powerAfter = Mathf.RoundToInt(power);
                LatestMessage = "Array repositioned to track the Sun. Battery: " + powerBefore + " → " + powerAfter + " game units (+" + (powerAfter - powerBefore) + ").";
            }
            else
            {
                resources.AddPower(5f);
                int powerAfter = Mathf.RoundToInt(power);
                LatestMessage = "Array repositioned to protect it. Battery: " + powerBefore + " → " + powerAfter + " game units (+" + (powerAfter - powerBefore) + "); this setting trades output for protection.";
            }
            array.PlayCompletionFeedback();
            hud?.HideStormPanel();
            CompleteObjective(AstraObjectiveId.Solar);
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            VisualHintManager.Instance?.RefreshMissionTarget(this);
            TryFinishMissionIfReady();
        }

        private void CompleteCommunicationsInteraction(AstraInteractable facility, int choice, bool stormReadiness)
        {
            if (facility == null) return;
            if (choice == 0 && !resources.TrySpendPower(2f))
            {
                hud?.ShowActivityChoice("RELAY POWER IS TOO LOW", "The primary relay needs 2 game power. Choose the backup beacon to preserve the remaining reserve.", "PRIMARY RELAY\n98% signal · uses 2 power", "BACKUP BEACON\n86% signal · saves power", next => CompleteCommunicationsInteraction(facility, next, stormReadiness));
                return;
            }

            communicationsOnline = true;
            facility.isOnline = true;
            facility.PlayCompletionFeedback();
            hud?.HideStormPanel();
            if (stormReadiness)
            {
                stormCommsChecked = true;
                LatestMessage = choice == 0
                    ? "Primary relay calibrated: 98% fictional signal quality; power −2. Mission Control confirms the link."
                    : "Backup beacon tested: 86% fictional signal quality; reserve power saved. Mission Control confirms the link.";
                CheckStormReadinessComplete();
            }
            else
            {
                LatestMessage = choice == 0
                    ? "Dish aligned to the lunar relay: 98% fictional signal quality; power −2. Mission Control is receiving telemetry."
                    : "Low-power beacon selected: 86% fictional signal quality and no power cost. Mission Control is receiving telemetry.";
                CompleteObjective(AstraObjectiveId.Communications);
            }
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void CompleteLifeSupportInteraction(AstraInteractable facility, int choice, bool stormReadiness)
        {
            if (facility == null) return;
            lifeSupportChecked = true;
            facility.isOnline = true;
            facility.PlayCompletionFeedback();
            hud?.HideStormPanel();
            string inspection = choice == 0
                ? "Water loop inspected: H₂O reserve " + Mathf.RoundToInt(water) + "% and recycler status recorded."
                : "Air loop inspected: O₂ reserve " + Mathf.RoundToInt(oxygen) + "% and habitat condition " + Mathf.RoundToInt(habitatCondition) + "% recorded.";
            if (stormReadiness)
            {
                stormLifeSupportChecked = true;
                LatestMessage = inspection + " Values are fictional game readings.";
                CheckStormReadinessComplete();
            }
            else
            {
                resources.AddOxygen(choice == 0 ? 2f : 5f);
                resources.AddWater(choice == 0 ? 5f : 2f);
                LatestMessage = inspection + " Diagnostic rebalanced a small amount of the selected reserve. NASA ECLSS is more complex; ASTRA meters are simplified game values.";
                CompleteObjective(AstraObjectiveId.LifeSupport);
            }
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void ChooseWaterCycle(int choice)
        {
            float powerCost = choice == 0 ? 4f : 2f;
            if (resources.Power < powerCost)
            {
                hud?.ShowActivityChoice("RECYCLER NEEDS MORE POWER", "Bring the solar array online or choose the lower-cost cycle.", "FULL RECLAMATION\n+10 water · −4 power", "ECO CYCLE\n+6 water · −2 power", ChooseWaterCycle);
                return;
            }
            resources.TrySpendPower(powerCost);
            float recovered = choice == 0 ? 10f : 6f;
            resources.AddWater(recovered);
            waterRepairCount++;
            hud?.HideStormPanel();
            LatestMessage = (choice == 0 ? "Full reclamation cycle" : "Eco cycle") + " finished: water +" + recovered + ", power −" + powerCost + ". Recovered water is checked before use; ASTRA values are fictional.";
            CompleteObjective(AstraObjectiveId.WaterRestored);
            FindFacility(FacilityType.WaterRecycler)?.GetComponent<AstraInteractable>()?.PlayCompletionFeedback();
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void ChooseOxygenPlan(int choice)
        {
            float powerCost = choice == 0 ? 4f : 2f;
            float waterCost = choice == 0 ? 4f : 2f;
            if (resources.Power < powerCost || resources.Water < waterCost)
            {
                hud?.ShowActivityChoice("GENERATOR INPUT TOO LOW", "The selected plan needs " + powerCost + " power and " + waterCost + " water. Restore reserves or choose the lower-cost plan.", "BOOST OUTPUT\n+10 O₂ · −4 water · −4 power", "ECONOMY MODE\n+6 O₂ · −2 water · −2 power", ChooseOxygenPlan);
                return;
            }
            resources.TrySpendPower(powerCost);
            resources.TrySpendWater(waterCost);
            float produced = choice == 0 ? 10f : 6f;
            resources.AddOxygen(produced);
            oxygenRepairCount++;
            hud?.HideStormPanel();
            LatestMessage = (choice == 0 ? "Boost output" : "Economy mode") + " complete: O₂ +" + produced + ", water −" + waterCost + ", power −" + powerCost + ". NASA uses electrolysis; ASTRA values are fictional.";
            CompleteObjective(AstraObjectiveId.OxygenRestored);
            FindFacility(FacilityType.OxygenProcessor)?.GetComponent<AstraInteractable>()?.PlayCompletionFeedback();
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void CompleteShelterPreparation(AstraInteractable facility, int choice, bool stormReadiness)
        {
            if (facility == null) return;
            if (stormReadiness && choice == 0 && !resources.TrySpendPower(2f))
            {
                hud?.ShowActivityChoice("POWER RESERVE IS LOW", "The pressure-door test needs 2 game power. Clear and mark the shelter access route instead.", "TEST PRESSURE DOOR\nspend 2 power", "CLEAR SHELTER ACCESS\nsave power", next => CompleteShelterPreparation(facility, next, true));
                return;
            }
            shelterPrepared = true;
            facility.isOnline = true;
            facility.PlayCompletionFeedback();
            hud?.HideStormPanel();
            if (stormReadiness)
            {
                stormShelterChecked = true;
                LatestMessage = choice == 0
                    ? "Pressure door tested and protected area prepared; power −2. This is a simulated readiness check, not a real pressure test."
                    : "Shelter access route cleared and protected area prepared without spending power."
                ;
                CheckStormReadinessComplete();
            }
            else
            {
                CompleteObjective(AstraObjectiveId.Shelter);
                LatestMessage = "Protected area prepared. Regolith can help shield a shelter, but ASTRA does not calculate real dose or shielding performance.";
            }
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void ChooseGreenhouseCrop(int choice)
        {
            selectedCrop = choice;
            cropYield = choice == 0 ? 18f : 24f;
            foodCycleDuration = choice == 0 ? 20f : 30f;
            int waterCost = choice == 0 ? 8 : 12;
            if (resources.Power < 8f || resources.Water < waterCost)
            {
                selectedCrop = -1;
                hud?.HideStormPanel();
                LatestMessage = "That crop plan needs " + waterCost + " game water and 8 game power. Restore reserves before planting.";
                hud?.ShowMessage(LatestMessage);
                return;
            }
            resources.TrySpendPower(8f);
            resources.TrySpendWater(waterCost);
            foodCycleStarted = true;
            foodCycleElapsed = 0f;
            greenhouseDisplay?.BeginGrowth();
            greenhouseDisplay?.SetGrowth(0f);
            hud?.HideStormPanel();
            LatestMessage = (choice == 0 ? "Leafy greens" : "Dwarf beans") + " planted. Water −" + waterCost + ", power −8. Return after the " + Mathf.RoundToInt(foodCycleDuration) + "-second shortened game cycle.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
        }

        private void ChooseEuropaRoute(int choice)
        {
            roverRouteChosen = true;
            roverDrive = FindFacility(FacilityType.Rover)?.GetComponent<AstraRoverDrive>();
            if (roverDrive == null && FindFacility(FacilityType.Rover) != null)
                roverDrive = FindFacility(FacilityType.Rover).gameObject.AddComponent<AstraRoverDrive>();
            roverDrive?.SetEuropaSurveyRoute(choice);
            hud?.HideStormPanel();
            if (!roverLaunchPaid && !TrySpendPower(6f))
            {
                roverRouteChosen = false;
                LatestMessage = "The rover route is planned, but deployment needs 6 game power. Restore reserves and plan again.";
                hud?.ShowMessage(LatestMessage);
                return;
            }
            roverLaunchPaid = true;
            CompleteObjective(AstraObjectiveId.EuropaRoutePlanned);
            roverDrive?.BeginDrive();
            LatestMessage = "Route selected. Drive the rover to the beacon, inspect the exposed surface sample, then classify it.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
        }

        private void ChooseSampleAnalysis(int choice)
        {
            if (!sampleAnalysisPending) return;
            bool correct = choice == 0;
            if (!correct)
            {
                LatestMessage = CurrentMission == AstraMissionId.ShadowScout
                    ? "Evidence check: an exposed surface sample can be classified as surface ice, but it cannot establish the subsurface ocean's composition. Try again."
                    : "Evidence check: this is loose surface regolith, not a collected liquid-water sample. Try again.";
                hud?.ShowActivityChoice(CurrentMission == AstraMissionId.ShadowScout ? "EUROPA SAMPLE CLASSIFICATION" : "LUNAR SAMPLE CLASSIFICATION",
                    LatestMessage, CurrentMission == AstraMissionId.ShadowScout ? "SURFACE WATER ICE" : "LOOSE REGOLITH",
                    CurrentMission == AstraMissionId.ShadowScout ? "SUBSURFACE OCEAN" : "LIQUID WATER", ChooseSampleAnalysis);
                return;
            }
            sampleAnalysisPending = false;
            hud?.HideStormPanel();
            CompleteObjective(AstraObjectiveId.RoverSample);
            if (CurrentMission == AstraMissionId.ShadowScout)
                LatestMessage = "Sample classified as exposed surface water ice. A surface sample cannot confirm the subsurface ocean's composition.";
            else LatestMessage = "Sample classified as loose lunar regolith. The rover telemetry and virtual sample are logged for the mission.";
            hud?.ShowMessage(LatestMessage);
            hud?.Refresh(this);
            TryFinishMissionIfReady();
        }

        private void TryFinishMissionIfReady()
        {
            if (missionFinished || objectives.Count == 0) return;
            foreach (MissionObjective objective in objectives)
                if (!objective.complete) return;
            FinishMission();
        }

        private void FailMission()
        {
            if (missionFinished) return;
            failure = true;
            missionFinished = true;
            missionPaused = true;
            hud?.ShowReport(CalculateScore(), false, BuildLearningSummary(), MissionDefinition, MissionBestScore, false);
            hud?.SetCursorForMission(false);
        }

        private void FinishMission()
        {
            missionFinished = true;
            missionPaused = true;
            int score = CalculateScore();
            if (!failure) AstraCampaignProgress.RecordSuccess(CurrentMission, score);
            if (CanLaunchNextDestination) AstraMissionWorldSetup.SetSpacecraftRepaired(false);
            bool nextUnlocked = !failure && (int)CurrentMission < AstraMissionCatalog.Count - 1;
            hud?.ShowReport(score, !failure, BuildLearningSummary(), MissionDefinition, MissionBestScore, nextUnlocked);
            hud?.SetCursorForMission(false);
        }

        public void RestartMission() => BeginMission(CurrentMission);

        public int CalculateScore()
        {
            int objectiveScore = 0;
            foreach (MissionObjective objective in objectives) if (objective.complete) objectiveScore += 120;
            float resources = (oxygen + water + food + power) / 4f;
            int conditionScore = Mathf.RoundToInt(habitatCondition * 1.6f + resources * 1.2f);
            int decisionScore = safeDecision ? 80 : eventResolved ? 20 : 0;
            return Mathf.Clamp(objectiveScore + conditionScore + decisionScore, 0, 1000);
        }

        private string BuildLearningSummary()
        {
            string result = MissionDefinition.Lesson;
            if (CurrentMission == AstraMissionId.StormWatch && eventResolved)
                result += safeDecision
                    ? " You sheltered the crew and traded power and food for a lower fictional exposure index."
                    : " You kept outside work running but accepted a higher fictional exposure index.";
            if (failure) result = "The crew ran short of a critical reserve. Review how system choices affected the habitat. " + result;
            return result;
        }

        [Serializable] private class DonkiSepEvent
        {
            public string activityID;
            public string eventTime;
            public string link;
            public string note;
            public string notes;
            public string description;
            public string submissionTime;
        }
        [Serializable] private class DonkiSepList { public DonkiSepEvent[] items; }

        private IEnumerator LoadDonkiContext()
        {
            DateTime end = DateTime.UtcNow.Date;
            DateTime start = end.AddDays(-30);
            string startText = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string endText = end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string url = "https://ccmc.gsfc.nasa.gov/DONKI-API/get/SEP?startDate=" + startText + "&endDate=" + endText;
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetDonkiFallback("NASA DONKI is offline or unavailable.");
                    yield break;
                }
                try
                {
                    string json = request.downloadHandler.text;
                    DonkiSepList parsed = JsonUtility.FromJson<DonkiSepList>("{\"items\":" + json + "}");
                    DonkiSepEvent[] events = parsed != null ? parsed.items : null;
                    int count = events != null ? events.Length : 0;
                    if (count == 0)
                    {
                        SetDonkiFallback("NASA DONKI returned no SEP records for " + startText + " to " + endText + ".");
                    }
                    else
                    {
                        DonkiSepEvent latestEvent = events[count - 1];
                        DateTime latestTime = DateTime.MinValue;
                        foreach (DonkiSepEvent item in events)
                        {
                            if (item == null || !DateTime.TryParse(item.eventTime, System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime parsedTime)) continue;
                            if (parsedTime > latestTime) { latestTime = parsedTime; latestEvent = item; }
                        }
                        string sourceDate = !string.IsNullOrEmpty(latestEvent.eventTime) ? latestEvent.eventTime : latestEvent.submissionTime;
                        DonkiEventDate = DateTime.TryParse(sourceDate, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime displayTime)
                            ? displayTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
                            : (string.IsNullOrEmpty(sourceDate) ? "Date not supplied" : sourceDate);
                        DonkiEventType = "Solar Energetic Particle event (SEP)";
                        string eventDescription = !string.IsNullOrWhiteSpace(latestEvent.note) ? latestEvent.note :
                            !string.IsNullOrWhiteSpace(latestEvent.notes) ? latestEvent.notes : latestEvent.description;
                        DonkiEventDescription = !string.IsNullOrWhiteSpace(eventDescription)
                            ? eventDescription.Trim()
                            : "DONKI record " + latestEvent.activityID + " catalogues a solar energetic particle event. Review the linked record for associated observations. This is not a Mars surface dose or crew-health forecast.";
                        DonkiEventUrl = !string.IsNullOrWhiteSpace(latestEvent.link) ? latestEvent.link : "https://ccmc.gsfc.nasa.gov/DONKI/events/";
                        DonkiSummary = "Latest NASA DONKI SEP event: " + DonkiEventDate + " · " + latestEvent.activityID + ". Event records provide context, not Mars surface dose or crew-health forecasts.";
                        DonkiDataReady = true;
                        hud?.RefreshDonkiBriefing(DonkiEventDate, DonkiEventType, DonkiEventDescription, DonkiEventUrl, true);
                    }
                }
                catch (Exception)
                {
                    SetDonkiFallback("NASA DONKI returned data ASTRA could not read.");
                }
            }
        }

        private void SetDonkiFallback(string reason)
        {
            DonkiEventDate = "Offline training record";
            DonkiEventType = "Simulated solar-particle event (not a live DONKI event)";
            DonkiEventDescription = reason + " Review this fictional practice event: pause outdoor work, shelter the crew, monitor the drill, and confirm essential systems.";
            DonkiEventUrl = "https://ccmc.gsfc.nasa.gov/DONKI/";
            DonkiSummary = reason + " ASTRA is displaying its clearly labeled fictional training record; no live event is substituted.";
            DonkiDataReady = true;
            hud?.RefreshDonkiBriefing(DonkiEventDate, DonkiEventType, DonkiEventDescription, DonkiEventUrl, true);
        }

        public void SetPausedFromHud(bool paused)
        {
            missionPaused = paused;
            hud?.SetPauseVisible(paused);
            hud?.SetCursorForMission(!paused);
        }

        public string GetOutpostStatus()
        {
            if (!string.IsNullOrEmpty(resources.GetWarning())) return "ATTENTION";
            if (!communicationsOnline || !solarOnline) return "INITIALIZING";
            return "NOMINAL";
        }
    }
}
