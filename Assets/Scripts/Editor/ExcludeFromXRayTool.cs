using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ExcludeFromXRayTool : EditorWindow
{
    private GameObject _targetModel;
    private GameObject _childObject;
    private XRayExclusionMode _exclusionMode = XRayExclusionMode.KeepOriginalMaterial;

    [MenuItem("Tools/Configure X-Ray Exclusions")]
    public static void Open() => GetWindow<ExcludeFromXRayTool>("X-Ray Exclusions");

    void OnGUI()
    {
        GUILayout.Label("X-Ray Exclusions Configurator", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _targetModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine Model / Prefab", _targetModel, typeof(GameObject), true);

        if (_targetModel == null)
        {
            EditorGUILayout.HelpBox("Drag the main engine model/prefab here.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        GUILayout.Label("Add / Configure Exclusion", EditorStyles.boldLabel);

        _childObject = (GameObject)EditorGUILayout.ObjectField(
            "Child GameObject to Exclude", _childObject, typeof(GameObject), true);

        if (_childObject != null)
        {
            // Verify if child is actually a child of the target model
            if (!IsChildOf(_childObject.transform, _targetModel.transform))
            {
                EditorGUILayout.HelpBox("Selected object is not a child of the main Engine Model!", MessageType.Error);
            }
            else
            {
                _exclusionMode = (XRayExclusionMode)EditorGUILayout.EnumPopup("Exclusion Mode", _exclusionMode);

                EditorGUILayout.Space(6);
                if (GUILayout.Button("Apply X-Ray Exclusion", GUILayout.Height(36)))
                {
                    ApplyExclusion(true);
                }
            }
        }

        EditorGUILayout.Space(16);
        GUILayout.Label("Current Configured Exclusions in Prefab/Model", EditorStyles.boldLabel);

        List<ExclusionInfo> exclusions = GetExclusionsInPrefab(_targetModel);
        if (exclusions.Count == 0)
        {
            EditorGUILayout.HelpBox("No X-Ray exclusions configured for this model yet.", MessageType.None);
        }
        else
        {
            EditorGUILayout.BeginVertical("box");
            foreach (var info in exclusions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{info.relativePath} ({info.mode})", EditorStyles.miniLabel);

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    RemoveExclusionByPath(info.relativePath);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
    }

    private struct ExclusionInfo
    {
        public string relativePath;
        public XRayExclusionMode mode;
    }

    private bool IsChildOf(Transform child, Transform potentialParent)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == potentialParent) return true;
            current = current.parent;
        }
        return false;
    }

    private string GetRelativePath(Transform root, Transform child)
    {
        if (child == root) return "";
        string path = child.name;
        Transform parent = child.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private List<ExclusionInfo> GetExclusionsInPrefab(GameObject target)
    {
        var list = new List<ExclusionInfo>();
        if (target == null) return list;

        string prefabPath = AssetDatabase.GetAssetPath(target);
        if (!string.IsNullOrEmpty(prefabPath))
        {
            // Read components inside the prefab asset safely
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var exclusions = scope.prefabContentsRoot.GetComponentsInChildren<ExcludeFromXRay>(true);
                foreach (var ex in exclusions)
                {
                    list.Add(new ExclusionInfo
                    {
                        relativePath = GetRelativePath(scope.prefabContentsRoot.transform, ex.transform),
                        mode = ex.mode
                    });
                }
            }
        }
        else
        {
            // Scene GameObject
            var exclusions = target.GetComponentsInChildren<ExcludeFromXRay>(true);
            foreach (var ex in exclusions)
            {
                list.Add(new ExclusionInfo
                {
                    relativePath = GetRelativePath(target.transform, ex.transform),
                    mode = ex.mode
                });
            }
        }
        return list;
    }

    private void ApplyExclusion(bool add)
    {
        if (_targetModel == null || _childObject == null) return;

        string prefabPath = AssetDatabase.GetAssetPath(_targetModel);
        bool isPrefabAsset = !string.IsNullOrEmpty(prefabPath);

        string relativePath = GetRelativePath(_targetModel.transform, _childObject.transform);

        if (isPrefabAsset)
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                Transform targetTransform = string.IsNullOrEmpty(relativePath) ? root.transform : root.transform.Find(relativePath);

                if (targetTransform == null)
                {
                    EditorUtility.DisplayDialog("Error", "Could not locate child object inside prefab hierarchy.", "OK");
                    return;
                }

                var comp = targetTransform.GetComponent<ExcludeFromXRay>();
                if (add)
                {
                    if (comp == null) comp = targetTransform.gameObject.AddComponent<ExcludeFromXRay>();
                    comp.mode = _exclusionMode;
                }
                else
                {
                    if (comp != null) DestroyImmediate(comp, true);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            // Scene GameObject
            Undo.RecordObject(_childObject, "Configure X-Ray Exclusion");
            var comp = _childObject.GetComponent<ExcludeFromXRay>();

            if (add)
            {
                if (comp == null) comp = Undo.AddComponent<ExcludeFromXRay>(_childObject);
                comp.mode = _exclusionMode;
                EditorUtility.SetDirty(_childObject);
            }
            else
            {
                if (comp != null) Undo.DestroyObjectImmediate(comp);
            }
        }

        EditorUtility.DisplayDialog("Success ✓", $"Applied X-Ray exclusion to '{_childObject.name}'.", "OK");
        _childObject = null;
    }

    private void RemoveExclusionByPath(string relativePath)
    {
        if (_targetModel == null) return;

        string prefabPath = AssetDatabase.GetAssetPath(_targetModel);
        bool isPrefabAsset = !string.IsNullOrEmpty(prefabPath);

        if (isPrefabAsset)
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                Transform targetTransform = string.IsNullOrEmpty(relativePath) ? root.transform : root.transform.Find(relativePath);

                if (targetTransform != null)
                {
                    var comp = targetTransform.GetComponent<ExcludeFromXRay>();
                    if (comp != null) DestroyImmediate(comp, true);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            Transform targetTransform = string.IsNullOrEmpty(relativePath) ? _targetModel.transform : _targetModel.transform.Find(relativePath);
            if (targetTransform != null)
            {
                var comp = targetTransform.GetComponent<ExcludeFromXRay>();
                if (comp != null) Undo.DestroyObjectImmediate(comp);
            }
        }

        EditorUtility.DisplayDialog("Success ✓", "Removed X-Ray exclusion.", "OK");
    }
}
