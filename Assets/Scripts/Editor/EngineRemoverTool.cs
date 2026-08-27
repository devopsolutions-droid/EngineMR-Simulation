using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tools > Engine Remover
///
/// Intelligently removes ALL engine-related assets and scene registrations:
///   • Removes EngineData from EngineRegistry
///   • Removes entry from TutorialPlayerDisplay (scene component)
///   • Removes entry from EngineInspectionSettings (scene component)
///   • Removes entry from EngineSceneLoader.engineEntries (scene component)
///   • Deletes the entire ScriptableObjects/Data/Engines/<Name>/ folder
///     (PartData assets, HoverPanel prefabs, EngineData, Manifest)
///   • Searches and deletes ALL matching prefabs (regular + dismantled)
///   • Searches and deletes ALL matching GLB files across the project
///   • Deletes engine-specific subfolders (e.g., "F6 Engine extras")
///   • Provides detailed preview before deletion
/// </summary>
public class EngineRemoverTool : EditorWindow
{
    private EngineData     _engineData;
    private EngineRegistry _registry;
    private bool           _deleteAllPrefabs = true;
    private bool           _deleteAllGlbs    = true;
    private string         _status           = "";
    private Vector2        _scrollPos;

    // Search results cache
    private List<string> _foundPrefabs;
    private List<string> _foundGlbs;
    private List<string> _foundFolders;

    [MenuItem("Tools/Engine Remover")]
    public static void Open() => GetWindow<EngineRemoverTool>("Engine Remover");

