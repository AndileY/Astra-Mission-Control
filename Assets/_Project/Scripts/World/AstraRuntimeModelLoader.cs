using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Rendering;

namespace Astra
{
    /// <summary>Loads NASA glTF binary models from StreamingAssets at runtime.</summary>
    public sealed class AstraRuntimeModelLoader : MonoBehaviour
    {
        public static AstraRuntimeModelLoader Instance { get; private set; }
        private readonly Dictionary<string, GltfImport> importedModels = new Dictionary<string, GltfImport>();
        private readonly HashSet<string> failedModels = new HashSet<string>();
        private readonly Dictionary<string, Mesh> importedStlMeshes = new Dictionary<string, Mesh>();
#if UNITY_EDITOR
        private bool lowMemoryNoticeShown;
#endif

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public async void LoadInto(string assetName, Transform parent, float targetHeight, Action<GameObject> onLoaded)
        {
            if (parent == null) return;
#if UNITY_EDITOR
            // Large NASA GLBs can exceed the memory budget while running inside the
            // Editor on 8 GB machines. Keep the authored blockout facilities visible;
            // standalone builds and higher-memory Editors can still load the models.
            if (SystemInfo.systemMemorySize <= 8192)
            {
                if (!lowMemoryNoticeShown)
                {
                    Debug.LogWarning("ASTRA is using its built-in scene models in the Editor because this device has 8 GB or less of memory.");
                    lowMemoryNoticeShown = true;
                }
                return;
            }
#endif
            // Prefer a packaged prefab when one is present. The source project keeps
            // NASA GLBs in StreamingAssets and loads them on demand to avoid importing
            // high-detail meshes into the Editor's asset database at startup.
            GameObject importedPrefab = Resources.Load<GameObject>("NASA/MissionHardware/Processed/" + assetName);
            if (importedPrefab == null)
                importedPrefab = Resources.Load<GameObject>("NASA/MissionHardware/" + assetName);
            if (importedPrefab == null && assetName == "Astronaut")
                importedPrefab = Resources.Load<GameObject>("NASA/Astronaut");
            if (importedPrefab != null)
            {
                GameObject prefabInstance = Instantiate(importedPrefab, parent, false);
                prefabInstance.name = "NASA " + assetName;
                NormalizeImportedModel(prefabInstance, parent, targetHeight, assetName, onLoaded);
                return;
            }
            GltfImport importer;
            if (!importedModels.TryGetValue(assetName, out importer))
            {
                if (failedModels.Contains(assetName)) return;
                // Keep glTFast's extension diagnostics in the player log. This makes
                // unsupported NASA asset encodings visible instead of silently leaving
                // the original interaction marker in the scene.
                importer = new GltfImport(logger: new GLTFast.Logging.ConsoleLogger());
                string path = Path.Combine(Application.streamingAssetsPath, "NASA", "MissionHardware", assetName + ".glb");
                bool loaded;
                try { loaded = await importer.LoadFile(path); }
                catch (Exception exception)
                {
                    failedModels.Add(assetName);
                    Debug.LogError("ASTRA could not import NASA model at runtime: " + path + " / " + exception.Message);
                    importer.Dispose();
                    return;
                }
                if (!loaded)
                {
                    failedModels.Add(assetName);
                    Debug.LogError("ASTRA could not import NASA model at runtime: " + path);
                    return;
                }
                importedModels[assetName] = importer;
            }

            if (parent == null) return;

            GameObject instance = new GameObject("NASA " + assetName);
            instance.transform.SetParent(parent, false);
            bool instantiated = await importer.InstantiateMainSceneAsync(instance.transform);
            if (!instantiated)
            {
                Destroy(instance);
                Debug.LogError("ASTRA could not instantiate NASA model: " + assetName);
                return;
            }

            NormalizeImportedModel(instance, parent, targetHeight, assetName, onLoaded);
        }

