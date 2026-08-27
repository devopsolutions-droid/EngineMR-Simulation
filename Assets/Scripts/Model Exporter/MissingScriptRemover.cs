#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

public class MissingScriptRemover : EditorWindow
{
    private GameObject targetModel;

    [MenuItem("Tools/Missing Script Remover")]
    static void ShowWindow() => GetWindow<MissingScriptRemover>("Missing Script Remover");

    void OnGUI()
    {
        GUILayout.Label("Drop a model from the Hierarchy:", EditorStyles.boldLabel);
        targetModel = (GameObject)EditorGUILayout.ObjectField(targetModel, typeof(GameObject), true);

        GUI.enabled = targetModel != null;
        if (GUILayout.Button("Remove Missing Scripts"))
            RemoveMissingScripts(targetModel);
        GUI.enabled = true;
    }

    static void RemoveMissingScripts(GameObject root)
    {
        int total = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (removed > 0)
            {
                Debug.Log($"Removed {removed} missing script(s) from '{t.name}'");
                total += removed;
            }
        }
        Debug.Log($"Done. Total missing scripts removed: {total}");
    }
}

#endif
