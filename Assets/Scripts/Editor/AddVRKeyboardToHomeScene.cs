using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VisualKeyboard;

public class AddVRKeyboardToHomeScene : EditorWindow
{
    [MenuItem("Tools/Integrate VR Keyboard to Home Scene")]
    public static void IntegrateKeyboard()
    {
        // 1. Open the Home Scene
        string scenePath = "Assets/Scenes/EngineButtons HomeScene.unity";
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                Debug.LogWarning("[VRKeyboardIntegrator] Cancelled because active scene was not saved.");
                return;
            }
        }

        // 2. Find the VRSearchInput component directly in the active scene
        VRSearchInput searchInput = FindFirstObjectByType<VRSearchInput>();
        if (searchInput == null)
        {
            Debug.LogError("[VRKeyboardIntegrator] Could not find VRSearchInput component in the active scene!");
            EditorUtility.DisplayDialog("Error", "Could not find VRSearchInput component in the active scene.", "OK");
            return;
        }

        // Find the placeholder child (named 'Thumbnail' or similar) under the Search Button
        Transform thumbnailTransform = searchInput.transform.Find("Thumbnail");
        if (thumbnailTransform != null)
        {
            searchInput.placeholderObject = thumbnailTransform.gameObject;
            EditorUtility.SetDirty(searchInput);
        }

        // 3. Define the parent container (the parent of the search input, i.e. the outer 'Engine Buttons UI')
        GameObject parentGo = searchInput.transform.parent.gameObject;
        if (parentGo == null)
        {
            Debug.LogError("[VRKeyboardIntegrator] Could not find parent container of the Search Button!");
            EditorUtility.DisplayDialog("Error", "Could not find parent container of the Search Button.", "OK");
            return;
        }

        // Record Undo for scene modification
        Undo.RegisterFullObjectHierarchyUndo(parentGo, "Integrate VR Keyboard");

        // 4. Clean up any existing visual keyboard instance
        Transform existingKeyboard = parentGo.transform.Find("Visual Keyboard");
        if (existingKeyboard != null)
        {
            Debug.Log("[VRKeyboardIntegrator] Destroying existing 'Visual Keyboard' instance in scene.");
            DestroyImmediate(existingKeyboard.gameObject);
        }

        // 5. Load the Visual Keyboard prefab
        string prefabPath = "Assets/Visual Keyboard/Visual Keyboard.prefab";
        GameObject keyboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (keyboardPrefab == null)
        {
            // Fallback path in case it is in Prefabs subfolder
            prefabPath = "Assets/Visual Keyboard/Prefabs/Visual Keyboard.prefab";
            keyboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        if (keyboardPrefab == null)
        {
            Debug.LogError("[VRKeyboardIntegrator] Could not load Visual Keyboard prefab from Assets.");
            EditorUtility.DisplayDialog("Error", "Could not load Visual Keyboard prefab.", "OK");
            return;
        }

        // 6. Instantiate the prefab under parent
        GameObject keyboardInstance = PrefabUtility.InstantiatePrefab(keyboardPrefab, parentGo.transform) as GameObject;
        keyboardInstance.name = "Visual Keyboard";
        keyboardInstance.SetActive(false);
        
        // Position and scale the keyboard
        RectTransform rt = keyboardInstance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition3D = new Vector3(0.19f, -5.7f, -1.71f);
            rt.sizeDelta = new Vector2(1920f, 600f);
            rt.localRotation = Quaternion.Euler(37.05f, 0f, 0f);
            rt.localScale = new Vector3(0.007f, 0.007f, 0.007f);
        }

        // 7. Attach the bridge script to the Search Input GameObject (which is always active in the scene)
        // Clean up any old bridge scripts to prevent duplicates
        var oldBridgeOnSearch = searchInput.GetComponent<VRKeyboardSearchBridge>();
        if (oldBridgeOnSearch != null)
        {
            DestroyImmediate(oldBridgeOnSearch);
        }
        var oldBridgeOnKeyboard = keyboardInstance.GetComponent<VRKeyboardSearchBridge>();
        if (oldBridgeOnKeyboard != null)
        {
            DestroyImmediate(oldBridgeOnKeyboard);
        }

        VRKeyboardSearchBridge bridge = searchInput.gameObject.AddComponent<VRKeyboardSearchBridge>();
        bridge.keyboard = keyboardInstance.GetComponent<VisualKeyboard.VisualKeyboard>();
        bridge.searchInput = searchInput;
        bridge.autoToggleVisibility = true;
        bridge.keyboardPanel = keyboardInstance;

        // Force Editor utility dirty
        EditorUtility.SetDirty(keyboardInstance);
        EditorUtility.SetDirty(parentGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[VRKeyboardIntegrator] Successfully integrated VR Keyboard and VRKeyboardSearchBridge into the Home scene!");
        EditorUtility.DisplayDialog("Success", "VR Keyboard integrated successfully!\n\nReferences have been wired and positioned.", "OK");
    }
}
