using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Tools > Engine Part Setup
///
/// One-click pipeline. After running, the developer only needs to:
///   1. Open each PartData asset and fill in description + drag audio clip
///   2. Assign thumbnail in EngineData asset
///   3. Set spawnPosition / spawnRotation in EngineData asset
/// </summary>
public class EnginePartSetupTool : EditorWindow
{
    private const string EnginePartsLayerName = "EngineParts";

    private GameObject     _engineModel;
    private EngineRegistry _registry;
    private string         _engineName     = "New Engine";
    private string         _savePath       = "Assets/ScriptableObjects/Data/Engines";
    private bool           _addGrabController = true;

    // ── Category Dropdown ─────────────────────────────────────────────────────
    private static readonly string[] _categoryOptions = new string[]
    {
        "Engineering",
        // Add more categories here as needed
    };
    private int _selectedCategoryIndex = 0;
    private string EngineCategory => _categoryOptions[_selectedCategoryIndex];

    // ── Auto-load default registry on open ────────────────────────────────────
    private const string DefaultRegistryPath = "Assets/ScriptableObjects/Data/EngineRegistry.asset";

    void OnEnable()
    {
        if (_registry == null)
            _registry = AssetDatabase.LoadAssetAtPath<EngineRegistry>(DefaultRegistryPath);
    }

    [MenuItem("Tools/Engine Part Setup")]
    public static void Open() => GetWindow<EnginePartSetupTool>("Engine Part Setup");

