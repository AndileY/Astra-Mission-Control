using System.Collections.Generic;
using Astra;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class AstraAutoSceneBootstrap
{
    private const string ScenePath = "Assets/_Project/Scenes/ASTRA_Bootstrap.unity";
    private const string PlayRequestFile = ".astra-play-bootstrap-once";

    static AstraAutoSceneBootstrap()
    {
        EditorApplication.delayCall += EnsureSceneExists;
        EditorApplication.delayCall += OpenRequestedSceneAndPlay;
    }

    private static void EnsureSceneExists()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        if (!System.IO.File.Exists(System.IO.Path.Combine(projectRoot, ScenePath)))
            AstraSceneBuilder.BuildScene();
    }

    private static void OpenRequestedSceneAndPlay()
    {
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string requestPath = System.IO.Path.Combine(projectRoot, PlayRequestFile);
        if (!System.IO.File.Exists(requestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OpenRequestedSceneAndPlay;
            return;
        }

        System.IO.File.Delete(requestPath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += EnterPlayModeWhenReady;
    }

    private static void EnterPlayModeWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnterPlayModeWhenReady;
            return;
        }

        EditorApplication.isPlaying = true;
    }
}

public static class AstraSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/ASTRA_Bootstrap.unity";
    private const float TerrainHalfSize = 130f;
    private const float TerrainBaseY = -0.12f;
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static int materialAssetIndex;
    private static readonly Vector3[] CraterCenters =
    {
        new Vector3(-42f, 0f, 29f),
        new Vector3(44f, 0f, 48f),
        new Vector3(-63f, 0f, -42f),
        new Vector3(68f, 0f, -55f),
        new Vector3(11f, 0f, 82f),
        new Vector3(-91f, 0f, 72f)
    };

    [MenuItem("ASTRA/Build First Playable Scene")]
    public static void BuildScene()
    {
        Materials.Clear();
        materialAssetIndex = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureProject();
        BuildEnvironment();
        BuildOutpost();
        BuildAstronaut();
        BuildPracticeDeck();
        BuildGameSystems();
        EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("ASTRA: generated the first playable scene at " + ScenePath);
    }

    [MenuItem("ASTRA/Upgrade Challenge Facilities")]
    public static void UpgradeChallengeFacilities()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject outpost = GameObject.Find("SHACKLETON OUTPOST");
        if (outpost == null)
            throw new System.InvalidOperationException("Could not find SHACKLETON OUTPOST in the ASTRA scene.");

        if (outpost.transform.Find("Radiation Shelter") == null)
        {
            BuildRadiationShelter(outpost.transform, new Vector3(-8f, 0f, 19f));
            AddBeacon(outpost.transform, new Vector3(-8f, 0f, 19f), new Color(0.8f, 0.55f, 1f));
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("ASTRA: added the challenge radiation shelter to the existing scene.");
    }

    private static void ConfigureProject()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        RenderPipelineAsset urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (urp != null)
        {
            GraphicsSettings.defaultRenderPipeline = urp;
            QualitySettings.renderPipeline = urp;
        }
    }

    private static void BuildEnvironment()
    {
        GameObject world = new GameObject("LUNAR ENVIRONMENT");
        CreateLunarTerrain(world.transform);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 260f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.006f, 0.010f, 0.032f);
        cameraObject.AddComponent<AudioListener>();

        GameObject lightObject = new GameObject("Sun - directional light");
        Light sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.9f;
        sun.color = new Color(1f, 0.94f, 0.84f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.82f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
        lightObject.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.15f, 0.27f);
        RenderSettings.ambientIntensity = 0.7f;
        // The Moon has no atmosphere: preserve a black sky and sharp distant silhouettes.
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        RenderSettings.reflectionIntensity = 0.25f;

        CreateEarth(world.transform);
        CreateStars(world.transform);
        CreateRockField(world.transform);
        CreateDistantRidge(world.transform);
    }

    private static void CreateLunarTerrain(Transform parent)
    {
        const int resolution = 181;
        int vertexCount = resolution * resolution;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        float step = TerrainHalfSize * 2f / (resolution - 1);

        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            float worldX = -TerrainHalfSize + x * step;
            float worldZ = -TerrainHalfSize + z * step;
            int index = z * resolution + x;
            vertices[index] = new Vector3(worldX, LunarTerrainHeight(worldX, worldZ), worldZ);
            uvs[index] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
        }

        int triangleIndex = 0;
        for (int z = 0; z < resolution - 1; z++)
        for (int x = 0; x < resolution - 1; x++)
        {
            int a = z * resolution + x;
            int b = a + 1;
            int c = a + resolution;
            int d = c + 1;
            triangles[triangleIndex++] = a;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = b;
            triangles[triangleIndex++] = b;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = d;
        }

        Mesh terrainMesh = new Mesh { name = "Shackleton Regolith Terrain" };
        terrainMesh.vertices = vertices;
        terrainMesh.uv = uvs;
        terrainMesh.triangles = triangles;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
        terrainMesh = StoreGeneratedMesh(terrainMesh, "ASTRA_CrateredTerrain");

        GameObject terrain = new GameObject("Regolith terrain mesh", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        terrain.transform.SetParent(parent, false);
        terrain.GetComponent<MeshFilter>().sharedMesh = terrainMesh;
        terrain.GetComponent<MeshCollider>().sharedMesh = terrainMesh;
        SetMaterial(terrain, "Regolith", new Color(0.34f, 0.31f, 0.43f), 0.02f);
        Material regolith = terrain.GetComponent<Renderer>().sharedMaterial;
        Texture2D surfaceTexture = GetLunarRegolithTexture();
        if (regolith != null && surfaceTexture != null)
        {
            regolith.mainTexture = surfaceTexture;
            regolith.mainTextureScale = Vector2.one;
        }
    }

    private static Texture2D GetLunarRegolithTexture()
    {
        const string texturePath = "Assets/_Project/Art/Textures/ASTRA_Moon_Regolith.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (existing != null) return existing;

        const int resolution = 512;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false, true)
        {
            name = "ASTRA Moon Regolith",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 8
        };
        System.Random random = new System.Random(51208);
        const int craterCount = 26;
        Vector2[] craterCenters = new Vector2[craterCount];
        float[] craterRadii = new float[craterCount];
        for (int i = 0; i < craterCount; i++)
        {
            craterCenters[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            craterRadii[i] = Mathf.Lerp(0.009f, 0.030f, (float)random.NextDouble());
        }

        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float u = (float)x / resolution;
            float v = (float)y / resolution;
            float broad = Mathf.PerlinNoise(u * 8f + 2.7f, v * 8f + 5.4f) - 0.5f;
            float medium = Mathf.PerlinNoise(u * 34f + 17.2f, v * 34f + 8.1f) - 0.5f;
            float grain = Mathf.PerlinNoise(u * 155f + 4.3f, v * 155f + 13.7f) - 0.5f;
            float relief = broad * 0.19f + medium * 0.13f + grain * 0.07f;
            Vector2 uv = new Vector2(u, v);
            for (int crater = 0; crater < craterCount; crater++)
            {
                float radius = craterRadii[crater];
                float distance = Vector2.Distance(uv, craterCenters[crater]) / radius;
                if (distance > 1.18f) continue;
                float bowl = Mathf.Max(0f, 1f - distance * distance);
                float rim = Mathf.Exp(-Mathf.Pow((distance - 0.92f) / 0.12f, 2f));
                relief += -0.22f * bowl * bowl + 0.10f * rim;
            }
            float shade = Mathf.Clamp(0.76f + relief, 0.48f, 1f);
            pixels[y * resolution + x] = new Color(shade, shade, shade, 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        string absolutePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), texturePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolutePath));
        System.IO.File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }

    private static float LunarTerrainHeight(float x, float z)
    {
        float distanceFromOutpost = new Vector2(x, z).magnitude;
        float relief = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(19f, 32f, distanceFromOutpost));
        float broadNoise = (Mathf.PerlinNoise(x * 0.019f + 8.3f, z * 0.019f + 4.7f) - 0.5f) * 4.2f;
        float detailNoise = (Mathf.PerlinNoise(x * 0.083f + 21.4f, z * 0.083f + 11.9f) - 0.5f) * 0.65f;
        float height = TerrainBaseY + relief * (broadNoise + detailNoise);

        for (int i = 0; i < CraterCenters.Length; i++)
        {
            Vector3 crater = CraterCenters[i];
            float radius = 11f + (i % 3) * 2.8f;
            float normalizedRadius = Vector2.Distance(new Vector2(x, z), new Vector2(crater.x, crater.z)) / radius;
            if (normalizedRadius >= 1.2f) continue;
            float bowl = -2.2f * Mathf.Pow(Mathf.Max(0f, 1f - normalizedRadius * normalizedRadius), 2f);
            float rimDistance = (normalizedRadius - 0.88f) / 0.13f;
            float raisedRim = 0.8f * Mathf.Exp(-rimDistance * rimDistance);
            height += relief * (bowl + raisedRim);
        }

        return height;
    }

    private static void BuildOutpost()
    {
        GameObject baseRoot = new GameObject("SHACKLETON OUTPOST");
        BuildHabitat(baseRoot.transform, new Vector3(0f, 0f, 10f));
        BuildComms(baseRoot.transform, new Vector3(-10f, 0f, 5f));
        BuildSolar(baseRoot.transform, new Vector3(10f, 0f, 5f));
        BuildLifeSupport(baseRoot.transform, new Vector3(-10f, 0f, -8f));
        BuildGreenhouse(baseRoot.transform, new Vector3(0f, 0f, -12f));
        BuildRover(baseRoot.transform, new Vector3(10f, 0f, -9f));
        BuildOxygenProcessor(baseRoot.transform, new Vector3(-16f, 0f, -12f));
        BuildWaterRecycler(baseRoot.transform, new Vector3(16f, 0f, -12f));
        BuildResearchLab(baseRoot.transform, new Vector3(0f, 0f, 21f));
        BuildRadiationShelter(baseRoot.transform, new Vector3(-8f, 0f, 19f));
        AddBeacon(baseRoot.transform, new Vector3(0f, 0f, 10f), new Color(0.1f, 0.86f, 1f));
        AddBeacon(baseRoot.transform, new Vector3(-10f, 0f, 5f), new Color(0.1f, 0.86f, 1f));
        AddBeacon(baseRoot.transform, new Vector3(10f, 0f, 5f), new Color(0.25f, 1f, 0.69f));
        AddBeacon(baseRoot.transform, new Vector3(-10f, 0f, -8f), new Color(0.25f, 1f, 0.69f));
        AddBeacon(baseRoot.transform, new Vector3(0f, 0f, -12f), new Color(1f, 0.71f, 0.28f));
        AddBeacon(baseRoot.transform, new Vector3(10f, 0f, -9f), new Color(1f, 0.71f, 0.28f));
        AddBeacon(baseRoot.transform, new Vector3(-16f, 0f, -12f), new Color(0.22f, 0.72f, 1f));
        AddBeacon(baseRoot.transform, new Vector3(16f, 0f, -12f), new Color(0.16f, 0.92f, 0.84f));
        AddBeacon(baseRoot.transform, new Vector3(0f, 0f, 21f), new Color(0.65f, 0.55f, 1f));
        AddBeacon(baseRoot.transform, new Vector3(-8f, 0f, 19f), new Color(0.8f, 0.55f, 1f));
        BuildLandingPad(baseRoot.transform, new Vector3(0f, 0f, -1f));
    }

    private static void BuildAstronaut()
    {
        GameObject astronaut = new GameObject("ASTRONAUT - Junior Commander");
        astronaut.transform.position = new Vector3(0f, 0.05f, -1.5f);
        CharacterController controller = astronaut.AddComponent<CharacterController>();
        controller.height = 1.85f;
        controller.radius = 0.36f;
        controller.center = new Vector3(0f, 0.95f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.25f;

        GameObject model = new GameObject("Suit model");
        model.transform.SetParent(astronaut.transform, false);
        MakePrimitive(PrimitiveType.Capsule, "Suit body", model.transform, new Vector3(0f, 1.03f, 0f), new Vector3(0.66f, 0.86f, 0.49f), new Color(0.88f, 0.90f, 0.91f));
        MakePrimitive(PrimitiveType.Cylinder, "Helmet neck seal", model.transform, new Vector3(0f, 1.51f, 0f), new Vector3(0.30f, 0.13f, 0.30f), new Color(0.26f, 0.32f, 0.37f), 0.24f);
        MakePrimitive(PrimitiveType.Sphere, "White helmet shell", model.transform, new Vector3(0f, 1.84f, 0.02f), new Vector3(0.56f, 0.53f, 0.55f), new Color(0.91f, 0.93f, 0.94f), 0.12f);
        MakePrimitive(PrimitiveType.Sphere, "Dark blue helmet visor", model.transform, new Vector3(0f, 1.84f, 0.245f), new Vector3(0.43f, 0.34f, 0.20f), new Color(0.035f, 0.10f, 0.16f), 0.48f, new Color(0.015f, 0.10f, 0.18f));
        MakePrimitive(PrimitiveType.Cube, "Life support backpack", model.transform, new Vector3(0f, 1.08f, -0.40f), new Vector3(0.52f, 0.76f, 0.30f), new Color(0.18f, 0.25f, 0.31f), 0.18f);
        MakePrimitive(PrimitiveType.Cube, "Backpack cyan status panel", model.transform, new Vector3(0f, 1.10f, -0.558f), new Vector3(0.25f, 0.40f, 0.025f), new Color(0.05f, 0.67f, 0.82f), 0.12f, new Color(0.02f, 0.30f, 0.42f));
        MakePrimitive(PrimitiveType.Cube, "Chest mission patch", model.transform, new Vector3(0f, 1.20f, 0.252f), new Vector3(0.22f, 0.16f, 0.025f), new Color(0.06f, 0.63f, 0.78f), 0.1f, new Color(0.02f, 0.18f, 0.24f));
        MakePrimitive(PrimitiveType.Cube, "Chest oxygen indicator", model.transform, new Vector3(-0.18f, 1.37f, 0.20f), new Vector3(0.08f, 0.19f, 0.035f), new Color(0.94f, 0.63f, 0.18f), 0.05f, new Color(0.48f, 0.20f, 0.02f));

        Transform leftArm = CreateSuitLimb(model.transform, "Left arm pivot", new Vector3(-0.36f, 1.42f, 0f));
        MakePrimitive(PrimitiveType.Capsule, "Left sleeve", leftArm, new Vector3(0f, -0.22f, 0f), new Vector3(0.24f, 0.27f, 0.24f), new Color(0.81f, 0.85f, 0.87f));
        MakePrimitive(PrimitiveType.Sphere, "Left EVA glove", leftArm, new Vector3(0f, -0.45f, 0.02f), new Vector3(0.24f, 0.20f, 0.24f), new Color(0.85f, 0.88f, 0.89f));
        Transform rightArm = CreateSuitLimb(model.transform, "Right arm pivot", new Vector3(0.36f, 1.42f, 0f));
        MakePrimitive(PrimitiveType.Capsule, "Right sleeve", rightArm, new Vector3(0f, -0.22f, 0f), new Vector3(0.24f, 0.27f, 0.24f), new Color(0.81f, 0.85f, 0.87f));
        MakePrimitive(PrimitiveType.Sphere, "Right EVA glove", rightArm, new Vector3(0f, -0.45f, 0.02f), new Vector3(0.24f, 0.20f, 0.24f), new Color(0.85f, 0.88f, 0.89f));

        Transform leftLeg = CreateSuitLimb(model.transform, "Left leg pivot", new Vector3(-0.17f, 0.66f, 0f));
        MakePrimitive(PrimitiveType.Capsule, "Left trouser leg", leftLeg, new Vector3(0f, -0.23f, 0f), new Vector3(0.26f, 0.29f, 0.25f), new Color(0.80f, 0.84f, 0.86f));
        MakePrimitive(PrimitiveType.Cube, "Left boot", leftLeg, new Vector3(0f, -0.49f, 0.08f), new Vector3(0.32f, 0.18f, 0.42f), new Color(0.14f, 0.20f, 0.27f));
        MakePrimitive(PrimitiveType.Cube, "Left boot sole", leftLeg, new Vector3(0f, -0.57f, 0.10f), new Vector3(0.34f, 0.045f, 0.43f), new Color(0.89f, 0.55f, 0.15f));
        Transform rightLeg = CreateSuitLimb(model.transform, "Right leg pivot", new Vector3(0.17f, 0.66f, 0f));
        MakePrimitive(PrimitiveType.Capsule, "Right trouser leg", rightLeg, new Vector3(0f, -0.23f, 0f), new Vector3(0.26f, 0.29f, 0.25f), new Color(0.80f, 0.84f, 0.86f));
        MakePrimitive(PrimitiveType.Cube, "Right boot", rightLeg, new Vector3(0f, -0.49f, 0.08f), new Vector3(0.32f, 0.18f, 0.42f), new Color(0.14f, 0.20f, 0.27f));
        MakePrimitive(PrimitiveType.Cube, "Right boot sole", rightLeg, new Vector3(0f, -0.57f, 0.10f), new Vector3(0.34f, 0.045f, 0.43f), new Color(0.89f, 0.55f, 0.15f));

        GameObject target = new GameObject("Camera target");
        target.transform.SetParent(astronaut.transform, false);
        target.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        AstraPlayerController player = astronaut.AddComponent<AstraPlayerController>();
        SerializedObject playerData = new SerializedObject(player);
        playerData.FindProperty("cameraTarget").objectReferenceValue = target.transform;
        playerData.FindProperty("cameraDistance").floatValue = 8.6f;
        playerData.ApplyModifiedPropertiesWithoutUndo();

        AstraAstronautMotion motion = astronaut.AddComponent<AstraAstronautMotion>();
        SerializedObject motionData = new SerializedObject(motion);
        motionData.FindProperty("player").objectReferenceValue = player;
        motionData.FindProperty("modelRoot").objectReferenceValue = model.transform;
        motionData.FindProperty("leftArmPivot").objectReferenceValue = leftArm;
        motionData.FindProperty("rightArmPivot").objectReferenceValue = rightArm;
        motionData.FindProperty("leftLegPivot").objectReferenceValue = leftLeg;
        motionData.FindProperty("rightLegPivot").objectReferenceValue = rightLeg;
        motionData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform CreateSuitLimb(Transform parent, string objectName, Vector3 localPosition)
    {
        GameObject pivot = new GameObject(objectName);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = localPosition;
        return pivot.transform;
    }

    private static void BuildGameSystems()
    {
        GameObject manager = new GameObject("ASTRA GAME SYSTEMS");
        manager.AddComponent<AstraResourceManager>();
        manager.AddComponent<PlayerSettingsManager>();
        manager.AddComponent<LanguageManager>();
        manager.AddComponent<AccessibilityManager>();
        manager.AddComponent<AudioSource>();
        manager.AddComponent<VoiceGuideManager>();
        manager.AddComponent<VisualHintManager>();
        manager.AddComponent<TutorialManager>();
        manager.AddComponent<OnboardingManager>();
        manager.AddComponent<AstraGameManager>();
        GameObject hud = new GameObject("ASTRA HUD SYSTEM");
        hud.AddComponent<AstraHUD>();
        hud.AddComponent<AstraOnboardingUI>();
    }

    private static void BuildPracticeDeck()
    {
        GameObject practice = new GameObject("ASTRO SAFE PRACTICE DECK");
        MakePrimitive(PrimitiveType.Cylinder, "Practice platform", practice.transform, new Vector3(0f, -0.02f, 1.2f),
            new Vector3(10f, 0.08f, 5.6f), new Color(0.16f, 0.29f, 0.34f));

        AstraInteractable comms = Facility(practice.transform, "Practice Communications", FacilityType.Communications,
            "Practice Communications Panel", "A safe practice button. It does not change mission systems.", new Vector3(-5.2f, 0f, 0.7f), new Vector3(0.7f, 1.5f, 0.8f));
        MakePrimitive(PrimitiveType.Cube, "Practice comms pedestal", comms.transform, new Vector3(0f, 0.68f, 0f), new Vector3(1.25f, 1.25f, 0.78f), new Color(0.42f, 0.56f, 0.62f));
        MakePrimitive(PrimitiveType.Cube, "Practice comms button", comms.transform, new Vector3(0f, 1.32f, 0.42f), new Vector3(0.44f, 0.3f, 0.12f), new Color(0.12f, 0.70f, 0.85f), 0.2f, new Color(0.04f, 0.48f, 0.72f));

        AstraInteractable solar = Facility(practice.transform, "Practice Solar", FacilityType.SolarArray,
            "Practice Solar Panel", "A safe practice panel. It lights up when you use it.", new Vector3(5.2f, 0f, 0.7f), new Vector3(0.8f, 1.4f, 0.8f));
        MakePrimitive(PrimitiveType.Cube, "Practice solar stand", solar.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 1.1f, 0.32f), new Color(0.58f, 0.64f, 0.66f));
        MakePrimitive(PrimitiveType.Cube, "Practice solar panel", solar.transform, new Vector3(0f, 1.25f, 0f), new Vector3(2.0f, 0.12f, 1.25f), new Color(0.08f, 0.26f, 0.52f), 0.35f, new Color(0.04f, 0.18f, 0.42f));

        GameObject robot = new GameObject("ASTRO - Robot Companion");
        robot.transform.position = new Vector3(-2.55f, 0f, 2.7f);
        Transform body = MakePrimitive(PrimitiveType.Sphere, "ASTRO round body", robot.transform, new Vector3(0f, 0.67f, 0f), new Vector3(0.78f, 0.83f, 0.62f), new Color(0.36f, 0.72f, 0.79f), 0.15f, new Color(0.08f, 0.40f, 0.48f)).transform;
        Transform head = MakePrimitive(PrimitiveType.Sphere, "ASTRO friendly head", robot.transform, new Vector3(0f, 1.25f, 0.03f), new Vector3(0.86f, 0.7f, 0.72f), new Color(0.62f, 0.83f, 0.86f)).transform;
        MakePrimitive(PrimitiveType.Sphere, "ASTRO left eye", robot.transform, new Vector3(-0.2f, 1.31f, 0.36f), Vector3.one * 0.1f, new Color(0.08f, 0.24f, 0.34f), 0.2f, new Color(0.1f, 0.8f, 1f));
        GameObject eyes = MakePrimitive(PrimitiveType.Sphere, "ASTRO right eye", robot.transform, new Vector3(0.2f, 1.31f, 0.36f), Vector3.one * 0.1f, new Color(0.08f, 0.24f, 0.34f), 0.2f, new Color(0.1f, 0.8f, 1f));
        MakePrimitive(PrimitiveType.Sphere, "ASTRO smile", robot.transform, new Vector3(0f, 1.08f, 0.365f), new Vector3(0.20f, 0.045f, 0.045f), new Color(0.08f, 0.24f, 0.34f));
        MakePrimitive(PrimitiveType.Sphere, "ASTRO antenna light", robot.transform, new Vector3(0f, 1.73f, 0f), Vector3.one * 0.17f, new Color(1f, 0.75f, 0.23f), 0.2f, new Color(1f, 0.58f, 0.1f));
        Transform arm = CreateSuitLimb(robot.transform, "ASTRO pointing arm", new Vector3(0.48f, 0.87f, 0f));
        MakePrimitive(PrimitiveType.Capsule, "ASTRO arm", arm, new Vector3(0.16f, 0f, 0f), new Vector3(0.22f, 0.25f, 0.22f), new Color(0.55f, 0.79f, 0.83f));
        GameObject fingertip = MakePrimitive(PrimitiveType.Sphere, "ASTRO finger light", arm, new Vector3(0.33f, 0.02f, 0.02f), Vector3.one * 0.14f, Gold(), 0.2f, Gold());
        AstroCompanionController companion = robot.AddComponent<AstroCompanionController>();
        SerializedObject astroData = new SerializedObject(companion);
        astroData.FindProperty("body").objectReferenceValue = body;
        astroData.FindProperty("head").objectReferenceValue = head;
        astroData.FindProperty("pointingArm").objectReferenceValue = arm;
        astroData.FindProperty("eyeGlow").objectReferenceValue = eyes.GetComponent<Renderer>();
        astroData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Color Gold() => new Color(1f, 0.76f, 0.24f);

    private static void BuildHabitat(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Habitat", FacilityType.Habitat, "Crew habitat", "A pressurized shelter. Inspecting it can improve the simulated habitat condition.", position, new Vector3(0.52f, 3f, 0.25f));
        MakePrimitive(PrimitiveType.Cylinder, "Main pressure module", facility.transform, new Vector3(0f, 1.5f, 0f), new Vector3(3.5f, 1.45f, 3.2f), new Color(0.67f, 0.72f, 0.75f));
        MakePrimitive(PrimitiveType.Sphere, "Habitat dome", facility.transform, new Vector3(0f, 3.6f, 0f), new Vector3(3.1f, 1.4f, 3.1f), new Color(0.44f, 0.56f, 0.63f));
        MakePrimitive(PrimitiveType.Cylinder, "Habitat lower pressure collar", facility.transform, new Vector3(0f, 0.52f, 0f), new Vector3(3.62f, 0.10f, 3.32f), new Color(0.87f, 0.57f, 0.18f), 0.22f);
        MakePrimitive(PrimitiveType.Cylinder, "Habitat upper pressure collar", facility.transform, new Vector3(0f, 2.48f, 0f), new Vector3(3.58f, 0.09f, 3.28f), new Color(0.87f, 0.57f, 0.18f), 0.22f);
        for (int rib = 0; rib < 8; rib++)
        {
            float angle = rib * 45f * Mathf.Deg2Rad;
            Vector3 positionAroundModule = new Vector3(Mathf.Sin(angle) * 1.69f, 1.48f, Mathf.Cos(angle) * 1.53f);
            GameObject support = MakePrimitive(PrimitiveType.Cylinder, "Habitat structural rib", facility.transform,
                positionAroundModule, new Vector3(0.075f, 0.89f, 0.075f), new Color(0.84f, 0.88f, 0.90f), 0.12f);
            support.transform.localRotation = Quaternion.Euler(0f, rib * 45f, 0f);
        }
        MakePrimitive(PrimitiveType.Cube, "Airlock", facility.transform, new Vector3(0f, 0.8f, 2.1f), new Vector3(1.7f, 1.6f, 1.3f), new Color(0.49f, 0.58f, 0.63f));
        MakePrimitive(PrimitiveType.Cube, "Airlock hatch", facility.transform, new Vector3(0f, 0.90f, 2.78f), new Vector3(0.86f, 1.32f, 0.08f), new Color(0.12f, 0.22f, 0.30f), 0.3f);
        MakePrimitive(PrimitiveType.Cube, "Airlock hatch window", facility.transform, new Vector3(0f, 1.32f, 2.84f), new Vector3(0.50f, 0.31f, 0.035f), new Color(0.04f, 0.46f, 0.68f), 0.38f, new Color(0.02f, 0.19f, 0.31f));
        for (int window = -1; window <= 1; window++)
        {
            MakePrimitive(PrimitiveType.Cube, "Habitat porthole frame", facility.transform, new Vector3(window * 0.92f, 1.83f, 1.58f),
                new Vector3(0.62f, 0.62f, 0.13f), new Color(0.22f, 0.31f, 0.38f), 0.22f);
            MakePrimitive(PrimitiveType.Cube, "Habitat blue porthole", facility.transform, new Vector3(window * 0.92f, 1.83f, 1.66f),
                new Vector3(0.46f, 0.46f, 0.045f), new Color(0.08f, 0.49f, 0.67f), 0.32f, new Color(0.02f, 0.20f, 0.33f));
        }
        MakePrimitive(PrimitiveType.Cube, "Solar wing left", facility.transform, new Vector3(-3.8f, 1.2f, 0f), new Vector3(2.4f, 0.08f, 1.8f), new Color(0.1f, 0.27f, 0.46f)).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        MakePrimitive(PrimitiveType.Cube, "Solar wing right", facility.transform, new Vector3(3.8f, 1.2f, 0f), new Vector3(2.4f, 0.08f, 1.8f), new Color(0.1f, 0.27f, 0.46f)).transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        MakePrimitive(PrimitiveType.Cylinder, "Airlock beacon", facility.transform, new Vector3(1.9f, 2.4f, 1.9f), new Vector3(0.1f, 0.38f, 0.1f), new Color(0.1f, 0.86f, 1f), 0.2f, new Color(0.1f, 0.86f, 1f));
    }

    private static void BuildComms(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Communications", FacilityType.Communications, "Communications station", "Aim the relay and establish a link with Mission Control.", position, new Vector3(0.58f, 2f, 0.65f));
        MakePrimitive(PrimitiveType.Cube, "Relay equipment cabinet", facility.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.8f, 2.2f, 1.5f), new Color(0.59f, 0.65f, 0.68f));
        MakePrimitive(PrimitiveType.Cylinder, "Relay mast", facility.transform, new Vector3(0f, 3.1f, 0f), new Vector3(0.12f, 1.3f, 0.12f), new Color(0.78f, 0.82f, 0.82f));
        GameObject dish = MakePrimitive(PrimitiveType.Cylinder, "Relay dish", facility.transform, new Vector3(0.2f, 4.25f, 0.1f), new Vector3(1.05f, 0.12f, 1.05f), new Color(0.75f, 0.78f, 0.78f));
        dish.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
        MakePrimitive(PrimitiveType.Cube, "Control screen", facility.transform, new Vector3(0f, 1.9f, 0.8f), new Vector3(0.85f, 0.52f, 0.08f), new Color(0.05f, 0.48f, 0.65f), 0.25f, new Color(0.05f, 0.48f, 0.65f));
    }

    private static void BuildSolar(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Solar Array", FacilityType.SolarArray, "Solar power station", "Deploy and align the array. Solar generation slows during simulated dust events.", position, new Vector3(0.5f, 3f, 0.4f));
        for (int i = -1; i <= 1; i++)
        {
            MakePrimitive(PrimitiveType.Cylinder, "Array support", facility.transform, new Vector3(i * 2.1f, 0.9f, 0f), new Vector3(0.11f, 0.9f, 0.11f), new Color(0.68f, 0.72f, 0.72f));
            GameObject panel = MakePrimitive(PrimitiveType.Cube, "Photovoltaic panel", facility.transform, new Vector3(i * 2.1f, 1.85f, 0f), new Vector3(1.85f, 0.12f, 2.5f), new Color(0.07f, 0.22f, 0.42f), 0.38f, new Color(0.04f, 0.19f, 0.38f));
            panel.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            for (int line = -1; line <= 1; line++)
                MakePrimitive(PrimitiveType.Cube, "Panel cell", facility.transform, new Vector3(i * 2.1f, 1.93f, line * 0.56f), new Vector3(1.62f, 0.025f, 0.035f), new Color(0.17f, 0.52f, 0.71f), 0.35f);
        }
        MakePrimitive(PrimitiveType.Cylinder, "Array controller", facility.transform, new Vector3(0f, 0.6f, 1.9f), new Vector3(0.65f, 0.65f, 0.65f), new Color(0.48f, 0.55f, 0.59f));
    }

    private static void BuildLifeSupport(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Life Support", FacilityType.LifeSupport, "Life-support systems", "Inspect the oxygen and water loop before the crew begins surface operations.", position, new Vector3(0.62f, 2.5f, 0.58f));
        MakePrimitive(PrimitiveType.Cylinder, "Oxygen processing tank", facility.transform, new Vector3(-1.25f, 1.4f, 0f), new Vector3(0.8f, 1.4f, 0.8f), new Color(0.69f, 0.75f, 0.77f));
        MakePrimitive(PrimitiveType.Cylinder, "Water reclamation tank", facility.transform, new Vector3(1.25f, 1.4f, 0f), new Vector3(0.8f, 1.4f, 0.8f), new Color(0.38f, 0.63f, 0.75f));
        MakePrimitive(PrimitiveType.Cube, "Life support control panel", facility.transform, new Vector3(0f, 1.3f, 1.1f), new Vector3(2f, 1.5f, 0.2f), new Color(0.29f, 0.38f, 0.43f));
        MakePrimitive(PrimitiveType.Cube, "Control display", facility.transform, new Vector3(0f, 1.75f, 1.22f), new Vector3(1.15f, 0.48f, 0.06f), new Color(0.12f, 0.72f, 0.79f), 0.2f, new Color(0.1f, 0.72f, 0.79f));
        MakePrimitive(PrimitiveType.Cylinder, "Pipe manifold", facility.transform, new Vector3(0f, 2.8f, 0f), new Vector3(1.7f, 0.16f, 0.16f), new Color(0.75f, 0.78f, 0.78f));
    }

    private static void BuildGreenhouse(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Greenhouse", FacilityType.Greenhouse, "Food production greenhouse", "A small grow module supports the crew but uses power for lighting and climate control.", position, new Vector3(0.75f, 2.3f, 0.65f));
        MakePrimitive(PrimitiveType.Cube, "Grow module frame", facility.transform, new Vector3(0f, 1.25f, 0f), new Vector3(4.4f, 2.4f, 3.3f), new Color(0.34f, 0.5f, 0.53f), 0.35f);
        MakePrimitive(PrimitiveType.Cube, "Greenhouse glazing", facility.transform, new Vector3(0f, 1.45f, 1.66f), new Vector3(3.9f, 1.65f, 0.08f), new Color(0.09f, 0.43f, 0.48f), 0.3f, new Color(0.05f, 0.25f, 0.3f));
        for (int i = -1; i <= 1; i++)
        {
            MakePrimitive(PrimitiveType.Cylinder, "Plant bed", facility.transform, new Vector3(i * 1.15f, 0.33f, -0.15f), new Vector3(0.62f, 0.25f, 0.65f), new Color(0.31f, 0.24f, 0.17f));
            MakePrimitive(PrimitiveType.Sphere, "Leaf cluster", facility.transform, new Vector3(i * 1.15f, 0.9f, 0.15f), new Vector3(0.58f, 0.8f, 0.58f), new Color(0.23f, 0.54f, 0.36f));
        }
    }

    private static void BuildRover(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Lunar Rover", FacilityType.Rover, "Lunar exploration rover", "Collect a virtual regolith sample when the outpost is stabilized.", position, new Vector3(0.58f, 1.4f, 0.52f));
        MakePrimitive(PrimitiveType.Cube, "Rover chassis", facility.transform, new Vector3(0f, 0.95f, 0f), new Vector3(3.1f, 0.75f, 2.2f), new Color(0.71f, 0.73f, 0.67f));
        MakePrimitive(PrimitiveType.Cube, "Rover instrument deck", facility.transform, new Vector3(0f, 1.6f, 0f), new Vector3(1.9f, 0.45f, 1.7f), new Color(0.55f, 0.61f, 0.62f));
        MakePrimitive(PrimitiveType.Cylinder, "Sample mast", facility.transform, new Vector3(0f, 2.5f, 0.15f), new Vector3(0.1f, 0.85f, 0.1f), new Color(0.77f, 0.79f, 0.76f));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            GameObject wheel = MakePrimitive(PrimitiveType.Cylinder, "Rover wheel", facility.transform, new Vector3(x * 1.38f, 0.49f, z * 0.82f), new Vector3(0.39f, 0.18f, 0.39f), new Color(0.12f, 0.16f, 0.19f));
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        MakePrimitive(PrimitiveType.Cube, "Sample container", facility.transform, new Vector3(0.9f, 1.55f, 0.55f), new Vector3(0.48f, 0.42f, 0.45f), new Color(0.92f, 0.7f, 0.3f));
    }

    private static void BuildOxygenProcessor(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Oxygen Processor", FacilityType.OxygenProcessor,
            "Oxygen processor", "A powered processor represents oxygen production in this simplified game.", position, new Vector3(2.8f, 2.5f, 2.8f));
        MakePrimitive(PrimitiveType.Cube, "Processor housing", facility.transform, new Vector3(0f, 1.25f, 0f), new Vector3(2.7f, 2.4f, 2.4f), new Color(0.60f, 0.68f, 0.73f), 0.24f);
        MakePrimitive(PrimitiveType.Cylinder, "Oxygen storage tank", facility.transform, new Vector3(-1.4f, 1.05f, 0f), new Vector3(0.55f, 0.92f, 0.55f), new Color(0.67f, 0.76f, 0.80f), 0.18f);
        MakePrimitive(PrimitiveType.Cube, "Oxygen status light", facility.transform, new Vector3(0f, 1.68f, 1.22f), new Vector3(0.62f, 0.22f, 0.08f), new Color(0.18f, 0.72f, 1f), 0.1f, new Color(0.05f, 0.32f, 0.65f));
        MakePrimitive(PrimitiveType.Cylinder, "Oxygen transfer pipe", facility.transform, new Vector3(1.55f, 0.48f, 0f), new Vector3(0.17f, 0.13f, 1.1f), new Color(0.34f, 0.49f, 0.57f));
    }

    private static void BuildWaterRecycler(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Water Recycler", FacilityType.WaterRecycler,
            "Water recycler", "A powered recycling unit returns simulated water to the outpost reserve.", position, new Vector3(2.8f, 2.5f, 2.8f));
        MakePrimitive(PrimitiveType.Cube, "Recycler housing", facility.transform, new Vector3(0f, 1.22f, 0f), new Vector3(2.6f, 2.25f, 2.5f), new Color(0.55f, 0.68f, 0.69f), 0.2f);
        MakePrimitive(PrimitiveType.Cylinder, "Water reserve tank", facility.transform, new Vector3(1.45f, 1.05f, 0f), new Vector3(0.57f, 0.94f, 0.57f), new Color(0.48f, 0.65f, 0.70f), 0.16f);
        MakePrimitive(PrimitiveType.Cube, "Water status light", facility.transform, new Vector3(0f, 1.65f, 1.27f), new Vector3(0.62f, 0.22f, 0.08f), new Color(0.16f, 0.92f, 0.84f), 0.1f, new Color(0.02f, 0.37f, 0.33f));
        MakePrimitive(PrimitiveType.Cylinder, "Recycler return pipe", facility.transform, new Vector3(-1.50f, 0.48f, 0f), new Vector3(0.17f, 0.13f, 1.0f), new Color(0.31f, 0.49f, 0.52f));
    }

    private static void BuildResearchLab(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Research Laboratory", FacilityType.ResearchLab,
            "Research laboratory", "A small field lab stores and studies virtual lunar samples.", position, new Vector3(4.6f, 2.8f, 4.2f));
        MakePrimitive(PrimitiveType.Cylinder, "Laboratory pressure module", facility.transform, new Vector3(0f, 1.4f, 0f), new Vector3(3.3f, 1.35f, 3.0f), new Color(0.63f, 0.68f, 0.74f), 0.18f);
        MakePrimitive(PrimitiveType.Cube, "Lab entry airlock", facility.transform, new Vector3(0f, 0.95f, 2.10f), new Vector3(1.65f, 1.9f, 0.7f), new Color(0.46f, 0.55f, 0.62f), 0.24f);
        MakePrimitive(PrimitiveType.Cube, "Lab observation window", facility.transform, new Vector3(0f, 1.65f, 1.58f), new Vector3(1.55f, 0.72f, 0.08f), new Color(0.12f, 0.38f, 0.54f), 0.35f, new Color(0.02f, 0.10f, 0.20f));
        MakePrimitive(PrimitiveType.Cube, "Sample analysis console", facility.transform, new Vector3(1.2f, 0.67f, 0.8f), new Vector3(0.95f, 1.25f, 0.78f), new Color(0.23f, 0.31f, 0.39f), 0.28f);
        MakePrimitive(PrimitiveType.Sphere, "Sample containment vessel", facility.transform, new Vector3(1.2f, 1.45f, 0.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(0.88f, 0.66f, 0.32f), 0.1f, new Color(0.24f, 0.12f, 0.03f));
    }

    private static void BuildRadiationShelter(Transform parent, Vector3 position)
    {
        AstraInteractable facility = Facility(parent, "Radiation Shelter", FacilityType.RadiationShelter,
            "Radiation shelter", "Prepare a crew shelter for a simulated solar-particle alert. This simplified mechanic is not a radiation-dose prediction.", position, new Vector3(3.8f, 2.6f, 3.8f));
        MakePrimitive(PrimitiveType.Cube, "Shelter module", facility.transform, new Vector3(0f, 1.25f, 0f), new Vector3(4.3f, 2.4f, 4.1f), new Color(0.38f, 0.43f, 0.53f));
        MakePrimitive(PrimitiveType.Cube, "Shelter access door", facility.transform, new Vector3(0f, 0.86f, 2.08f), new Vector3(1.3f, 1.7f, 0.08f), new Color(0.12f, 0.72f, 0.79f), 0.12f, new Color(0.04f, 0.34f, 0.45f));
        MakePrimitive(PrimitiveType.Cube, "Regolith shielding berm", facility.transform, new Vector3(-2.5f, 0.48f, 0f), new Vector3(0.55f, 0.95f, 4.6f), new Color(0.43f, 0.40f, 0.34f));
        MakePrimitive(PrimitiveType.Cube, "Regolith shielding berm", facility.transform, new Vector3(2.5f, 0.48f, 0f), new Vector3(0.55f, 0.95f, 4.6f), new Color(0.43f, 0.40f, 0.34f));
    }

    private static AstraInteractable Facility(Transform parent, string objectName, FacilityType type, string title, string description, Vector3 position, Vector3 colliderSize)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        AstraInteractable interactable = root.AddComponent<AstraInteractable>();
        interactable.facilityType = type;
        interactable.facilityName = title;
        interactable.description = description;
        interactable.interactionRange = type == FacilityType.Rover ? 4.5f : 4f;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, colliderSize.y * 0.48f, 0f);
        collider.size = colliderSize;
        return interactable;
    }

    private static void BuildLandingPad(Transform parent, Vector3 position)
    {
        GameObject pad = MakePrimitive(PrimitiveType.Cylinder, "Landing pad", parent, position + new Vector3(0f, 0.02f, 0f), new Vector3(5.5f, 0.1f, 5.5f), new Color(0.35f, 0.39f, 0.42f));
        pad.GetComponent<Collider>().enabled = false;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 4.4f);
            MakePrimitive(PrimitiveType.Cube, "Landing marking", parent, position + offset + Vector3.up * 0.05f, new Vector3(0.28f, 0.06f, 1.2f), new Color(0.79f, 0.74f, 0.56f));
        }
    }

    private static void AddBeacon(Transform parent, Vector3 position, Color color)
    {
        GameObject beacon = new GameObject("Facility navigation beacon");
        beacon.transform.SetParent(parent, false);
        beacon.transform.localPosition = position + new Vector3(0f, 3.15f, 0f);
        GameObject stem = MakePrimitive(PrimitiveType.Cylinder, "Beacon stem", beacon.transform, new Vector3(0f, -0.35f, 0f), new Vector3(0.045f, 0.45f, 0.045f), color);
        GameObject lamp = MakePrimitive(PrimitiveType.Sphere, "Beacon light", beacon.transform, Vector3.zero, Vector3.one * 0.23f, color, 0.2f, color);
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 4f;
        light.intensity = 0.8f;
    }

    private static void CreateEarth(Transform parent)
    {
        MakePrimitive(PrimitiveType.Sphere, "Earth in the lunar sky", parent, new Vector3(34f, 27f, 118f), Vector3.one * 8f,
            new Color(0.12f, 0.35f, 0.70f), 0.08f, new Color(0.025f, 0.09f, 0.24f));
    }

    private static void CreateStars(Transform parent)
    {
        System.Random random = new System.Random(12);
        for (int i = 0; i < 150; i++)
        {
            float x = (float)(random.NextDouble() * 250.0 - 125.0);
            float y = (float)(random.NextDouble() * 72.0 + 18.0);
            float z = (float)(random.NextDouble() * 250.0 - 70.0);
            float size = i % 11 == 0 ? 0.17f : 0.075f;
            MakePrimitive(PrimitiveType.Sphere, "Distant star", parent, new Vector3(x, y, z), Vector3.one * size,
                Color.white, 0f, new Color(0.7f, 0.82f, 1f));
        }
    }

    private static void CreateRockField(Transform parent)
    {
        System.Random random = new System.Random(2026);
        for (int i = 0; i < 190; i++)
        {
            float x = (float)(random.NextDouble() * 180.0 - 90.0);
            float z = (float)(random.NextDouble() * 180.0 - 90.0);
            if (Vector2.Distance(new Vector2(x, z), Vector2.zero) < 23f) continue;
            float size = (float)(random.NextDouble() * 0.62 + 0.16);
            float y = LunarTerrainHeight(x, z) + size * 0.28f;
            GameObject rock = MakePrimitive(PrimitiveType.Sphere, "Regolith boulder", parent, new Vector3(x, y, z), new Vector3(size * 1.25f, size * 0.68f, size), Color.Lerp(new Color(0.32f, 0.30f, 0.39f), new Color(0.49f, 0.46f, 0.56f), (float)random.NextDouble()));
            rock.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 12f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 12f);
        }
    }

    private static void CreateDistantRidge(Transform parent)
    {
        CreateMountainRidge(parent, "South-pole highlands foreground", 91f, 125f, 5f, 16f, new Color(0.34f, 0.31f, 0.42f), 31);
        CreateMountainRidge(parent, "South-pole highlands far ridge", 108f, 135f, 13f, 26f, new Color(0.19f, 0.17f, 0.28f), 67);
    }

    private static void CreateMountainRidge(Transform parent, string objectName, float z, float halfWidth, float minHeight, float maxHeight, Color color, int seed)
    {
        const int segments = 64;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Lerp(-halfWidth, halfWidth, (float)i / segments);
            float noise = Mathf.PerlinNoise((x + seed * 3f) * 0.018f, seed * 0.071f);
            float serration = Mathf.Max(0f, Mathf.Sin(i * 1.37f + seed) * 0.22f);
            float peak = Mathf.Lerp(minHeight, maxHeight, noise) + serration * (maxHeight - minHeight);
            int vertex = i * 2;
            vertices[vertex] = new Vector3(x, TerrainBaseY, z);
            vertices[vertex + 1] = new Vector3(x, peak, z);
            uvs[vertex] = new Vector2((float)i / segments, 0f);
            uvs[vertex + 1] = new Vector2((float)i / segments, 1f);

            if (i == segments) continue;
            int triangle = i * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }

        Mesh ridgeMesh = new Mesh { name = objectName + " mesh" };
        ridgeMesh.vertices = vertices;
        ridgeMesh.uv = uvs;
        ridgeMesh.triangles = triangles;
        ridgeMesh.RecalculateNormals();
        ridgeMesh.RecalculateBounds();
        ridgeMesh = StoreGeneratedMesh(ridgeMesh, objectName.Replace(' ', '_'));
        GameObject ridge = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        ridge.transform.SetParent(parent, false);
        ridge.GetComponent<MeshFilter>().sharedMesh = ridgeMesh;
        SetMaterial(ridge, objectName, color, 0f);
        ridge.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        ridge.GetComponent<Renderer>().receiveShadows = false;
    }

    private static Mesh StoreGeneratedMesh(Mesh generatedMesh, string assetName)
    {
        const string folder = "Assets/_Project/Art/Meshes";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "Meshes");

        string path = folder + "/" + assetName + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
        }
        AssetDatabase.CreateAsset(generatedMesh, path);
        return generatedMesh;
    }

    private static GameObject MakePrimitive(PrimitiveType type, string objectName, Transform parent, Vector3 position, Vector3 scale, Color color, float metallic = 0.05f, Color? emission = null)
    {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        SetMaterial(item, "ASTRA " + objectName, color, metallic, emission);
        Collider collider = item.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        return item;
    }

    private static void SetMaterial(GameObject target, string materialName, Color color, float metallic, Color? emission = null)
    {
        Material material = GetMaterial(materialName, color, metallic, emission);
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private static Material GetMaterial(string materialName, Color color, float metallic, Color? emission)
    {
        string key = materialName + color + metallic + emission;
        if (Materials.TryGetValue(key, out Material existing)) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color, enableInstancing = true };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission.Value);
        }
        string safeName = materialName.Replace("/", "-").Replace("\\", "-").Replace(":", "-").Replace(" ", "_");
        string path = "Assets/_Project/Art/Materials/" + materialAssetIndex++.ToString("D3") + "_" + safeName + ".mat";
        Material generated = material;
        Material prior = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (prior != null)
        {
            prior.shader = material.shader;
            prior.color = color;
            if (prior.HasProperty("_Metallic")) prior.SetFloat("_Metallic", metallic);
            if (emission.HasValue && prior.HasProperty("_EmissionColor"))
            {
                prior.EnableKeyword("_EMISSION");
                prior.SetColor("_EmissionColor", emission.Value);
            }
            UnityEngine.Object.DestroyImmediate(generated);
            material = prior;
        }
        else AssetDatabase.CreateAsset(material, path);
        Materials[key] = material;
        return material;
    }
}