    void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        GUILayout.Label("Smart Engine Remover", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _engineData = (EngineData)EditorGUILayout.ObjectField(
            "Engine Data Asset", _engineData, typeof(EngineData), false);

        _registry = (EngineRegistry)EditorGUILayout.ObjectField(
            "Engine Registry", _registry, typeof(EngineRegistry), false);

        EditorGUILayout.Space(4);
        _deleteAllPrefabs = EditorGUILayout.Toggle("Delete All Matching Prefabs", _deleteAllPrefabs);
        _deleteAllGlbs    = EditorGUILayout.Toggle("Delete All Matching GLBs",    _deleteAllGlbs);

        EditorGUILayout.Space(6);

        // Preview what will be deleted
        if (_engineData != null)
        {
            // Scan for all matching assets
            ScanForAssets();

            string engineFolder = GetEngineFolder(_engineData);

            EditorGUILayout.HelpBox(
                $"Engine: {_engineData.engineName}\n" +
                $"Prefab Reference: {(_engineData.enginePrefab != null ? _engineData.enginePrefab.name : "none")}\n\n" +
                "The following will be PERMANENTLY DELETED:",
                MessageType.Warning);

            EditorGUILayout.Space(2);

            // Registry
            EditorGUILayout.LabelField("Registry Entry:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  • {_engineData.engineName} (from EngineRegistry)");

            EditorGUILayout.Space(2);

            // Data folder
            EditorGUILayout.LabelField("Data Folder:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  • {engineFolder ?? "not found"}");

            EditorGUILayout.Space(2);

            // Prefabs
            if (_deleteAllPrefabs && _foundPrefabs != null && _foundPrefabs.Count > 0)
            {
                EditorGUILayout.LabelField($"Prefabs ({_foundPrefabs.Count}):", EditorStyles.boldLabel);
                foreach (var p in _foundPrefabs)
                    EditorGUILayout.LabelField($"  • {p}");
            }
            else if (_deleteAllPrefabs)
            {
                EditorGUILayout.LabelField("Prefabs:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("  • None found");
            }

            EditorGUILayout.Space(2);

            // GLBs
            if (_deleteAllGlbs && _foundGlbs != null && _foundGlbs.Count > 0)
            {
                EditorGUILayout.LabelField($"GLB Files ({_foundGlbs.Count}):", EditorStyles.boldLabel);
                foreach (var g in _foundGlbs)
                    EditorGUILayout.LabelField($"  • {g}");
            }
            else if (_deleteAllGlbs)
            {
                EditorGUILayout.LabelField("GLB Files:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("  • None found");
            }

            EditorGUILayout.Space(2);

            // Engine-specific folders
            if (_foundFolders != null && _foundFolders.Count > 0)
            {
                EditorGUILayout.LabelField($"Engine-Specific Folders ({_foundFolders.Count}):", EditorStyles.boldLabel);
                foreach (var f in _foundFolders)
                    EditorGUILayout.LabelField($"  • {f}");
            }
        }

        EditorGUILayout.Space(6);

        bool canRun = _engineData != null && _registry != null;
        GUI.enabled = canRun;

        if (GUILayout.Button("REMOVE ENGINE  (irreversible)", GUILayout.Height(40)))
        {
            int totalAssets = 1 + // registry
                              (_foundPrefabs?.Count ?? 0) +
                              (_foundGlbs?.Count ?? 0) +
                              (_foundFolders?.Count ?? 0) +
                              1; // data folder

            if (EditorUtility.DisplayDialog("Confirm Removal",
                $"This will permanently delete {totalAssets} assets for:\n\n\"{_engineData.engineName}\"\n\n" +
                "This cannot be undone. Are you sure?",
                "Delete Everything", "Cancel"))
            {
                RunRemoval();
            }
        }

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "After running, manually:\n" +
            "  1. Open the Main Scene\n" +
            "  2. Delete the engine root GameObject(s) from the Hierarchy\n" +
            "  3. Delete the dismantled engine GameObject (if it exists)\n" +
            "  (Scene registry entries are removed automatically)",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────

    void ScanForAssets()
    {
        if (_engineData == null) return;

        string engineName = _engineData.engineName;
        string prefabName = _engineData.enginePrefab != null ? _engineData.enginePrefab.name : null;

        // Generate search patterns
        var searchPatterns = new List<string>();
        searchPatterns.Add(engineName.Replace(" ", ""));
        searchPatterns.Add(engineName.Replace(" ", "_"));
        searchPatterns.Add(engineName);
        if (prefabName != null)
        {
            searchPatterns.Add(prefabName.Replace(" ", ""));
            searchPatterns.Add(prefabName.Replace(" ", "_"));
            searchPatterns.Add(prefabName);
        }
        searchPatterns = searchPatterns.Distinct().ToList();

        // Search for prefabs
        _foundPrefabs = new List<string>();
        if (_deleteAllPrefabs)
        {
            string[] prefabFolders = { "Assets/Prefabs", "Assets/VRRoom", "Assets" };
            foreach (var folder in prefabFolders)
            {
                if (!Directory.Exists(folder)) continue;
                _foundPrefabs.AddRange(FindMatchingAssets(folder, ".prefab", searchPatterns));
            }
        }

        // Search for GLBs
        _foundGlbs = new List<string>();
        if (_deleteAllGlbs)
        {
            string[] glbFolders = { "Assets/All Engines", "Assets/Prefabs/Engine Prefabs", "Assets/VRRoom" };
            foreach (var folder in glbFolders)
            {
                if (!Directory.Exists(folder)) continue;
                _foundGlbs.AddRange(FindMatchingAssets(folder, ".glb", searchPatterns));
            }
        }

        // Search for engine-specific folders (e.g., "F6 Engine extras")
        _foundFolders = new List<string>();
        string[] searchFolders = { "Assets/Prefabs/Engine Prefabs", "Assets/VRRoom" };
        foreach (var folder in searchFolders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var dir in Directory.GetDirectories(folder))
            {
                string dirName = Path.GetFileName(dir);
                foreach (var pattern in searchPatterns)
                {
                    if (dirName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string unityPath = dir.Replace("\\", "/");
                        if (!_foundFolders.Contains(unityPath))
                            _foundFolders.Add(unityPath);
                        break;
                    }
                }
            }
        }
    }

    List<string> FindMatchingAssets(string folder, string extension, List<string> patterns)
    {
        var results = new List<string>();
        if (!Directory.Exists(folder)) return results;

        string[] files = Directory.GetFiles(folder, $"*{extension}", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            foreach (var pattern in patterns)
            {
                if (fileName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string unityPath = file.Replace("\\", "/");
                    if (!results.Contains(unityPath))
                        results.Add(unityPath);
                    break;
                }
            }
        }
        return results;
    }

    void RunRemoval()
    {
        string engineName = _engineData.engineName;
        var    log        = new System.Text.StringBuilder();
        log.AppendLine($"[EngineRemover] Starting smart removal of: {engineName}");
        log.AppendLine();

        int deletedCount = 0;

        // ── Step 1: Remove from EngineRegistry ───────────────────────────────
        if (_registry.engines.Contains(_engineData))
        {
            _registry.engines.Remove(_engineData);
            EditorUtility.SetDirty(_registry);
            log.AppendLine("✓ Removed from EngineRegistry");
            deletedCount++;
        }
        else
        {
            log.AppendLine("! EngineData was not found in EngineRegistry (skipped)");
        }

        // ── Step 2: Remove from TutorialPlayerDisplay ───────────────────────────
        TutorialPlayerDisplay tutorialDisplay = FindFirstObjectByType<TutorialPlayerDisplay>(FindObjectsInactive.Include);
        if (tutorialDisplay != null && tutorialDisplay.engineContents != null)
        {
            int removed = tutorialDisplay.engineContents.RemoveAll(c => c.engineData == _engineData);
            if (removed > 0)
            {
                EditorUtility.SetDirty(tutorialDisplay);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorialDisplay.gameObject.scene);
                log.AppendLine($"✓ Removed from TutorialPlayerDisplay ({removed} entry)");
                deletedCount++;
            }
            else
            {
                log.AppendLine("! Not found in TutorialPlayerDisplay (skipped)");
            }
        }
        else
        {
            log.AppendLine("! TutorialPlayerDisplay not found in scene (skipped)");
        }

        // ── Step 3: Remove from EngineInspectionSettings ────────────────────────
        EngineInspectionSettings inspectionSettings = FindFirstObjectByType<EngineInspectionSettings>(FindObjectsInactive.Include);
        if (inspectionSettings != null && inspectionSettings.overrides != null)
        {
            int removed = inspectionSettings.overrides.RemoveAll(o => o != null && o.engineData == _engineData);
            if (removed > 0)
            {
                EditorUtility.SetDirty(inspectionSettings);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inspectionSettings.gameObject.scene);
                log.AppendLine($"✓ Removed from EngineInspectionSettings ({removed} entry)");
                deletedCount++;
            }
            else
            {
                log.AppendLine("! Not found in EngineInspectionSettings (skipped)");
            }
        }
        else
        {
            log.AppendLine("! EngineInspectionSettings not found in scene (skipped)");
        }

        // ── Step 4: Remove from EngineSceneLoader ───────────────────────────────
        EngineSceneLoader sceneLoader = FindFirstObjectByType<EngineSceneLoader>(FindObjectsInactive.Include);
        if (sceneLoader != null && sceneLoader.engineEntries != null)
        {
            var entries = new List<EngineSceneEntry>(sceneLoader.engineEntries);
            int removed = entries.RemoveAll(e => e != null && e.engineData == _engineData);
            if (removed > 0)
            {
                sceneLoader.engineEntries = entries.ToArray();
                EditorUtility.SetDirty(sceneLoader);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneLoader.gameObject.scene);
                log.AppendLine($"✓ Removed from EngineSceneLoader.engineEntries ({removed} entry)");
                deletedCount++;
            }
            else
            {
                log.AppendLine("! Not found in EngineSceneLoader.engineEntries (skipped)");
            }
        }
        else
        {
            log.AppendLine("! EngineSceneLoader not found in scene (skipped)");
        }

