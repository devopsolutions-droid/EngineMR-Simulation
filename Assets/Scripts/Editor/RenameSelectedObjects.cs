using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public class RenameSelectedObjects : EditorWindow
{
    private string _prefix = "Dethatch";
    private int _startIndex = 1;
    private bool _useZeroPadding = true;

    [MenuItem("Tools/Rename Selected...")]
    public static void Open()
    {
        var window = GetWindow<RenameSelectedObjects>("Rename Selected");
        window.minSize = new Vector2(300, 160);
        window.maxSize = new Vector2(350, 170);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        GUILayout.Label("Rename Selected Objects", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
        EditorGUILayout.HelpBox($"{selectedCount} objects selected.", MessageType.Info);

        _prefix = EditorGUILayout.TextField("New Name Prefix", _prefix);
        _startIndex = EditorGUILayout.IntField("Start Index", _startIndex);
        _useZeroPadding = EditorGUILayout.Toggle("Use Zero Padding (01, 02)", _useZeroPadding);

        EditorGUILayout.Space(10);

        GUI.enabled = selectedCount > 0 && !string.IsNullOrEmpty(_prefix);
        if (GUILayout.Button("⚡ Rename Objects", GUILayout.Height(30)))
        {
            Rename();
        }
        GUI.enabled = true;
    }

    private void Rename()
    {
        GameObject[] objs = Selection.gameObjects;
        if (objs == null || objs.Length == 0) return;

        // Traverse the active scenes recursively to build a true top-to-bottom hierarchy list
        List<GameObject> allSceneObjects = new List<GameObject>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    AddChildrenRecursively(root.transform, allSceneObjects);
                }
            }
        }

        // Sort by hierarchy layout order first, fallback to name order for assets
        var sortedObjs = objs
            .OrderBy(o => {
                int idx = allSceneObjects.IndexOf(o);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ThenBy(o => o.name)
            .ToArray();

        for (int i = 0; i < sortedObjs.Length; i++)
        {
            // Register complete undo for the GameObject before modifying its name
            Undo.RegisterCompleteObjectUndo(sortedObjs[i], "Rename Objects");
            
            int number = _startIndex + i;
            string format = "D";
            
            if (_useZeroPadding)
            {
                if (sortedObjs.Length >= 100)
                    format = "D3";
                else if (sortedObjs.Length >= 10)
                    format = "D2";
                else
                    format = "D2"; // Default to 2 digits (e.g. 01)
            }
            
            sortedObjs[i].name = _prefix + number.ToString(format);
            EditorUtility.SetDirty(sortedObjs[i]);
        }

        Debug.Log($"[RenameSelectedObjects] Successfully renamed {sortedObjs.Length} objects with prefix '{_prefix}'.");
        Close();
    }

    private void AddChildrenRecursively(Transform t, List<GameObject> list)
    {
        list.Add(t.gameObject);
        for (int i = 0; i < t.childCount; i++)
        {
            AddChildrenRecursively(t.GetChild(i), list);
        }
    }
}
