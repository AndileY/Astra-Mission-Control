using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Astra
{
    /// <summary>Turns a completed maintenance task into a visible outpost change.</summary>
    public sealed class AstraFacilityFeedback : MonoBehaviour
    {
        private static readonly List<Light> OutpostLights = new List<Light>();
        private static bool lightingBuilt;
        private Renderer[] renderers;
        private Light localLight;
        private readonly Dictionary<Material, Color> originalEmission = new Dictionary<Material, Color>();
        private readonly Dictionary<Material, bool> originalEmissionKeyword = new Dictionary<Material, bool>();
        private readonly Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();
        private FacilityType? activeMotion;
        private Transform motionRoot;
        private Transform dishElevation;
        private Transform orbiterHinge;
        private Quaternion motionRootRest;
        private Quaternion dishRest;
        private Quaternion hingeRest;
        private float motionStartedAt;
        private bool motionRootRestCaptured;
        private bool dishRestCaptured;
        private bool hingeRestCaptured;

        public void Play(AstraInteractable facility)
        {
            if (facility == null) return;
            activeMotion = facility.facilityType;
            motionStartedAt = Time.time;
            motionRoot = FindNamedChild(transform, "ASTRA NASA hardware");
            if (motionRoot != null) motionRootRest = motionRoot.localRotation;
            dishElevation = FindNamedChild(transform, "f_70m_rev_");
            if (dishElevation != null) dishRest = dishElevation.localRotation;
            orbiterHinge = FindNamedChild(transform, "Pivot-Layer_1");
            if (orbiterHinge != null) hingeRest = orbiterHinge.localRotation;
            motionRootRestCaptured = motionRoot != null;
            dishRestCaptured = dishElevation != null;
            hingeRestCaptured = orbiterHinge != null;
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            Color accent = AccentFor(facility.facilityType);
            foreach (Renderer item in renderers)
            {
                if (item == null) continue;
                Material material = item.material;
                if (material.HasProperty("_EmissionColor"))
                {
                    if (!originalEmission.ContainsKey(material))
                    {
                        originalEmission[material] = material.GetColor("_EmissionColor");
                        originalEmissionKeyword[material] = material.IsKeywordEnabled("_EMISSION");
                    }
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", accent * 0.35f);
                }
            }
            if (localLight == null)
            {
                GameObject lightObject = new GameObject("Activated facility light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.1f, 0.2f);
                localLight = lightObject.AddComponent<Light>();
                localLight.type = LightType.Point;
                localLight.range = 7f;
                localLight.color = accent;
                localLight.intensity = 0f;
            }
            StopAllCoroutines();
            StartCoroutine(FadeOn(localLight));
            if (facility.facilityType == FacilityType.SolarArray) ActivateOutpostLighting();
            AnimateFacility(facility.facilityType);
        }

        public void ResetVisuals()
        {
            StopAllCoroutines();
            activeMotion = null;
            motionRoot = null;
            dishElevation = null;
            orbiterHinge = null;
            if (localLight != null) localLight.intensity = 0f;
            if (renderers != null)
                foreach (Renderer item in renderers)
                {
                    if (item == null) continue;
                    Material material = item.material;
                    if (material.HasProperty("_EmissionColor") && originalEmission.TryGetValue(material, out Color emission))
                        material.SetColor("_EmissionColor", emission);
                    if (originalEmissionKeyword.TryGetValue(material, out bool keywordEnabled) && !keywordEnabled)
                        material.DisableKeyword("_EMISSION");
                }
            foreach (KeyValuePair<Transform, Quaternion> pair in originalRotations)
                if (pair.Key != null) pair.Key.localRotation = pair.Value;
            Transform fan = FindNamedChild(transform, "Activated circulation fan");
            if (fan != null) fan.gameObject.SetActive(false);
            foreach (Light light in OutpostLights)
                if (light != null) light.intensity = 0f;
        }

        private void Update()
        {
            if (!activeMotion.HasValue) return;
            float time = Time.time - motionStartedAt;
            // Track a slow radio target using the actual NASA DSN mount and dish mesh.
            if (activeMotion.Value == FacilityType.Communications)
            {
                if (motionRoot == null) motionRoot = FindNamedChild(transform, "ASTRA NASA hardware");
                if (dishElevation == null) dishElevation = FindNamedChild(transform, "f_70m_rev_");
                if (motionRoot != null && !motionRootRestCaptured) { motionRootRest = motionRoot.localRotation; motionRootRestCaptured = true; }
                if (dishElevation != null && !dishRestCaptured) { dishRest = dishElevation.localRotation; dishRestCaptured = true; }
                if (motionRoot != null)
                    motionRoot.localRotation = motionRootRest * Quaternion.Euler(0f, Mathf.Sin(time * 0.20f) * 24f, 0f);
                if (dishElevation != null)
                    dishElevation.localRotation = dishRest * Quaternion.Euler(-8f + Mathf.Sin(time * 0.13f) * 5f, 0f, 0f);
            }
            // The LRO model's own named panel pivots and main assembly articulate
            // gently to simulate solar tracking above the outpost.
            else if (activeMotion.Value == FacilityType.SolarArray)
            {
                if (motionRoot == null) motionRoot = FindNamedChild(transform, "ASTRA NASA hardware");
                if (orbiterHinge == null) orbiterHinge = FindNamedChild(transform, "Pivot-Layer_1");
                if (motionRoot != null && !motionRootRestCaptured) { motionRootRest = motionRoot.localRotation; motionRootRestCaptured = true; }
                if (orbiterHinge != null && !hingeRestCaptured) { hingeRest = orbiterHinge.localRotation; hingeRestCaptured = true; }
                if (motionRoot != null)
                    motionRoot.localRotation = motionRootRest * Quaternion.Euler(0f, time * 2.5f, 0f);
                if (orbiterHinge != null)
                    orbiterHinge.localRotation = hingeRest * Quaternion.Euler(Mathf.Sin(time * 0.35f) * 6f, 0f, 0f);
            }
        }

        private IEnumerator FadeOn(Light light)
        {
            if (light == null) yield break;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                light.intensity = Mathf.Lerp(0f, 1.7f, t / 1.5f);
                yield return null;
            }
            light.intensity = 1.7f;
        }

        private static void ActivateOutpostLighting()
        {
            if (!lightingBuilt)
            {
                lightingBuilt = true;
                Vector3[] positions =
                {
                    new Vector3(0f, 4.2f, 10f), new Vector3(-10f, 3.3f, 5f),
                    new Vector3(10f, 3.5f, 5f), new Vector3(-10f, 3.2f, -8f),
                    new Vector3(0f, 3.2f, -12f), new Vector3(10f, 3.2f, -9f),
                    new Vector3(-8f, 3.6f, 19f), new Vector3(0f, 3.4f, 21f)
                };
                for (int i = 0; i < positions.Length; i++)
                {
                    GameObject lamp = new GameObject("Outpost light " + (i + 1));
                    lamp.transform.position = positions[i];
                    Light light = lamp.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.48f);
                    light.range = 12f;
                    light.intensity = 0f;
                    OutpostLights.Add(light);
                }
            }
            foreach (Light light in OutpostLights)
                if (light != null) light.intensity = 2.2f;
        }

        private void AnimateFacility(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications:
                    // Persistent dish tracking runs in Update using NASA's actual
                    // mount and elevation transform nodes.
                    break;
                case FacilityType.SolarArray:
                    // LRO solar-panel pivots slowly track sunlight in Update.
                    break;
                case FacilityType.Rover:
                    Transform rover = FindNamedChild(transform, "ASTRA NASA hardware");
                    if (rover != null) StartCoroutine(DriveInPlace(rover));
                    break;
                case FacilityType.LifeSupport:
                    // Real ISS hardware remains still; the material pulse above marks
                    // the system check without spawning another primitive prop.
                    break;
                case FacilityType.Greenhouse:
                    foreach (Renderer item in renderers)
                    {
                        if (item == null || !item.name.Contains("Leaf")) continue;
                        Material material = item.material;
                        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(0.20f, 1f, 0.38f) * 0.75f);
                    }
                    break;
            }
        }

        private IEnumerator DriveInPlace(Transform rover)
        {
            if (rover == null) yield break;
            Vector3 start = rover.localPosition;
            Vector3 end = start + new Vector3(0f, 0f, 0.28f);
            float elapsed = 0f;
            while (elapsed < 0.65f && rover != null)
            {
                elapsed += Time.deltaTime;
                rover.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / 0.65f));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < 0.65f && rover != null)
            {
                elapsed += Time.deltaTime;
                rover.localPosition = Vector3.Lerp(end, start, Mathf.SmoothStep(0f, 1f, elapsed / 0.65f));
                yield return null;
            }
            if (rover != null) rover.localPosition = start;
        }

        private IEnumerator RotateTo(Transform target, Quaternion localTarget)
        {
            if (target == null) yield break;
            Quaternion start = target.localRotation;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                target.localRotation = Quaternion.Slerp(start, localTarget, t / 2f);
                yield return null;
            }
            target.localRotation = localTarget;
        }

        private static Transform FindNamedChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        private void RememberRotation(Transform target)
        {
            if (target != null && !originalRotations.ContainsKey(target)) originalRotations[target] = target.localRotation;
        }

        private static Transform[] FindNamedChildren(Transform parent, string childName)
        {
            List<Transform> found = new List<Transform>();
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) found.Add(child);
            return found.ToArray();
        }

        private static Color AccentFor(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Communications: return new Color(0.15f, 0.8f, 1f);
                case FacilityType.SolarArray: return new Color(1f, 0.78f, 0.4f);
                case FacilityType.Greenhouse: return new Color(0.35f, 1f, 0.48f);
                case FacilityType.Rover: return new Color(1f, 0.68f, 0.26f);
                case FacilityType.RadiationShelter: return new Color(0.75f, 0.52f, 1f);
                default: return new Color(0.18f, 0.88f, 0.82f);
            }
        }
    }
}