        private void NormalizeImportedModel(GameObject instance, Transform parent, float targetHeight, string assetName, Action<GameObject> onLoaded)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(instance);
                Debug.LogError("NASA model has no visible meshes: " + assetName);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) instance.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 parentPosition = parent.position;
            Vector3 position = instance.transform.position;
            position.x += parentPosition.x - bounds.center.x;
            position.y += parentPosition.y - bounds.min.y;
            position.z += parentPosition.z - bounds.center.z;
            instance.transform.position = position;
            StartEmbeddedModelAnimation(instance, assetName);
            onLoaded?.Invoke(instance);
        }

        private static void StartEmbeddedModelAnimation(GameObject instance, string assetName)
        {
            // Several NASA models are authored as rigid meshes but the Apollo lander
            // and ISS include real glTF transform clips. Start a suitable imported
            // clip instead of adding fake animated geometry around those models.
            Animation[] animations = instance.GetComponentsInChildren<Animation>(true);
            foreach (Animation animation in animations)
            {
                if (animation == null || animation.GetClipCount() == 0) continue;
                AnimationClip selected = null;
                foreach (AnimationState state in animation)
                {
                    if (state == null || state.clip == null) continue;
                    string clipName = state.clip.name;
                    if (assetName == "ApolloLunarModule" && clipName.IndexOf("lunarlande", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selected = state.clip;
                        break;
                    }
                    if (assetName == "NASAInternationalSpaceStation" && clipName.IndexOf("isscombine", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selected = state.clip;
                        break;
                    }
                }
                if (selected == null) continue;
                animation.clip = selected;
                animation.wrapMode = WrapMode.Loop;
                animation.Play(selected.name);
                Debug.Log("ASTRA playing NASA model animation " + assetName + ": " + selected.name);
            }
        }

        public async void LoadStlInto(string relativePath, Transform parent, float targetHeight, Action<GameObject> onLoaded)
        {
            if (parent == null) return;
            Mesh mesh;
            if (!importedStlMeshes.TryGetValue(relativePath, out mesh))
            {
                string path = Path.Combine(Application.streamingAssetsPath, "NASA", relativePath);
                StlData data;
                try { data = await Task.Run(() => ReadBinaryStl(path)); }
                catch (Exception exception)
                {
                    Debug.LogError("ASTRA could not read NASA STL " + path + ": " + exception.Message);
                    return;
                }
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path), indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(data.vertices);
                mesh.SetTriangles(data.triangles, 0, true);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                importedStlMeshes[relativePath] = mesh;
            }
            if (parent == null) return;

            GameObject instance = new GameObject("NASA " + Path.GetFileNameWithoutExtension(relativePath));
            instance.transform.SetParent(parent, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = new Color(0.67f, 0.66f, 0.64f) };
            if (mesh.bounds.size.y > 0.001f) instance.transform.localScale = Vector3.one * (targetHeight / mesh.bounds.size.y);
            Vector3 local = instance.transform.localPosition;
            local.x -= mesh.bounds.center.x * instance.transform.localScale.x;
            local.z -= mesh.bounds.center.z * instance.transform.localScale.z;
            local.y -= mesh.bounds.min.y * instance.transform.localScale.y;
            instance.transform.localPosition = local;
            onLoaded?.Invoke(instance);
        }

        private static StlData ReadBinaryStl(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 84) throw new InvalidDataException("STL header is incomplete");
            int triangleCount = BitConverter.ToInt32(bytes, 80);
            if (triangleCount < 1 || 84L + triangleCount * 50L > bytes.Length)
                throw new InvalidDataException("Only binary STL with a valid triangle count is supported");

            var vertices = new List<Vector3>(Mathf.Min(triangleCount, 800000));
            var triangles = new List<int>(Mathf.Min(triangleCount * 3, 2400000));
            var indices = new Dictionary<Vector3, int>(Mathf.Min(triangleCount, 800000));
            for (int face = 0; face < triangleCount; face++)
            {
                int offset = 84 + face * 50 + 12;
                int faceStart = triangles.Count;
                for (int vertex = 0; vertex < 3; vertex++)
                {
                    float x = BitConverter.ToSingle(bytes, offset + vertex * 12);
                    float y = BitConverter.ToSingle(bytes, offset + vertex * 12 + 4);
                    float z = BitConverter.ToSingle(bytes, offset + vertex * 12 + 8);
                    // NASA printable lunar terrain specifies relief on the STL Z axis;
                    // NASA's SLS print model uses the same Z-up convention.
                    Vector3 point = new Vector3(x, z, y);
                    int index;
                    if (!indices.TryGetValue(point, out index))
                    {
                        index = vertices.Count;
                        indices.Add(point, index);
                        vertices.Add(point);
                    }
                    triangles.Add(index);
                }
                // The Z-up to Y-up axis swap reverses handedness; restore outward face winding.
                int second = triangles[faceStart + 1];
                triangles[faceStart + 1] = triangles[faceStart + 2];
                triangles[faceStart + 2] = second;
            }
            return new StlData(vertices, triangles);
        }

        private sealed class StlData
        {
            public readonly List<Vector3> vertices;
            public readonly List<int> triangles;
            public StlData(List<Vector3> vertices, List<int> triangles) { this.vertices = vertices; this.triangles = triangles; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (GltfImport importer in importedModels.Values) importer.Dispose();
            importedModels.Clear();
            foreach (Mesh mesh in importedStlMeshes.Values) if (mesh != null) Destroy(mesh);
            importedStlMeshes.Clear();
        }
    }
}
