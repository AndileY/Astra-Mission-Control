using System.IO;
using UnityEditor;
using UnityEngine;

namespace Astra.Editor
{
    /// <summary>
    /// Keeps the legacy import hook harmless when NASA GLBs are stored only in
    /// StreamingAssets and loaded on demand by AstraRuntimeModelLoader.
    /// </summary>
    [InitializeOnLoad]
    internal static class AstraNASAAssetImportOnOpen
    {
        private const string ImportFlag = "ASTRA_NASA_GLB_IMPORT_V1";
        private const string ImportProgress = "ASTRA_NASA_GLB_IMPORT_PROGRESS_V1";
        private const string ModelDirectory = "Assets/Resources/NASA/MissionHardware/Processed";
        private static string[] pendingModels;
        private static int nextModel;
        private static int importedModels;
        private static int failedModels;

        static AstraNASAAssetImportOnOpen()
        {
            EditorApplication.delayCall += BeginNASAImport;
        }

        private static void BeginNASAImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BeginNASAImport;
                return;
            }
            if (SessionState.GetBool(ImportFlag, false)) return;

            if (!Directory.Exists(ModelDirectory))
            {
                return;
            }

            pendingModels = Directory.GetFiles(ModelDirectory, "*.glb", SearchOption.TopDirectoryOnly);
            if (pendingModels.Length == 0) return;
            System.Array.Sort(pendingModels, System.StringComparer.OrdinalIgnoreCase);
            nextModel = SessionState.GetInt(ImportProgress, 0);
            importedModels = failedModels = 0;
            EditorApplication.update += ImportNextNASAAsset;
        }

        private static void ImportNextNASAAsset()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (pendingModels == null || nextModel >= pendingModels.Length)
            {
                EditorApplication.update -= ImportNextNASAAsset;
                AssetDatabase.SaveAssets();
                SessionState.SetBool(ImportFlag, true);
                Debug.Log("ASTRA NASA model import finished: " + importedModels + " ready, " + failedModels + " failed.");
                return;
            }

            string assetPath = pendingModels[nextModel++].Replace('\\', '/');
            SessionState.SetInt(ImportProgress, nextModel);
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (model == null)
                {
                    failedModels++;
                    Debug.LogError("ASTRA could not import NASA model as a Unity GameObject: " + assetPath + ". Check the glTFast/Draco import report.");
                    return;
                }

                importedModels++;
                Debug.Log("ASTRA imported NASA model: " + assetPath + " (" + model.name + ")");
            }
            catch (System.Exception exception)
            {
                failedModels++;
                Debug.LogException(exception);
            }
        }
    }
}
