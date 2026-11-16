using UnityEngine;
using System.Collections.Generic;

namespace Astra
{
    /// <summary>Backfills mission facilities missing from an older saved bootstrap scene.</summary>
    public static class AstraMissionWorldSetup
    {
        private static readonly Dictionary<string, Material> RuntimeMaterials = new Dictionary<string, Material>();
        private static Texture2D sharedRegolithTexture;
        private static AstraDestinationId activeDestination = AstraDestinationId.Moon;
        /// <summary>Replaces missing/error shaders on scene renderers with URP Lit at runtime.</summary>
        public static int RepairInvalidRenderMaterials()
        {
            Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (fallback == null) fallback = Shader.Find("Standard");
            if (fallback == null) return 0;

            int repairedCount = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    bool invalidShader = original == null || original.shader == null || !original.shader.isSupported ||
                                         original.shader.name.Contains("Error");
                    Color originalColor = Color.white;
                    bool hasOriginalColor = false;
                    if (original != null)
                    {
                        if (original.HasProperty("_BaseColor")) { originalColor = original.GetColor("_BaseColor"); hasOriginalColor = true; }
                        else if (original.HasProperty("_Color")) { originalColor = original.GetColor("_Color"); hasOriginalColor = true; }
                    }
                    // Unity renders unsupported shaders bright pink. Some imported assets keep
                    // a valid shader reference even though its shader variant is unavailable.
                    bool isErrorPink = hasOriginalColor && originalColor.r > 0.75f &&
                                       originalColor.g < 0.35f && originalColor.b > 0.75f;
                    if (!invalidShader && !isErrorPink) continue;

                    bool isPanel = renderer.name.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   renderer.name.IndexOf("solar", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    Color safeColor = isPanel ? new Color(0.055f, 0.18f, 0.30f) : new Color(0.58f, 0.66f, 0.70f);

                    Material repaired = new Material(fallback)
                    {
                        name = original != null ? original.name + " (URP fallback)" : "Recovered scene material"
                    };
                    if (original != null && !isErrorPink)
                    {
                        if (original.HasProperty("_BaseColor")) repaired.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                        else if (original.HasProperty("_Color")) repaired.SetColor("_BaseColor", original.GetColor("_Color"));
                        if (original.HasProperty("_Metallic")) repaired.SetFloat("_Metallic", original.GetFloat("_Metallic"));
                        if (original.HasProperty("_Smoothness")) repaired.SetFloat("_Smoothness", original.GetFloat("_Smoothness"));
                    }
                    else repaired.SetColor("_BaseColor", safeColor);
                    materials[i] = repaired;
                    changed = true;
                    repairedCount++;
                }
                if (changed) renderer.materials = materials;
            }

            if (repairedCount > 0)
                Debug.LogWarning("ASTRA repaired " + repairedCount + " missing, unsupported, or error-pink scene material(s) using URP Lit.");
            return repairedCount;
        }