    void OnGUI()
    {
        // Draw Custom Header
        Rect headerRect = EditorGUILayout.GetControlRect(false, 45);
        EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.15f));
        
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.normal.textColor = Color.white;
        titleStyle.fontSize = 14;
        titleStyle.padding = new RectOffset(10, 0, 5, 0);

        GUIStyle subTitleStyle = new GUIStyle(EditorStyles.label);
        subTitleStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        subTitleStyle.fontSize = 11;
        subTitleStyle.padding = new RectOffset(10, 0, -2, 0);

        GUILayout.BeginArea(headerRect);
        GUILayout.Label("ENGINE PART SETUP TOOL", titleStyle);
        GUILayout.Label("Create automated VR interactions for engine assemblies", subTitleStyle);
        GUILayout.EndArea();

        // Draw Accent Line
        Rect lineRect = new Rect(headerRect.x, headerRect.yMax, headerRect.width, 2);
        EditorGUI.DrawRect(lineRect, new Color(0.18f, 0.53f, 0.96f));

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("1. Core Engine Reference", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        _engineModel = (GameObject)EditorGUILayout.ObjectField("Engine Model / Prefab", _engineModel, typeof(GameObject), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("2. Display Information", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        _engineName = EditorGUILayout.TextField("Display Button Name", _engineName);
        _selectedCategoryIndex = EditorGUILayout.Popup("Category", _selectedCategoryIndex, _categoryOptions);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("3. Data & Registry", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        _registry = (EngineRegistry)EditorGUILayout.ObjectField("Engine Registry", _registry, typeof(EngineRegistry), false);
        if (_registry == null)
            EditorGUILayout.HelpBox("No EngineRegistry found! Place it at:\n" + DefaultRegistryPath, MessageType.Warning);
        _savePath = EditorGUILayout.TextField("Save Path", _savePath);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("4. Advanced Options", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        _addGrabController = EditorGUILayout.Toggle("Add Grab Controller to All Parts", _addGrabController);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(15);

        GUI.enabled = _engineModel != null;
        var oldColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f); // Nice soft green
        
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 14;
        btnStyle.fontStyle = FontStyle.Bold;
        
        if (GUILayout.Button("🚀 RUN FULL SETUP (One Click)", btnStyle, GUILayout.Height(45)))
        {
            RunFullSetup();
        }
        
        GUI.backgroundColor = oldColor;
        GUI.enabled = true;

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        
        // Left Column
        EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.55f));
        GUILayout.Label("ONE CLICK DOES EVERYTHING", EditorStyles.boldLabel);
        GUILayout.Label("• Applies 'EngineParts' layer to all meshes\n" +
                        "• Adds convex MeshColliders\n" +
                        "• Assigns EnginePart & Explode scripts\n" +
                        "• Sets RED outline on Visuals script\n" +
                        "• Optionally adds Grab Controllers\n" +
                        "• Generates PartData assets & Manifest\n" +
                        "• Creates new EngineData asset\n" +
                        "• Auto-registers into EngineRegistry\n" +
                        "• Auto-links TutorialPlayerDisplay\n" +
                        "• Auto-links Inspection Settings\n" +
                        "• Auto-links EngineSceneLoader", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();

        // Right Column
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("AFTER RUNNING", EditorStyles.boldLabel);
        GUILayout.Label("1. Open Parts/ folder\n" +
                        "2. Fill in description for each part\n" +
                        "3. Drop in voiceover audio\n" +
                        "4. Assign thumbnail in EngineData\n" +
                        "5. Set final spawn Pos/Rot", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────

    void RunFullSetup()
    {
        if (_engineModel == null) return;

        // ── Ensure EngineParts layer exists ───────────────────────────────────
        int enginePartsLayer = EnsureLayer(EnginePartsLayerName);
        if (enginePartsLayer < 0)
        {
            EditorUtility.DisplayDialog("Layer Error",
                $"Could not create or find layer '{EnginePartsLayerName}'.\n" +
                "Please add it manually in Edit > Project Settings > Tags and Layers, then re-run.",
                "OK");
            return;
        }

        string engineFolder = $"{_savePath}/{_engineName.Replace(" ", "")}";
        string partsFolder  = $"{engineFolder}/Parts";

        EnsureFolder(_savePath);
        EnsureFolder(engineFolder);
        EnsureFolder(partsFolder);

        var manifest        = ScriptableObject.CreateInstance<EnginePartManifest>();
        int addedParts      = 0;
        int createdPartData = 0;

        var palette = new OutlineColorPreset[]
        {
            OutlineColorPreset.Cyan,
            OutlineColorPreset.Orange,
            OutlineColorPreset.LightGreen,
            OutlineColorPreset.Yellow,
            OutlineColorPreset.Pink,
            OutlineColorPreset.Green,
            OutlineColorPreset.Purple,
            OutlineColorPreset.Red,
            OutlineColorPreset.Blue,
            OutlineColorPreset.White
        };
        int paletteIndex = 0;

        bool   isPrefabAsset   = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_engineModel));
        string prefabAssetPath = AssetDatabase.GetAssetPath(_engineModel);
        bool   canEditPrefab   = !string.IsNullOrEmpty(prefabAssetPath);

        PrefabUtility.EditPrefabContentsScope? scope = canEditPrefab
            ? new PrefabUtility.EditPrefabContentsScope(prefabAssetPath)
            : (PrefabUtility.EditPrefabContentsScope?)null;

        try
        {
            GameObject prefabRoot  = canEditPrefab ? scope.Value.prefabContentsRoot : _engineModel;
            var        scopedChildren = CollectMeshChildren(prefabRoot);

            // ── EnginePart + layer + PartData ─────────────────────────────────
            foreach (var go in scopedChildren)
            {
                go.layer = enginePartsLayer;

                var mc = go.GetComponent<MeshCollider>();
                if (mc == null) mc = go.AddComponent<MeshCollider>();
                mc.convex = true;

                var ep = go.GetComponent<EnginePart>();
                if (ep == null)
                {
                    ep = go.AddComponent<EnginePart>();
                    ep.partName = go.name;
                    addedParts++;
                }

                // Ensure sibling components exist
                if (go.GetComponent<EnginePartVisuals>() == null)
                {
                    var vis = go.AddComponent<EnginePartVisuals>();
                    vis.outlineColorPreset = OutlineColorPreset.Red;
                    vis.outlineWidth       = 3.5f;
                }
                if (go.GetComponent<EnginePartExplode>() == null)
                    go.AddComponent<EnginePartExplode>();
                paletteIndex++;

                string safeName  = SanitizeName(go.name);
                string assetPath = $"{partsFolder}/{safeName}.asset";

                PartData pd;
                if (File.Exists(assetPath))
                    pd = AssetDatabase.LoadAssetAtPath<PartData>(assetPath);
                else
                {
                    pd = ScriptableObject.CreateInstance<PartData>();
                    pd.partName    = go.name;
                    pd.description = $"Description for {go.name}.";
                    AssetDatabase.CreateAsset(pd, assetPath);
                    createdPartData++;
                }

                ep.partData = pd;

                manifest.parts.Add(new EnginePartManifest.PartEntry
                {
                    gameObjectName = go.name,
                    partData       = pd
                });
            }

            Debug.Log($"[Setup] {addedParts} EnginePart components added, {createdPartData} PartData assets created.");

            // ── Add Grab Controller (optional) ─────────────────────────────────
            int addedGrabControllers = 0;
            if (_addGrabController)
            {
                foreach (var go in scopedChildren)
                {
                    if (go == null) continue;

                    // Remove stale Rigidbody — it causes whole-engine grab
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        DestroyImmediate(rb);
                        Debug.Log($"[Setup] Removed Rigidbody from '{go.name}' (would cause whole-engine grab).");
                    }

                    // Remove stale XRGrabInteractable for the same reason
                    var xrGrab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
                    if (xrGrab != null)
                    {
                        DestroyImmediate(xrGrab);
                        Debug.Log($"[Setup] Removed XRGrabInteractable from '{go.name}'.");
                    }

                    // Add EnginePartGrabController
                    var grab = go.GetComponent<EnginePartGrabController>();
                    if (grab == null)
                    {
                        go.AddComponent<EnginePartGrabController>();
                        addedGrabControllers++;
                    }
                }
                Debug.Log($"[Setup] {addedGrabControllers} EnginePartGrabController components added.");
            }

            // ── Save manifest ─────────────────────────────────────────────────
            string manifestPath = $"{engineFolder}/{_engineName.Replace(" ", "")}Manifest.asset";
            EnginePartManifest existingManifest = AssetDatabase.LoadAssetAtPath<EnginePartManifest>(manifestPath);
            if (existingManifest != null)
            {
                foreach (var newEntry in manifest.parts)
                {
                    bool found = false;
                    foreach (var e in existingManifest.parts)
                        if (e.gameObjectName == newEntry.gameObjectName) { found = true; break; }
                    if (!found) existingManifest.parts.Add(newEntry);
                }
                EditorUtility.SetDirty(existingManifest);
                manifest = existingManifest;
            }
            else
                AssetDatabase.CreateAsset(manifest, manifestPath);
        }
        finally
        {
            scope?.Dispose();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── EngineData asset ──────────────────────────────────────────────────
        string dataPath = $"{engineFolder}/{_engineName.Replace(" ", "")}Data.asset";
        EngineData engineData = AssetDatabase.LoadAssetAtPath<EngineData>(dataPath);
        if (engineData == null)
        {
            engineData = ScriptableObject.CreateInstance<EngineData>();
            AssetDatabase.CreateAsset(engineData, dataPath);
        }
        engineData.engineName     = _engineName;
        engineData.engineCategory = EngineCategory;
        engineData.partManifest   = manifest;
        if (!string.IsNullOrEmpty(prefabAssetPath))
            engineData.enginePrefab = _engineModel;
        EditorUtility.SetDirty(engineData);

        // ── Registry ──────────────────────────────────────────────────────────
        if (_registry != null && !_registry.engines.Contains(engineData))
        {
            _registry.engines.Add(engineData);
            EditorUtility.SetDirty(_registry);
        }

        // ── EngineInspectionConfig registration ──────────────────────────────
        string configPath = "Assets/ScriptableObjects/EngineInspectionConfig.asset";
        EngineInspectionConfig inspectionConfig = AssetDatabase.LoadAssetAtPath<EngineInspectionConfig>(configPath);
        if (inspectionConfig == null)
        {
            EnsureFolder("Assets/ScriptableObjects");
            inspectionConfig = ScriptableObject.CreateInstance<EngineInspectionConfig>();
            AssetDatabase.CreateAsset(inspectionConfig, configPath);
            Debug.Log($"[Setup] Created persistent EngineInspectionConfig asset at {configPath}");
        }

        if (inspectionConfig.engineConfigs == null)
        {
            inspectionConfig.engineConfigs = new List<EngineInspectionEntry>();
        }

        bool foundConfig = false;
        foreach (var entry in inspectionConfig.engineConfigs)
        {
            if (entry != null && entry.engineData == engineData)
            {
                foundConfig = true;
                break;
            }
        }

        if (!foundConfig)
        {
            var newEntry = new EngineInspectionEntry
            {
                engineData = engineData,
                inspectionLocalPosition = new Vector3(0.037f, 0.113f, 0.432f) // Default fallback values
            };
            inspectionConfig.engineConfigs.Add(newEntry);
            EditorUtility.SetDirty(inspectionConfig);
            Debug.Log($"[Setup] Registered engine '{_engineName}' with default inspection coordinates in EngineInspectionConfig.");
        }

        // ── TutorialPlayerDisplay registration ────────────────────────────────
        TutorialPlayerDisplay tutorialDisplay = FindFirstObjectByType<TutorialPlayerDisplay>();
        if (tutorialDisplay != null)
        {
            if (tutorialDisplay.engineContents == null) 
                tutorialDisplay.engineContents = new List<TutorialPlayerDisplay.EngineTutorialContent>();

            bool foundTutorial = false;
            foreach (var content in tutorialDisplay.engineContents)
            {
                if (content.engineData == engineData)
                {
                    foundTutorial = true;
                    break;
                }
            }

            if (!foundTutorial)
            {
                tutorialDisplay.engineContents.Add(new TutorialPlayerDisplay.EngineTutorialContent
                {
                    engineData = engineData,
                    customEngineName = _engineName,
                    learningObjectives = ""
                });
                EditorUtility.SetDirty(tutorialDisplay);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorialDisplay.gameObject.scene);
                Debug.Log($"[Setup] Registered engine '{_engineName}' in TutorialPlayerDisplay.");
            }
        }

        // ── EngineInspectionSettings (Scene Component) registration ───────────
        EngineInspectionSettings inspectionSettings = FindFirstObjectByType<EngineInspectionSettings>(FindObjectsInactive.Include);
        if (inspectionSettings != null)
        {
            if (inspectionSettings.overrides == null)
                inspectionSettings.overrides = new List<EngineInspectionPositionOverride>();

            bool foundInspection = false;
            foreach (var entry in inspectionSettings.overrides)
            {
                if (entry != null && entry.engineData == engineData)
                {
                    foundInspection = true;
                    break;
                }
            }

            if (!foundInspection)
            {
                inspectionSettings.overrides.Add(new EngineInspectionPositionOverride
                {
                    engineData = engineData,
                    inspectionLocalPosition = Vector3.zero
                });
                EditorUtility.SetDirty(inspectionSettings);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inspectionSettings.gameObject.scene);
                Debug.Log($"[Setup] Registered engine '{_engineName}' in EngineInspectionSettings with empty (0,0,0) coordinates.");
            }
        }
        // ── EngineSceneLoader registration ────────────────────────────────────
        EngineSceneLoader sceneLoader = FindFirstObjectByType<EngineSceneLoader>(FindObjectsInactive.Include);
        if (sceneLoader != null)
        {
            if (sceneLoader.engineEntries == null)
                sceneLoader.engineEntries = new EngineSceneEntry[0];

            bool foundEntry = false;
            foreach (var entry in sceneLoader.engineEntries)
            {
                if (entry != null && entry.engineData == engineData)
                {
                    foundEntry = true;
                    break;
                }
            }

            if (!foundEntry)
            {
                var newEntries = new List<EngineSceneEntry>(sceneLoader.engineEntries);
                newEntries.Add(new EngineSceneEntry
                {
                    engineData = engineData,
                    sceneRoot = _engineModel
                });
                sceneLoader.engineEntries = newEntries.ToArray();

                EditorUtility.SetDirty(sceneLoader);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneLoader.gameObject.scene);
                Debug.Log($"[Setup] Registered engine '{_engineName}' in EngineSceneLoader (sceneRoot: {_engineModel.name}).");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (isPrefabAsset)
            PrefabUtility.SavePrefabAsset(_engineModel);

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = engineData;

        EditorUtility.DisplayDialog("Setup Complete ✓",
            $"Engine: {_engineName}\n\n" +
            $"  • Layer '{EnginePartsLayerName}' applied to all parts\n" +
            $"  • {addedParts} EnginePart + EnginePartVisuals + EnginePartExplode components assigned\n" +
            $"  • Outline set to solid RED (3.5px) on EnginePartVisuals\n" +
            $"  • {(_addGrabController ? "EnginePartGrabController added to all parts" : "Grab controller NOT added (disabled in options)")}\n" +
            $"  • {createdPartData} PartData assets created & wired\n" +
            $"  • EngineData asset ready\n\n" +
            "Remaining steps:\n" +
            "1. Open Parts/ folder → fill description + drag audio into each PartData\n" +
            "2. Assign thumbnail in EngineData\n" +
            "3. Set spawnPosition / spawnRotation in EngineData",
            "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");
        if (layersProp == null || !layersProp.isArray) return -1;

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var layerProp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[Setup] Created layer '{layerName}' at index {i}.");
                return i;
            }
        }
        return -1;
    }

    List<GameObject> CollectMeshChildren(GameObject root)
    {
        var result = new List<GameObject>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject == root) continue;
            if (!result.Contains(r.gameObject))
                result.Add(r.gameObject);
        }
        return result;
    }

    void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    string SanitizeName(string name) =>
        name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_")
            .Replace(":", "_").Replace("*", "_").Replace("?", "_")
            .Replace("\"", "_").Replace("<", "_").Replace(">", "_")
            .Replace("|", "_");
}