        // ── Step 5: Delete all matching prefabs ────────────────────────────────
        if (_deleteAllPrefabs && _foundPrefabs != null && _foundPrefabs.Count > 0)
        {
            log.AppendLine();
            log.AppendLine($"Deleting {_foundPrefabs.Count} prefab(s):");
            foreach (var prefabPath in _foundPrefabs)
            {
                if (AssetDatabase.DeleteAsset(prefabPath))
                {
                    log.AppendLine($"  ✓ {prefabPath}");
                    deletedCount++;
                }
                else
                {
                    log.AppendLine($"  ✗ Failed: {prefabPath}");
                }
            }
        }

        // ── Step 6: Delete all matching GLBs ──────────────────────────────────
        if (_deleteAllGlbs && _foundGlbs != null && _foundGlbs.Count > 0)
        {
            log.AppendLine();
            log.AppendLine($"Deleting {_foundGlbs.Count} GLB file(s):");
            foreach (var glbPath in _foundGlbs)
            {
                if (AssetDatabase.DeleteAsset(glbPath))
                {
                    log.AppendLine($"  ✓ {glbPath}");
                    deletedCount++;
                }
                else
                {
                    log.AppendLine($"  ✗ Failed: {glbPath}");
                }
            }
        }

        // ── Step 7: Delete engine-specific folders ──────────────────────────────
        if (_foundFolders != null && _foundFolders.Count > 0)
        {
            log.AppendLine();
            log.AppendLine($"Deleting {_foundFolders.Count} engine-specific folder(s):");
            foreach (var folderPath in _foundFolders)
            {
                if (AssetDatabase.DeleteAsset(folderPath))
                {
                    log.AppendLine($"  ✓ {folderPath}");
                    deletedCount++;
                }
                else
                {
                    // Fallback
                    FileUtil.DeleteFileOrDirectory(folderPath);
                    FileUtil.DeleteFileOrDirectory(folderPath + ".meta");
                    log.AppendLine($"  ✓ {folderPath} (via FileUtil)");
                    deletedCount++;
                }
            }
        }