        /// <summary>
        /// Swaps the old blockout renderers on every interactive facility for NASA's
        /// real GLB hardware models. Root colliders stay in place so gameplay keeps working.
        /// </summary>
        public static int ApplyNASAHardwareModels(AstraRuntimeModelLoader loader)
        {
            if (loader == null) return 0;
            int replaced = 0;
            foreach (AstraInteractable facility in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (ApplyNASAHardwareModel(facility, loader)) replaced++;
            return replaced;
        }

        /// <summary>Uses the NASA Apollo 11 LRO landing-site height mesh as visible lunar ground.</summary>
        public static void ApplyNASALunarTerrain(AstraRuntimeModelLoader loader)
        {
            // Keep the existing playable terrain and all mission interaction anchors.
            // The LRO STL is a sparse elevation survey, not a finished surface material;
            // drawing it over the mission terrain created visible holes and hid landmarks.
        }

        private static bool ApplyNASAHardwareModel(AstraInteractable facility, AstraRuntimeModelLoader loader)
        {
            if (facility == null || facility.transform.Find("ASTRA NASA hardware") != null) return false;

            string primaryAsset;
            string secondaryAsset = null;
            float targetHeight;
            switch (facility.facilityType)
            {
                case FacilityType.Habitat:
                case FacilityType.RadiationShelter:
                    primaryAsset = "HabitatDemonstrationUnit-Part1";
                    secondaryAsset = "HabitatDemonstrationUnit-Part2";
                    targetHeight = 4.4f;
                    break;
                case FacilityType.Communications:
                    primaryAsset = "NASADSN70mDish";
                    // The source is a 70 m ground antenna. Keep it landmark sized
                    // beside the crew scale habitat instead of letting the antenna
                    // dominate the entire playable outpost.
                    targetHeight = 2.8f;
                    break;
                case FacilityType.SolarArray:
                    // An orbiter is not a surface solar array. Keep the authored
                    // articulated panel assembly and its interaction target visible.
                    return false;
                case FacilityType.Rover:
                    primaryAsset = activeDestination == AstraDestinationId.Mars ? "PerseveranceRover" : "SpaceExplorationVehicle";
                    targetHeight = 2.7f;
                    break;
                case FacilityType.Spacecraft:
                    primaryAsset = "ApolloLunarModule";
                    targetHeight = 5.2f;
                    break;
                case FacilityType.LifeSupport:
                case FacilityType.OxygenProcessor:
                case FacilityType.WaterRecycler:
                case FacilityType.Greenhouse:
                case FacilityType.ResearchLab:
                    // The ISS is not a lunar surface facility. Preserve the authored
                    // life-support/recycler/greenhouse equipment so players can see
                    // and approach the correct mission-specific object.
                    return false;
                default:
                    return false;
            }

            GameObject modelGroup = new GameObject("ASTRA NASA hardware");
            modelGroup.transform.SetParent(facility.transform, false);
            modelGroup.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // Leave the authored facility visible until the imported model is
            // confirmed instantiated. If the GLB is absent or fails to decode,
            // players still see and can interact with the actual mission equipment.
            loader.LoadInto(primaryAsset, modelGroup.transform, targetHeight, primaryInstance =>
            {
                if (primaryInstance == null) return;
                if (!string.IsNullOrEmpty(secondaryAsset))
                    loader.LoadInto(secondaryAsset, modelGroup.transform, targetHeight * 0.72f, secondaryInstance =>
                    {
                        if (secondaryInstance == null) return;
                        HideBlockoutFacility(facility, modelGroup.transform);
                    });
                else HideBlockoutFacility(facility, modelGroup.transform);
                Debug.Log("ASTRA replaced " + facility.facilityName + " blockout with imported NASA 3D model " + primaryAsset + ".glb");
            });
            return true;
        }

        private static void HideBlockoutFacility(AstraInteractable facility, Transform importedModelRoot)
        {
            foreach (Renderer renderer in facility.GetComponentsInChildren<Renderer>(true))
                if (renderer.transform != facility.transform && !renderer.transform.IsChildOf(importedModelRoot)) renderer.enabled = false;
            foreach (Collider collider in facility.GetComponentsInChildren<Collider>(true))
                if (collider.transform != facility.transform && !collider.transform.IsChildOf(importedModelRoot)) collider.enabled = false;
        }

        public static AstraInteractable EnsureSpaceWeatherStation()
        {
            foreach (AstraInteractable facility in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (facility.facilityType == FacilityType.SpaceWeatherStation) return facility;

            GameObject outpost = GameObject.Find("SHACKLETON OUTPOST");
            GameObject root = new GameObject("Mars Dust-Storm Weather Console");
            if (outpost != null)
            {
                root.transform.SetParent(outpost.transform, false);
                root.transform.localPosition = new Vector3(8f, 0f, 22f);
            }
            else root.transform.position = new Vector3(8f, 0f, 22f);
            AstraInteractable interactable = root.AddComponent<AstraInteractable>();
            interactable.facilityType = FacilityType.SpaceWeatherStation;
            interactable.facilityName = "Mars Dust-Storm Weather Console";
            interactable.description = "Review NASA DONKI space-weather context, read the separate fictional Mars dust-storm drill briefing, then submit the post-drill inspection report.";
            interactable.interactionRange = 5f;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.05f, 0f);
            collider.size = new Vector3(1.8f, 2.2f, 1.4f);
            AddPart(PrimitiveType.Cube, "Weather console pedestal", root.transform, new Vector3(0f, 0.55f, 0f),
                new Vector3(1.65f, 1.1f, 1.15f), new Color(0.20f, 0.29f, 0.36f), Color.black);
            AddPart(PrimitiveType.Cube, "Weather monitoring screen", root.transform, new Vector3(0f, 1.48f, 0.48f),
                new Vector3(1.48f, 0.82f, 0.10f), new Color(0.04f, 0.20f, 0.29f), new Color(0.02f, 0.32f, 0.52f));
            AddPart(PrimitiveType.Cube, "Weather monitor graph", root.transform, new Vector3(0f, 1.45f, 0.54f),
                new Vector3(0.96f, 0.08f, 0.025f), new Color(0.18f, 0.91f, 0.86f), new Color(0.08f, 0.68f, 0.62f));
            AddPart(PrimitiveType.Cube, "Weather monitor status light", root.transform, new Vector3(0.54f, 0.96f, 0.59f),
                new Vector3(0.14f, 0.14f, 0.06f), new Color(0.17f, 0.86f, 0.58f), new Color(0.05f, 0.62f, 0.30f));
            return interactable;
        }

        /// <summary>
        /// Repairs mission equipment in scenes saved before all challenge assets were added.
        /// A mission objective must always have a visible, collidable object to approach.
        /// Existing authored or NASA imported models are preserved.
        /// </summary>
        public static void EnsureMissionFacilities(AstraMissionId mission)
        {
            switch (mission)
            {
                case AstraMissionId.FirstLight:
                    EnsureVisibleFacility(FacilityType.Communications);
                    EnsureVisibleFacility(FacilityType.SolarArray);
                    EnsureVisibleFacility(FacilityType.LifeSupport);
                    break;
                case AstraMissionId.AirAndWater:
                    EnsureVisibleFacility(FacilityType.WaterRecycler);
                    EnsureVisibleFacility(FacilityType.OxygenProcessor);
                    break;
                case AstraMissionId.ShadowScout:
                    EnsureVisibleFacility(FacilityType.Rover);
                    break;
                case AstraMissionId.StormWatch:
                    EnsureSpaceWeatherStation();
                    EnsureRadiationShelter();
                    EnsureVisibleFacility(FacilityType.SpaceWeatherStation);
                    EnsureVisibleFacility(FacilityType.RadiationShelter);
                    EnsureVisibleFacility(FacilityType.Communications);
                    EnsureVisibleFacility(FacilityType.SolarArray);
                    EnsureVisibleFacility(FacilityType.LifeSupport);
                    break;
                case AstraMissionId.FoodForTheLongStay:
                    EnsureVisibleFacility(FacilityType.Greenhouse);
                    break;
            }
        }

        private static AstraInteractable EnsureVisibleFacility(FacilityType type)
        {
            AstraInteractable facility = null;
            foreach (AstraInteractable candidate in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
            {
                if (candidate.facilityType != type || candidate.facilityName.IndexOf("Practice", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                facility = candidate;
                break;
            }

            if (facility == null)
            {
                GameObject outpost = GameObject.Find("SHACKLETON OUTPOST");
                GameObject root = new GameObject(FacilityObjectName(type));
                if (outpost != null) root.transform.SetParent(outpost.transform, false);
                root.transform.localPosition = FacilityPosition(type);
                facility = root.AddComponent<AstraInteractable>();
                facility.facilityType = type;
                facility.facilityName = FacilityDisplayName(type);
                facility.description = FacilityDescription(type);
                facility.interactionRange = type == FacilityType.Rover ? 5.5f : 4.5f;
            }

            bool hasVisibleModel = false;
            foreach (Renderer renderer in facility.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    hasVisibleModel = true;
                    break;
                }

            if (!hasVisibleModel && facility.transform.Find("ASTRA visible equipment fallback") == null)
                BuildVisibleFacilityFallback(facility);

            Collider rootCollider = facility.GetComponent<Collider>();
            if (rootCollider == null)
            {
                BoxCollider bounds = facility.gameObject.AddComponent<BoxCollider>();
                bounds.center = new Vector3(0f, 1.1f, 0f);
                bounds.size = type == FacilityType.Rover ? new Vector3(3.8f, 2.2f, 3.2f) : new Vector3(3.2f, 2.7f, 3.0f);
            }
            return facility;
        }

        private static void BuildVisibleFacilityFallback(AstraInteractable facility)
        {
            GameObject model = new GameObject("ASTRA visible equipment fallback");
            model.transform.SetParent(facility.transform, false);
            Color steel = new Color(0.66f, 0.73f, 0.77f);
            Color dark = new Color(0.055f, 0.12f, 0.19f);
            Color cyan = new Color(0.08f, 0.78f, 0.91f);
            switch (facility.facilityType)
            {
                case FacilityType.Communications:
                    AddPart(PrimitiveType.Cube, "Relay electronics cabinet", model.transform, new Vector3(0f, 0.82f, 0f), new Vector3(1.35f, 1.55f, 0.85f), steel, Color.black);
                    AddPart(PrimitiveType.Cylinder, "Relay mast", model.transform, new Vector3(0f, 2.05f, 0f), new Vector3(0.11f, 1.05f, 0.11f), steel, Color.black);
                    AddPart(PrimitiveType.Cylinder, "Communications dish", model.transform, new Vector3(0f, 2.95f, 0.15f), new Vector3(1.12f, 0.13f, 1.12f), new Color(0.78f, 0.83f, 0.86f), Color.black).transform.localRotation = Quaternion.Euler(27f, 0f, 0f);
                    AddPart(PrimitiveType.Cube, "Relay status display", model.transform, new Vector3(0f, 1.25f, 0.48f), new Vector3(0.62f, 0.34f, 0.035f), cyan, cyan * 0.35f);
                    break;
                case FacilityType.SolarArray:
                    AddPart(PrimitiveType.Cylinder, "Array pedestal", model.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.33f, 0.58f, 0.33f), steel, Color.black);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        AddPart(PrimitiveType.Cube, "Photovoltaic wing", model.transform, new Vector3(side * 1.38f, 1.24f, 0f), new Vector3(2.55f, 0.11f, 1.75f), new Color(0.045f, 0.19f, 0.38f), new Color(0.015f, 0.06f, 0.15f));
                        AddPart(PrimitiveType.Cube, "Solar cell busbar", model.transform, new Vector3(side * 1.38f, 1.31f, 0f), new Vector3(0.045f, 0.025f, 1.65f), cyan, cyan * 0.24f);
                    }
                    AddPart(PrimitiveType.Cylinder, "Array rotation joint", model.transform, new Vector3(0f, 1.13f, 0f), new Vector3(0.18f, 0.15f, 0.18f), new Color(0.92f, 0.62f, 0.18f), Color.black);
                    break;
                case FacilityType.LifeSupport:
                    AddPart(PrimitiveType.Cylinder, "Oxygen loop vessel", model.transform, new Vector3(-0.82f, 1.12f, 0f), new Vector3(0.58f, 1.12f, 0.58f), steel, Color.black);
                    AddPart(PrimitiveType.Cylinder, "Water loop vessel", model.transform, new Vector3(0.82f, 1.12f, 0f), new Vector3(0.58f, 1.12f, 0.58f), new Color(0.31f, 0.60f, 0.73f), Color.black);
                    AddPart(PrimitiveType.Cube, "Life support control console", model.transform, new Vector3(0f, 1.16f, 0.65f), new Vector3(1.8f, 1.25f, 0.22f), dark, Color.black);
                    AddPart(PrimitiveType.Cube, "Life support status screen", model.transform, new Vector3(0f, 1.55f, 0.78f), new Vector3(1.05f, 0.34f, 0.04f), cyan, cyan * 0.32f);
                    AddPart(PrimitiveType.Cylinder, "Life support transfer manifold", model.transform, new Vector3(0f, 2.5f, 0f), new Vector3(1.15f, 0.10f, 0.10f), steel, Color.black).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case FacilityType.WaterRecycler:
                case FacilityType.OxygenProcessor:
                    bool recycler = facility.facilityType == FacilityType.WaterRecycler;
                    AddPart(PrimitiveType.Cube, recycler ? "Recycler housing" : "Oxygen generator housing", model.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.8f, 1.95f, 1.55f), new Color(0.47f, 0.58f, 0.63f), Color.black);
                    AddPart(PrimitiveType.Cylinder, recycler ? "Recovered water tank" : "Oxygen storage tank", model.transform, new Vector3(1.15f, 0.9f, 0f), new Vector3(0.42f, 0.8f, 0.42f), recycler ? new Color(0.31f, 0.65f, 0.78f) : steel, Color.black);
                    AddPart(PrimitiveType.Cube, recycler ? "Recycler cycle display" : "Oxygen generator display", model.transform, new Vector3(0f, 1.4f, 0.8f), new Vector3(0.95f, 0.38f, 0.04f), cyan, cyan * 0.32f);
                    AddPart(PrimitiveType.Cylinder, recycler ? "Recycler return pipe" : "Electrolysis transfer pipe", model.transform, new Vector3(-0.92f, 0.42f, 0f), new Vector3(0.13f, 0.12f, 1.2f), steel, Color.black);
                    break;
                case FacilityType.Greenhouse:
                    AddPart(PrimitiveType.Cube, "Greenhouse grow module", model.transform, new Vector3(0f, 1.08f, 0f), new Vector3(3.5f, 2.05f, 2.7f), new Color(0.20f, 0.39f, 0.43f), Color.black);
                    AddPart(PrimitiveType.Cube, "Greenhouse viewing window", model.transform, new Vector3(0f, 1.2f, 1.37f), new Vector3(2.65f, 1.35f, 0.06f), new Color(0.04f, 0.36f, 0.43f), cyan * 0.18f);
                    for (int i = -1; i <= 1; i++)
                    {
                        AddPart(PrimitiveType.Cube, "Hydroponic grow tray", model.transform, new Vector3(i * 0.85f, 0.34f, -0.25f), new Vector3(0.66f, 0.22f, 0.8f), new Color(0.31f, 0.23f, 0.16f), Color.black);
                        AddPart(PrimitiveType.Sphere, "Greenhouse seedling", model.transform, new Vector3(i * 0.85f, 0.82f, -0.1f), new Vector3(0.4f, 0.68f, 0.4f), new Color(0.22f, 0.65f, 0.34f), Color.black);
                    }
                    break;
                case FacilityType.RadiationShelter:
                    AddPart(PrimitiveType.Cube, "Storm protected-area habitat", model.transform, new Vector3(0f, 1.05f, 0f), new Vector3(3.8f, 2.1f, 3.6f), new Color(0.32f, 0.40f, 0.53f), Color.black);
                    AddPart(PrimitiveType.Cube, "Protected-area pressure door", model.transform, new Vector3(0f, 0.82f, 1.84f), new Vector3(1.15f, 1.58f, 0.09f), cyan, cyan * 0.22f);
                    AddPart(PrimitiveType.Cube, "Storm shelter status panel", model.transform, new Vector3(1.14f, 1.45f, 1.88f), new Vector3(0.34f, 0.52f, 0.09f), dark, Color.black);
                    AddPart(PrimitiveType.Sphere, "Protected-area ready beacon", model.transform, new Vector3(0f, 2.48f, 0f), Vector3.one * 0.22f, new Color(0.75f, 0.55f, 1f), new Color(0.24f, 0.08f, 0.45f));
                    break;
                case FacilityType.SpaceWeatherStation:
                    AddPart(PrimitiveType.Cube, "Mars weather console", model.transform, new Vector3(0f, 0.78f, 0f), new Vector3(1.65f, 1.35f, 0.96f), dark, Color.black);
                    AddPart(PrimitiveType.Cube, "Space-weather event display", model.transform, new Vector3(0f, 1.64f, 0.35f), new Vector3(1.45f, 0.72f, 0.08f), new Color(0.04f, 0.30f, 0.40f), cyan * 0.20f);
                    AddPart(PrimitiveType.Cube, "Event graph trace", model.transform, new Vector3(0f, 1.57f, 0.405f), new Vector3(0.90f, 0.055f, 0.025f), cyan, cyan * 0.32f);
                    AddPart(PrimitiveType.Sphere, "Weather console status lamp", model.transform, new Vector3(0.58f, 0.99f, 0.52f), Vector3.one * 0.12f, new Color(0.18f, 0.92f, 0.61f), new Color(0.06f, 0.48f, 0.26f));
                    break;
                case FacilityType.Rover:
                    AddPart(PrimitiveType.Cube, "Surface rover chassis", model.transform, new Vector3(0f, 0.95f, 0f), new Vector3(2.65f, 0.65f, 1.85f), new Color(0.75f, 0.70f, 0.54f), Color.black);
                    AddPart(PrimitiveType.Cube, "Rover instrument deck", model.transform, new Vector3(0f, 1.44f, 0f), new Vector3(1.45f, 0.34f, 1.18f), steel, Color.black);
                    for (int side = -1; side <= 1; side += 2)
                    for (int axle = -1; axle <= 1; axle++)
                        AddPart(PrimitiveType.Cylinder, "Rover wheel", model.transform, new Vector3(side * 1.1f, 0.47f, axle * 0.58f), new Vector3(0.42f, 0.17f, 0.42f), dark, Color.black).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    AddPart(PrimitiveType.Cylinder, "Rover sample arm", model.transform, new Vector3(0.65f, 1.92f, 0.1f), new Vector3(0.09f, 0.46f, 0.09f), steel, Color.black);
                    break;
            }
        }

        private static string FacilityObjectName(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return "Communications";
                case FacilityType.SolarArray: return "Solar Array";
                case FacilityType.LifeSupport: return "Life Support";
                case FacilityType.WaterRecycler: return "Water Recycler";
                case FacilityType.OxygenProcessor: return "Oxygen Processor";
                case FacilityType.Greenhouse: return "Greenhouse";
                case FacilityType.Rover: return "Lunar Rover";
                case FacilityType.RadiationShelter: return "Radiation Shelter";
                case FacilityType.SpaceWeatherStation: return "Mars Dust-Storm Weather Console";
                default: return type.ToString();
            }
        }

