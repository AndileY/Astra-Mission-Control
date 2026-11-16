using System.Collections.Generic;
using UnityEngine;

namespace Astra
{
    /// <summary>Shows crop growth and the harvested food as objects in the greenhouse scene.</summary>
    public sealed class AstraGreenhouseDisplay : MonoBehaviour
    {
        private readonly List<Transform> leaves = new List<Transform>();
        private readonly List<Vector3> leafScales = new List<Vector3>();
        private GameObject harvestCrate;
        private TextMesh statusLabel;
        private Light growLight;

        private void Awake()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.Contains("Leaf cluster")) continue;
                leaves.Add(child);
                leafScales.Add(child.localScale);
            }
            CreateHarvestDisplay();
            ResetVisuals();
        }

        public void ResetVisuals()
        {
            for (int i = 0; i < leaves.Count; i++) leaves[i].localScale = Vector3.zero;
            if (harvestCrate != null) harvestCrate.SetActive(false);
            if (statusLabel != null) statusLabel.gameObject.SetActive(false);
            if (growLight != null) growLight.intensity = 0f;
        }

        public void BeginGrowth()
        {
            if (harvestCrate != null) harvestCrate.SetActive(false);
            if (statusLabel != null)
            {
                statusLabel.text = "CROP GROWING";
                statusLabel.gameObject.SetActive(true);
            }
            if (growLight != null) growLight.intensity = 0.65f;
        }

        public void SetGrowth(float progress)
        {
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            for (int i = 0; i < leaves.Count; i++) leaves[i].localScale = leafScales[i] * eased;
            if (statusLabel != null)
            {
                statusLabel.text = "CROP GROWING  " + Mathf.RoundToInt(progress * 100f) + "%";
                statusLabel.gameObject.SetActive(true);
            }
        }

        public void ShowReady()
        {
            SetGrowth(1f);
            if (statusLabel != null) statusLabel.text = "HARVEST READY  •  PRESS E";
            if (growLight != null) growLight.intensity = 1.2f;
        }

        public void ShowHarvest(int harvestNumber)
        {
            for (int i = 0; i < leaves.Count; i++) leaves[i].localScale = Vector3.zero;
            if (harvestCrate != null) harvestCrate.SetActive(true);
            if (statusLabel != null)
            {
                statusLabel.text = "CROP HARVESTED  •  +18 FOOD  •  BATCH " + harvestNumber;
                statusLabel.gameObject.SetActive(true);
            }
            if (growLight != null) growLight.intensity = 0.35f;
        }

        private void CreateHarvestDisplay()
        {
            harvestCrate = new GameObject("Visible harvested crop basket");
            harvestCrate.transform.SetParent(transform, false);
            harvestCrate.transform.localPosition = new Vector3(0f, 0.22f, 2.15f);
            AddPiece(PrimitiveType.Cube, "Harvest basket", harvestCrate.transform, new Vector3(0f, 0f, 0f),
                new Vector3(1.05f, 0.44f, 0.72f), new Color(0.48f, 0.31f, 0.16f), false);
            Color[] produceColors = { new Color(0.33f, 0.79f, 0.31f), new Color(0.80f, 0.78f, 0.30f), new Color(0.45f, 0.72f, 0.27f) };
            for (int i = 0; i < produceColors.Length; i++)
            {
                float x = (i - 1) * 0.27f;
                AddPiece(PrimitiveType.Sphere, "Harvested produce", harvestCrate.transform,
                    new Vector3(x, 0.30f + (i % 2) * 0.06f, 0f), new Vector3(0.33f, 0.28f, 0.30f), produceColors[i], false);
            }
            harvestCrate.SetActive(false);

            GameObject labelObject = new GameObject("Greenhouse crop status");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 3.2f, 2.2f);
            statusLabel = labelObject.AddComponent<TextMesh>();
            statusLabel.anchor = TextAnchor.MiddleCenter;
            statusLabel.alignment = TextAlignment.Center;
            statusLabel.fontSize = 58;
            statusLabel.characterSize = 0.10f;
            statusLabel.color = new Color(0.68f, 1f, 0.64f);
            labelObject.SetActive(false);

            GameObject lightObject = new GameObject("Greenhouse grow light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.6f, 0.4f);
            growLight = lightObject.AddComponent<Light>();
            growLight.type = LightType.Point;
            growLight.color = new Color(0.56f, 1f, 0.46f);
            growLight.range = 6f;
            growLight.intensity = 0f;
        }

        private static GameObject AddPiece(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Color color, bool withCollider)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().material.color = color;
            Collider collider = piece.GetComponent<Collider>();
            if (!withCollider && collider != null) Object.Destroy(collider);
            return piece;
        }
    }
}
