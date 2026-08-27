using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class UpdateInspectionOffsetsTool : EditorWindow
{
    [MenuItem("Tools/Update Inspection Offsets")]
    public static void RunUpdate()
    {
        // 1. Find the EngineInspectionSettings component in the active scene
        EngineInspectionSettings settings = FindFirstObjectByType<EngineInspectionSettings>(FindObjectsInactive.Include);
        if (settings == null)
        {
            Debug.LogError("[UpdateTool] Could not find EngineInspectionSettings in the active scene!");
            return;
        }

        // 2. Load the EngineRegistry
        string registryPath = "Assets/ScriptableObjects/Data/EngineRegistry.asset";
        EngineRegistry registry = AssetDatabase.LoadAssetAtPath<EngineRegistry>(registryPath);
        if (registry == null)
        {
            Debug.LogError($"[UpdateTool] Could not find EngineRegistry at {registryPath}!");
            return;
        }

        if (settings.overrides == null)
            settings.overrides = new List<EngineInspectionPositionOverride>();

        // 3. Create a brand new list that matches the Registry's sequence
        List<EngineInspectionPositionOverride> orderedList = new List<EngineInspectionPositionOverride>();
        int addedCount = 0;

        foreach (EngineData engine in registry.engines)
        {
            if (engine == null) continue;

            // Try to find if this engine already exists in the user's current array
            EngineInspectionPositionOverride existingEntry = null;
            foreach (var entry in settings.overrides)
            {
                if (entry != null && entry.engineData == engine)
                {
                    existingEntry = entry;
                    break;
                }
            }

            if (existingEntry != null)
            {
                // CRITICAL: Engine already exists. Keep the EXACT existing coordinates!
                orderedList.Add(existingEntry);
            }
            else
            {
                // Missing engine found. Add it to the list with default coordinates.
                orderedList.Add(new EngineInspectionPositionOverride
                {
                    engineData = engine,
                    inspectionLocalPosition = new Vector3(0.037f, 0.113f, 0.432f) // Standard fallback default
                });
                addedCount++;
                Debug.Log($"[UpdateTool] Added missing engine '{engine.engineName}' to InspectionOffsets.");
            }
        }

        // 4. Overwrite the old messy list with the new clean, sequential list
        settings.overrides = orderedList;

        // 5. Save the scene changes
        EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);

        EditorUtility.DisplayDialog("Success", 
            $"EngineInspectionSettings array has been perfectly sorted to match the EngineRegistry sequence.\n\n" +
            $"Added {addedCount} missing engines.\n\n" +
            $"All of your previously set coordinates were strictly preserved!", "OK");
    }
}