        private static string FacilityDisplayName(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return "Communications station";
                case FacilityType.SolarArray: return "Solar power station";
                case FacilityType.LifeSupport: return "Life-support systems";
                case FacilityType.WaterRecycler: return "Water recycler";
                case FacilityType.OxygenProcessor: return "Oxygen generator";
                case FacilityType.Greenhouse: return "Food production greenhouse";
                case FacilityType.Rover: return "Surface exploration rover";
                case FacilityType.RadiationShelter: return "Protected-area shelter";
                case FacilityType.SpaceWeatherStation: return "Mars weather console";
                default: return FacilityObjectName(type);
            }
        }

        private static string FacilityDescription(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return "Align and commission the mission communications link.";
                case FacilityType.SolarArray: return "Reposition the photovoltaic array and observe the simulated power output.";
                case FacilityType.LifeSupport: return "Inspect the habitat air and water life-support readings.";
                case FacilityType.WaterRecycler: return "Run a water recovery cycle and compare the simulated reserve readings.";
                case FacilityType.OxygenProcessor: return "Use recovered water in a simplified oxygen-generation activity.";
                case FacilityType.Greenhouse: return "Choose crop and growing conditions, then compare the harvests.";
                case FacilityType.Rover: return "Drive to the marked virtual sample site and record the surface evidence.";
                case FacilityType.RadiationShelter: return "Prepare the protected area for the simulated Mars dust-storm drill.";
                case FacilityType.SpaceWeatherStation: return "Review the space-weather context and Mars drill briefing.";
                default: return "Mission equipment and interaction point.";
            }
        }

        private static Vector3 FacilityPosition(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return new Vector3(-10f, 0f, 5f);
                case FacilityType.SolarArray: return new Vector3(10f, 0f, 5f);
                case FacilityType.LifeSupport: return new Vector3(-10f, 0f, -8f);
                case FacilityType.WaterRecycler: return new Vector3(16f, 0f, -12f);
                case FacilityType.OxygenProcessor: return new Vector3(-16f, 0f, -12f);
                case FacilityType.Greenhouse: return new Vector3(0f, 0f, -12f);
                case FacilityType.Rover: return new Vector3(10f, 0f, -9f);
                case FacilityType.RadiationShelter: return new Vector3(-8f, 0f, 19f);
                case FacilityType.SpaceWeatherStation: return new Vector3(8f, 0f, 22f);
                default: return Vector3.zero;
            }
        }

        public static AstraInteractable EnsureSpacecraft()
        {
            foreach (AstraInteractable facility in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (facility.facilityType == FacilityType.Spacecraft)
                {
                    facility.transform.position = new Vector3(8f, 0f, 14f);
                    return facility;
                }

            GameObject root = new GameObject("ASTRA interplanetary spacecraft");
            // Keep the repairable spacecraft in the player's forward field of view so it
            // reads as a real mission landmark while remaining a usable interaction target.
            root.transform.position = new Vector3(8f, 0f, 14f);
            AstraInteractable interactable = root.AddComponent<AstraInteractable>();
            interactable.facilityType = FacilityType.Spacecraft;
            interactable.facilityName = "Interplanetary spacecraft";
            interactable.description = "Repair the spacecraft before launching to the next unlocked destination.";
            interactable.interactionRange = 8f;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.25f, 0f);
            collider.size = new Vector3(9f, 3f, 5f);
            AddPart(PrimitiveType.Capsule, "Spacecraft pressure hull", root.transform, new Vector3(0f, 1.45f, 0f),
                new Vector3(2.3f, 0.95f, 5.3f), new Color(0.68f, 0.75f, 0.80f), Color.black);
            AddPart(PrimitiveType.Sphere, "Spacecraft cockpit", root.transform, new Vector3(0f, 1.5f, 2.45f),
                new Vector3(1.45f, 0.78f, 0.45f), new Color(0.08f, 0.48f, 0.68f), new Color(0.02f, 0.18f, 0.28f));
            AddPart(PrimitiveType.Cube, "Spacecraft damaged service panel", root.transform, new Vector3(1.18f, 1.42f, 0f),
                new Vector3(0.14f, 0.75f, 1.2f), new Color(0.76f, 0.20f, 0.15f), new Color(0.42f, 0.04f, 0.02f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddPart(PrimitiveType.Cylinder, "Spacecraft landing strut", root.transform, new Vector3(side * 1.5f, 0.46f, -0.35f),
                    new Vector3(0.12f, 0.46f, 0.12f), new Color(0.45f, 0.52f, 0.56f), Color.black);
                AddPart(PrimitiveType.Cube, "Spacecraft solar wing", root.transform, new Vector3(side * 3.3f, 1.45f, -0.35f),
                    new Vector3(2.1f, 0.08f, 1.45f), new Color(0.08f, 0.24f, 0.49f), new Color(0.01f, 0.05f, 0.14f));
            }
            return interactable;
        }

        public static void ApplyDestination(AstraDestinationId destination)
        {
            UpgradeGeneratedOutpostVisuals();
            activeDestination = destination;
            GameObject terrain = FindSceneObject("Regolith terrain mesh");
            Renderer terrainRenderer = terrain != null ? terrain.GetComponent<Renderer>() : null;
            GameObject lunarTerrainMesh = FindSceneObject("NASA Apollo11LandingSite");
            bool useLunarTerrain = destination == AstraDestinationId.Moon && lunarTerrainMesh != null;
            if (lunarTerrainMesh != null) lunarTerrainMesh.SetActive(useLunarTerrain);
            if (terrainRenderer != null)
            {
                terrainRenderer.enabled = !useLunarTerrain;
                Color color = destination == AstraDestinationId.Mars ? new Color(0.58f, 0.32f, 0.24f) :
                    destination == AstraDestinationId.Europa ? new Color(0.34f, 0.47f, 0.55f) : new Color(0.40f, 0.36f, 0.49f);
                Material surface = terrainRenderer.material;
                surface.color = color;
                surface.mainTexture = GetRegolithTexture();
                surface.mainTextureScale = Vector2.one;
            }
            Camera missionCamera = Camera.main;
            if (missionCamera != null)
            {
                missionCamera.backgroundColor = destination == AstraDestinationId.Mars ? new Color(0.055f, 0.018f, 0.012f) :
                    destination == AstraDestinationId.Europa ? new Color(0.008f, 0.025f, 0.065f) : new Color(0.004f, 0.006f, 0.016f);
                missionCamera.clearFlags = CameraClearFlags.SolidColor;
                missionCamera.farClipPlane = Mathf.Max(missionCamera.farClipPlane, 1200f);
            }
            EnsureLunarStarfield();
            RenderSettings.ambientLight = destination == AstraDestinationId.Mars ? new Color(0.34f, 0.19f, 0.14f) :
                destination == AstraDestinationId.Europa ? new Color(0.20f, 0.29f, 0.40f) : new Color(0.25f, 0.23f, 0.34f);
            GameObject foregroundRidge = FindSceneObject("South-pole highlands foreground");
            GameObject farRidge = FindSceneObject("South-pole highlands far ridge");
            if (foregroundRidge != null)
                foregroundRidge.GetComponent<Renderer>().material.color = destination == AstraDestinationId.Mars
                    ? new Color(0.48f, 0.28f, 0.23f) : destination == AstraDestinationId.Europa
                        ? new Color(0.34f, 0.43f, 0.55f) : new Color(0.38f, 0.32f, 0.48f);
            if (farRidge != null)
                farRidge.GetComponent<Renderer>().material.color = destination == AstraDestinationId.Mars
                    ? new Color(0.25f, 0.12f, 0.12f) : destination == AstraDestinationId.Europa
                        ? new Color(0.18f, 0.27f, 0.40f) : new Color(0.23f, 0.20f, 0.34f);
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    light.color = destination == AstraDestinationId.Mars ? new Color(1f, 0.78f, 0.66f) :
                        destination == AstraDestinationId.Europa ? new Color(0.69f, 0.82f, 1f) : Color.white;
            GameObject earth = FindSceneObject("Earth in the lunar sky");
            if (earth != null) earth.SetActive(destination == AstraDestinationId.Moon);
            SetDestinationSkyBody(destination);
        }

        /// <summary>Gives the airless destination skies a stable, distant field of stars.</summary>
        private static void EnsureLunarStarfield()
        {
            if (FindSceneObject("ASTRA deep-space starfield") != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            GameObject field = new GameObject("ASTRA deep-space starfield");
            ParticleSystem stars = field.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = stars.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 2400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = 0.85f;
            main.startColor = Color.white;
            ParticleSystem.EmissionModule emission = stars.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = stars.shape;
            shape.enabled = false;
            ParticleSystemRenderer particleRenderer = field.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.material = new Material(shader) { name = "ASTRA distant stars" };
            if (particleRenderer.material.HasProperty("_BaseColor")) particleRenderer.material.SetColor("_BaseColor", Color.white);
            if (particleRenderer.material.HasProperty("_Color")) particleRenderer.material.SetColor("_Color", Color.white);

            UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(61204);
            for (int i = 0; i < 2100; i++)
            {
                ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams();
                particle.position = UnityEngine.Random.onUnitSphere * 420f;
                particle.startSize = UnityEngine.Random.Range(0.28f, 0.9f);
                float tint = UnityEngine.Random.Range(0.78f, 1f);
                particle.startColor = new Color(tint * 0.88f, tint * 0.94f, tint, 1f);
                particle.startLifetime = 100000f;
                particle.velocity = Vector3.zero;
                stars.Emit(particle, 1);
            }
            UnityEngine.Random.state = savedRandom;
            stars.Play();
        }

        public static void SetSpacecraftRepaired(bool repaired)
        {
            AstraInteractable ship = EnsureSpacecraft();
            if (ship == null) return;
            Color color = repaired ? new Color(0.12f, 0.78f, 0.38f) : new Color(0.95f, 0.22f, 0.12f);
            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    if (!material.HasProperty("_EmissionColor")) continue;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * (repaired ? 0.18f : 0.08f));
                }
            }
        }

        private static void SetDestinationSkyBody(AstraDestinationId destination)
        {
            GameObject body = FindSceneObject("ASTRA destination sky body");
            if (body == null)
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "ASTRA destination sky body";
                body.transform.position = new Vector3(34f, 27f, 118f);
                body.transform.localScale = Vector3.one * 10f;
                Collider collider = body.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
            }
            Renderer renderer = body.GetComponent<Renderer>();
            renderer.material.color = destination == AstraDestinationId.Mars ? new Color(0.72f, 0.25f, 0.13f) : new Color(0.62f, 0.78f, 0.92f);
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", destination == AstraDestinationId.Mars ? new Color(0.25f, 0.05f, 0.01f) : new Color(0.14f, 0.23f, 0.36f));
            }
            body.SetActive(destination != AstraDestinationId.Moon);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
                if (item.name == objectName && item.scene.IsValid()) return item;
            return null;
        }

        public static AstraInteractable EnsureRadiationShelter()
        {
            foreach (AstraInteractable facility in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
                if (facility.facilityType == FacilityType.RadiationShelter) return facility;

            GameObject outpost = GameObject.Find("SHACKLETON OUTPOST");
            Transform parent = outpost != null ? outpost.transform : null;
            GameObject root = new GameObject("Radiation Shelter");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
                root.transform.localPosition = new Vector3(-8f, 0f, 19f);
            }
            AstraInteractable interactable = root.AddComponent<AstraInteractable>();
            interactable.facilityType = FacilityType.RadiationShelter;
            interactable.facilityName = "Radiation shelter";
            interactable.description = "Prepare a crew shelter for the simulated solar-particle alert. This is a fictional learning activity, not radiation-dose guidance.";
            interactable.interactionRange = 6f;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.1f, 0f);
            collider.size = new Vector3(6f, 2.8f, 6f);

            AddPart(PrimitiveType.Cube, "Shelter module", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(4.8f, 2.2f, 4.4f), new Color(0.33f, 0.40f, 0.53f), new Color(0.15f, 0.18f, 0.28f));
            AddPart(PrimitiveType.Cube, "Shelter access door", root.transform, new Vector3(0f, 0.78f, 2.23f), new Vector3(1.35f, 1.55f, 0.10f), new Color(0.15f, 0.72f, 0.83f), new Color(0.05f, 0.45f, 0.60f));
            AddPart(PrimitiveType.Cube, "Left regolith shielding berm", root.transform, new Vector3(-2.9f, 0.42f, 0f), new Vector3(0.65f, 0.84f, 5.2f), new Color(0.47f, 0.43f, 0.37f), Color.black);
            AddPart(PrimitiveType.Cube, "Right regolith shielding berm", root.transform, new Vector3(2.9f, 0.42f, 0f), new Vector3(0.65f, 0.84f, 5.2f), new Color(0.47f, 0.43f, 0.37f), Color.black);
            AddPart(PrimitiveType.Cube, "Shelter status panel", root.transform, new Vector3(1.15f, 1.55f, 2.25f), new Vector3(0.36f, 0.52f, 0.10f), new Color(0.2f, 0.82f, 0.93f), new Color(0.03f, 0.65f, 0.9f));
            AddBeacon(root.transform);
            return interactable;
        }

        public static AstraGreenhouseDisplay EnsureGreenhouseDisplay()
        {
            AstraInteractable[] facilities = Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None);
            foreach (AstraInteractable facility in facilities)
            {
                if (facility.facilityType != FacilityType.Greenhouse) continue;
                AstraGreenhouseDisplay display = facility.GetComponent<AstraGreenhouseDisplay>();
                if (display == null) display = facility.gameObject.AddComponent<AstraGreenhouseDisplay>();
                return display;
            }
            return null;
        }

        public static AstraRoverDrive EnsureRoverDrive()
        {
            foreach (AstraInteractable facility in Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None))
            {
                if (facility.facilityType != FacilityType.Rover) continue;
                AstraRoverDrive drive = facility.GetComponent<AstraRoverDrive>();
                if (drive == null) drive = facility.gameObject.AddComponent<AstraRoverDrive>();
                return drive;
            }
            return null;
        }

        private static GameObject AddPart(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Color color, Color emission)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            Material material = GetRuntimeMaterial(color, emission);
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            Collider childCollider = part.GetComponent<Collider>();
            if (childCollider != null) Object.Destroy(childCollider);
            return part;
        }

        private static Material GetRuntimeMaterial(Color color, Color emission)
        {
            string key = color.ToString() + emission.ToString();
            if (RuntimeMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = "ASTRA Runtime Facility",
                color = color,
                enableInstancing = true
            };
            if (material.HasProperty("_EmissionColor") && emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            RuntimeMaterials[key] = material;
            return material;
        }

        private static Texture2D GetRegolithTexture()
        {
            if (sharedRegolithTexture != null) return sharedRegolithTexture;
            const int size = 256;
            sharedRegolithTexture = new Texture2D(size, size, TextureFormat.RGB24, false, true)
            {
                name = "ASTRA procedural regolith",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 8
            };
            System.Random random = new System.Random(60115);
            Vector2[] craters = new Vector2[22];
            float[] radii = new float[craters.Length];
            for (int i = 0; i < craters.Length; i++)
            {
                craters[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                radii[i] = Mathf.Lerp(0.012f, 0.045f, (float)random.NextDouble());
            }

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float relief = (Mathf.PerlinNoise(u * 19f + 3.2f, v * 19f + 8.7f) - 0.5f) * 0.18f +
                               (Mathf.PerlinNoise(u * 83f + 13.1f, v * 83f + 1.9f) - 0.5f) * 0.10f +
                               (Mathf.PerlinNoise(u * 191f + 9.4f, v * 191f + 17.6f) - 0.5f) * 0.045f;
                Vector2 uv = new Vector2(u, v);
                for (int i = 0; i < craters.Length; i++)
                {
                    float d = Vector2.Distance(uv, craters[i]) / radii[i];
                    if (d > 1.18f) continue;
                    float bowl = Mathf.Max(0f, 1f - d * d);
                    float rim = Mathf.Exp(-Mathf.Pow((d - 0.9f) / 0.13f, 2f));
                    relief += -0.18f * bowl * bowl + 0.08f * rim;
                }
                float grain = Mathf.Clamp(0.76f + relief, 0.48f, 1f);
                pixels[y * size + x] = new Color(grain, grain, grain, 1f);
            }
            sharedRegolithTexture.SetPixels(pixels);
            sharedRegolithTexture.Apply(false, true);
            return sharedRegolithTexture;
        }

        private static void UpgradeGeneratedOutpostVisuals()
        {
            GameObject astronaut = GameObject.Find("ASTRONAUT - Junior Commander");
            Transform suit = astronaut != null ? astronaut.transform.Find("Suit model") : null;
            if (suit != null)
            {
                Transform helmet = suit.Find("Helmet visor");
                if (helmet != null)
                {
                    helmet.name = "Helmet shell";
                    helmet.localPosition = new Vector3(0f, 1.84f, 0.02f);
                    helmet.localScale = new Vector3(0.56f, 0.53f, 0.55f);
                    SetVisualMaterial(helmet.gameObject, new Color(0.91f, 0.93f, 0.94f), Color.black);
                }
                AddVisual(suit, PrimitiveType.Cylinder, "Helmet neck seal", new Vector3(0f, 1.51f, 0f),
                    new Vector3(0.30f, 0.13f, 0.30f), new Color(0.26f, 0.32f, 0.37f), Color.black);
                AddVisual(suit, PrimitiveType.Sphere, "Helmet face shield", new Vector3(0f, 1.84f, 0.245f),
                    new Vector3(0.43f, 0.34f, 0.20f), new Color(0.035f, 0.10f, 0.16f), new Color(0.015f, 0.10f, 0.18f));
                AddVisual(suit, PrimitiveType.Cube, "Backpack status panel", new Vector3(0f, 1.10f, -0.558f),
                    new Vector3(0.25f, 0.40f, 0.025f), new Color(0.05f, 0.67f, 0.82f), new Color(0.02f, 0.30f, 0.42f));
                AddVisual(suit, PrimitiveType.Cube, "Chest oxygen indicator", new Vector3(-0.18f, 1.37f, 0.20f),
                    new Vector3(0.08f, 0.19f, 0.035f), new Color(0.94f, 0.63f, 0.18f), new Color(0.48f, 0.20f, 0.02f));
                AddSuitExtremity(suit, "Left arm pivot", "Left EVA glove", PrimitiveType.Sphere, new Vector3(0f, -0.45f, 0.02f),
                    new Vector3(0.24f, 0.20f, 0.24f), new Color(0.85f, 0.88f, 0.89f));
                AddSuitExtremity(suit, "Right arm pivot", "Right EVA glove", PrimitiveType.Sphere, new Vector3(0f, -0.45f, 0.02f),
                    new Vector3(0.24f, 0.20f, 0.24f), new Color(0.85f, 0.88f, 0.89f));
                AddSuitExtremity(suit, "Left leg pivot", "Left boot sole", PrimitiveType.Cube, new Vector3(0f, -0.57f, 0.10f),
                    new Vector3(0.34f, 0.045f, 0.43f), new Color(0.89f, 0.55f, 0.15f));
                AddSuitExtremity(suit, "Right leg pivot", "Right boot sole", PrimitiveType.Cube, new Vector3(0f, -0.57f, 0.10f),
                    new Vector3(0.34f, 0.045f, 0.43f), new Color(0.89f, 0.55f, 0.15f));
            }

            AstraInteractable habitat = FindFacility(FacilityType.Habitat);
            if (habitat != null)
            {
                for (int window = -1; window <= 1; window++)
                {
                    AddVisual(habitat.transform, PrimitiveType.Cube, "Habitat porthole frame " + window,
                        new Vector3(window * 0.92f, 1.83f, 1.58f), new Vector3(0.62f, 0.62f, 0.13f),
                        new Color(0.22f, 0.31f, 0.38f), Color.black);
                    AddVisual(habitat.transform, PrimitiveType.Cube, "Habitat blue porthole " + window,
                        new Vector3(window * 0.92f, 1.83f, 1.66f), new Vector3(0.46f, 0.46f, 0.045f),
                        new Color(0.08f, 0.49f, 0.67f), new Color(0.02f, 0.20f, 0.33f));
                }
            }
        }

        private static void AddSuitExtremity(Transform suit, string pivotName, string visualName, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Color color)
        {
            Transform pivot = suit.Find(pivotName);
            if (pivot != null) AddVisual(pivot, type, visualName, localPosition, localScale, color, Color.black);
        }

        private static GameObject AddVisual(Transform parent, PrimitiveType type, string name, Vector3 localPosition,
            Vector3 localScale, Color color, Color emission)
        {
            Transform existing = parent.Find(name);
            GameObject item;
            if (existing != null)
            {
                item = existing.gameObject;
            }
            else
            {
                item = GameObject.CreatePrimitive(type);
                item.name = name;
                item.transform.SetParent(parent, false);
                Collider collider = item.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
            }
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            SetVisualMaterial(item, color, emission);
            return item;
        }

        private static void SetVisualMaterial(GameObject item, Color color, Color emission)
        {
            Renderer renderer = item != null ? item.GetComponent<Renderer>() : null;
            Material material = GetRuntimeMaterial(color, emission);
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private static AstraInteractable FindFacility(FacilityType type)
        {
            AstraInteractable[] facilities = Object.FindObjectsByType<AstraInteractable>(FindObjectsSortMode.None);
            foreach (AstraInteractable facility in facilities)
                if (facility.facilityType == type) return facility;
            return null;
        }

        private static void AddBeacon(Transform parent)
        {
            GameObject beacon = new GameObject("Radiation shelter mission beacon");
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = new Vector3(0f, 3.45f, 0f);
            AddPart(PrimitiveType.Cylinder, "Beacon stem", beacon.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(0.07f, 0.28f, 0.07f), new Color(0.77f, 0.62f, 0.98f), Color.black);
            AddPart(PrimitiveType.Sphere, "Beacon lamp", beacon.transform, Vector3.zero,
                Vector3.one * 0.34f, new Color(0.81f, 0.62f, 1f), new Color(0.50f, 0.20f, 1f));
            Light pointLight = beacon.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(0.73f, 0.49f, 1f);
            pointLight.range = 5f;
            pointLight.intensity = 1.4f;
        }
    }
}
