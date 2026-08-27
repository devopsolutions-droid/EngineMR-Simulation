using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class PopulateTutorialDisplayTool : EditorWindow
{
    [MenuItem("Tools/Populate Tutorial Display")]
    public static void RunPopulate()
    {
        // 1. Find TutorialPlayerDisplay in the active scene
        TutorialPlayerDisplay tutorialDisplay = FindFirstObjectByType<TutorialPlayerDisplay>(FindObjectsInactive.Include);
        if (tutorialDisplay == null)
        {
            Debug.LogError("[PopulateTool] Could not find TutorialPlayerDisplay in the active scene!");
            return;
        }

        // 2. Load EngineRegistry
        string registryPath = "Assets/ScriptableObjects/Data/EngineRegistry.asset";
        EngineRegistry registry = AssetDatabase.LoadAssetAtPath<EngineRegistry>(registryPath);
        if (registry == null)
        {
            Debug.LogError($"[PopulateTool] Could not find EngineRegistry at {registryPath}!");
            return;
        }

        if (tutorialDisplay.engineContents == null)
            tutorialDisplay.engineContents = new List<TutorialPlayerDisplay.EngineTutorialContent>();

        int addedCount = 0;

        // 3. Loop through registry engines and add missing ones
        foreach (EngineData engine in registry.engines)
        {
            if (engine == null) continue;

            bool found = false;
            foreach (var content in tutorialDisplay.engineContents)
            {
                if (content.engineData == engine)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                tutorialDisplay.engineContents.Add(new TutorialPlayerDisplay.EngineTutorialContent
                {
                    engineData = engine,
                    customEngineName = engine.engineName,
                    learningObjectives = ""
                });
                addedCount++;
                Debug.Log($"[PopulateTool] Added {engine.engineName} to TutorialPlayerDisplay.");
            }
        }

        // 4. Save changes
        if (addedCount > 0)
        {
            EditorUtility.SetDirty(tutorialDisplay);
            EditorSceneManager.MarkSceneDirty(tutorialDisplay.gameObject.scene);
            EditorUtility.DisplayDialog("Success", $"Successfully added {addedCount} engines to the TutorialPlayerDisplay in the scene!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Up to date", "All engines in the registry are already present in the TutorialPlayerDisplay.", "OK");
        }
    }
}
