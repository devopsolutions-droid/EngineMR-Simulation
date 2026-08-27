using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ModelExporter : EditorWindow
{
    private GameObject sourcePrefab;
    private string targetFolderPath = @"C:\Users\ADMIN\Desktop\Debojit\EngineVR Simulation\EngineVRSimulation\Assets\All Engines";
    private bool stripCustomScripts = true;
    private bool copyMetaFiles = true;
    private bool exportOnlyVisuals = true;

    [MenuItem("Tools/Model Exporter")]
    public static void ShowWindow()
    {
        GetWindow<ModelExporter>("Model Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Export Model/GameObject with Dependencies", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Object/Prefab", sourcePrefab, typeof(GameObject), true);

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        targetFolderPath = EditorGUILayout.TextField("Target Folder", targetFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(75)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Export Target Folder", targetFolderPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                targetFolderPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        exportOnlyVisuals = EditorGUILayout.Toggle("Only Model, Materials, Textures", exportOnlyVisuals);
        stripCustomScripts = EditorGUILayout.Toggle("Strip Custom Scripts", stripCustomScripts);
        copyMetaFiles = EditorGUILayout.Toggle("Copy .meta Files", copyMetaFiles);

        GUILayout.Space(15);

        if (GUILayout.Button("Export Model", GUILayout.Height(40)))
        {
            Export();
        }
    }

    private bool IsVisualAsset(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".prefab":
            case ".fbx":
            case ".obj":
            case ".blend":
            case ".dae":
            case ".3ds":
            case ".mat":
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".psd":
            case ".tiff":
            case ".tif":
            case ".exr":
            case ".hdr":
            case ".bmp":
            case ".shader":
            case ".shadergraph":
            case ".shadersubgraph":
            case ".controller":
            case ".overridecontroller":
            case ".anim":
            case ".mask":
                return true;
            default:
                return false;
        }
    }

    private void Export()
    {
        if (sourcePrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a Source Object or Prefab to export.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(targetFolderPath) || !Directory.Exists(targetFolderPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid target folder.", "OK");
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
        bool needsTempPrefabCleanup = false;
        string tempPrefabPath = "";

        // If it's a scene GameObject (not an asset path)
        if (string.IsNullOrEmpty(prefabPath))
        {
            string tempDir = "Assets/TempExporter";
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            tempPrefabPath = $"{tempDir}/{sourcePrefab.name}_TempSceneExport.prefab";
            
            // Create temporary prefab of the scene object
            GameObject tempAsset = PrefabUtility.SaveAsPrefabAsset(sourcePrefab, tempPrefabPath);
            if (tempAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to create a temporary prefab from the scene object.", "OK");
                return;
            }
            prefabPath = tempPrefabPath;
            needsTempPrefabCleanup = true;
        }

        // 1. Gather all dependencies
        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        
        List<string> assetsToCopy = new List<string>();
        List<string> scriptsToExclude = new List<string>();

        // Explicitly include the prefab/model file itself
        assetsToCopy.Add(prefabPath);

        foreach (string dep in dependencies)
        {
            if (dep.StartsWith("Resources/") || dep.StartsWith("Library/") || dep.StartsWith("Packages/"))
            {
                continue;
            }

            if (dep.EndsWith(".cs"))
            {
                scriptsToExclude.Add(dep);
                continue;
            }

            if (exportOnlyVisuals && !IsVisualAsset(dep))
            {
                continue;
            }

            if (!assetsToCopy.Contains(dep))
            {
                assetsToCopy.Add(dep);
            }
        }

        Debug.Log($"[ModelExporter] Found {assetsToCopy.Count} asset dependencies. Excluded {scriptsToExclude.Count} script files.");

        // 2. Setup temporary prefab copy if stripping scripts
        string finalPrefabPath = prefabPath;
        bool createdTempPrefabForStripping = false;
        string tempStripPrefabPath = "";

        if (stripCustomScripts)
        {
            string pathToModify = prefabPath;
            bool overwriteExisting = false;

            if (needsTempPrefabCleanup)
            {
                pathToModify = tempPrefabPath;
                overwriteExisting = true;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(pathToModify);
            if (instance != null)
            {
                try
                {
                    // Remove missing script components
                    int missingScriptsRemoved = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(instance);
                    Debug.Log($"[ModelExporter] Removed {missingScriptsRemoved} missing script components.");

                    // Remove custom script components
                    MonoBehaviour[] monoBehaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
                    int customScriptsRemoved = 0;
                    for (int i = monoBehaviours.Length - 1; i >= 0; i--)
                    {
                        var mb = monoBehaviours[i];
                        if (mb == null) continue;

                        MonoScript script = MonoScript.FromMonoBehaviour(mb);
                        if (script != null)
                        {
                            string scriptPath = AssetDatabase.GetAssetPath(script);
                            if (scriptPath.StartsWith("Assets/") && scriptPath.EndsWith(".cs"))
                            {
                                DestroyImmediate(mb, true);
                                customScriptsRemoved++;
                            }
                        }
                    }

                    Debug.Log($"[ModelExporter] Stripped {customScriptsRemoved} custom script components.");

                    if (overwriteExisting)
                    {
                        PrefabUtility.SaveAsPrefabAsset(instance, pathToModify);
                    }
                    else
                    {
                        string tempDir = "Assets/TempExporter";
                        if (!Directory.Exists(tempDir))
                        {
                            Directory.CreateDirectory(tempDir);
                        }
                        tempStripPrefabPath = $"{tempDir}/{sourcePrefab.name}_ExportCopy.prefab";
                        PrefabUtility.SaveAsPrefabAsset(instance, tempStripPrefabPath);
                        finalPrefabPath = tempStripPrefabPath;
                        createdTempPrefabForStripping = true;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
        }

        // 3. Copy files preserving relative structure
        int copiedCount = 0;
        foreach (string assetPath in assetsToCopy)
        {
            string sourcePath = assetPath;
            if (createdTempPrefabForStripping && assetPath == prefabPath)
            {
                sourcePath = tempStripPrefabPath;
            }

            // Copy to the target preserving relative Assets/ structure
            string relativePath = sourcePath;
            string targetFilePath = Path.Combine(targetFolderPath, relativePath);

            // Reconstruct target path for temporary prefabs
            if (needsTempPrefabCleanup && (assetPath == tempPrefabPath || assetPath == tempStripPrefabPath))
            {
                // Place scene object prefab directly in destination Assets/ folder
                targetFilePath = Path.Combine(targetFolderPath, "Assets", $"{sourcePrefab.name}.prefab");
            }
            else if (createdTempPrefabForStripping && assetPath == prefabPath)
            {
                targetFilePath = Path.Combine(targetFolderPath, prefabPath);
            }

            // Create directories if they don't exist
            string targetFileDir = Path.GetDirectoryName(targetFilePath);
            if (!Directory.Exists(targetFileDir))
            {
                Directory.CreateDirectory(targetFileDir);
            }

            // Copy asset file
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetFilePath, true);
                copiedCount++;

                // Copy .meta file
                if (copyMetaFiles)
                {
                    string metaSourcePath = sourcePath + ".meta";
                    string metaTargetPath = targetFilePath + ".meta";
                    if (File.Exists(metaSourcePath))
                    {
                        File.Copy(metaSourcePath, metaTargetPath, true);
                    }
                }
            }
        }

        // 4. Clean up temporary files
        if (needsTempPrefabCleanup)
        {
            AssetDatabase.DeleteAsset(tempPrefabPath);
        }
        if (createdTempPrefabForStripping)
        {
            AssetDatabase.DeleteAsset(tempStripPrefabPath);
        }

        if (Directory.Exists("Assets/TempExporter") && Directory.GetFiles("Assets/TempExporter").Length == 0)
        {
            Directory.Delete("Assets/TempExporter");
        }
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Export Complete", 
            $"Successfully exported {copiedCount} assets (including prefab) to:\n{targetFolderPath}\n\nAll custom C# scripts were excluded and stripped.", 
            "OK");
    }
}