        // ── Step 8: Delete entire engine data folder ───────────────────────────
        string engineFolder = GetEngineFolder(_engineData);
        if (!string.IsNullOrEmpty(engineFolder) && Directory.Exists(engineFolder))
        {
            log.AppendLine();
            log.AppendLine("Deleting engine data folder:");
            string unityPath = engineFolder.Replace("\\", "/");
            if (unityPath.StartsWith(Application.dataPath.Replace("\\", "/")))
                unityPath = "Assets" + unityPath.Substring(Application.dataPath.Length);

            if (AssetDatabase.DeleteAsset(unityPath))
            {
                log.AppendLine($"  ✓ {unityPath}");
                deletedCount++;
            }
            else
            {
                FileUtil.DeleteFileOrDirectory(engineFolder);
                FileUtil.DeleteFileOrDirectory(engineFolder + ".meta");
                log.AppendLine($"  ✓ {engineFolder} (via FileUtil)");
                deletedCount++;
            }
        }
        else
        {
            log.AppendLine();
            log.AppendLine($"! Data folder not found: {engineFolder ?? "null"}");
        }

        // ── Step 9: Save and refresh ──────────────────────────────────────────
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Step 10: Summary ───────────────────────────────────────────────────
        log.AppendLine();
        log.AppendLine("═══════════════════════════════════════════════════════");
        log.AppendLine($"✓ REMOVAL COMPLETE: {deletedCount} assets deleted");
        log.AppendLine("═══════════════════════════════════════════════════════");
        log.AppendLine();
        log.AppendLine("MANUAL STEPS STILL REQUIRED:");
        log.AppendLine($"  1. Open the Main Scene");
        log.AppendLine($"  2. Find and DELETE the '{engineName}' root GameObject in the hierarchy");
        log.AppendLine($"  3. Find and DELETE the '{engineName} Dismantled' GameObject (if exists)");

        Debug.Log(log.ToString());

        _status = $"✓ Complete — {deletedCount} assets deleted.\n\nSee Console for manual scene steps.";
        _engineData = null;
        _foundPrefabs = null;
        _foundGlbs = null;
        _foundFolders = null;
        Repaint();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the engine's data folder by looking at the EngineData asset's own path.
    /// e.g. Assets/ScriptableObjects/Data/Engines/F6BoxerEngine/F6BoxerEngineData.asset
    ///   → Assets/ScriptableObjects/Data/Engines/F6BoxerEngine
    /// </summary>
    static string GetEngineFolder(EngineData data)
    {
        string assetPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(assetPath)) return null;
        return Path.GetDirectoryName(assetPath).Replace("\\", "/");
    }
}
