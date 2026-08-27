using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Custom Inspector for EngineAssemblyConfig.
/// Adds 1-click hierarchy selection and reparenting buttons directly inside the Unity Inspector.
/// </summary>
[CustomEditor(typeof(EngineAssemblyConfig))]
public class EngineAssemblyConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EngineAssemblyConfig config = (EngineAssemblyConfig)target;

        // Big Action Buttons at the TOP of the Inspector
        if (config.partGroups != null && config.partGroups.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Quick Hierarchy Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("📁 Organize Hierarchy Nodes (1-Click)", GUILayout.Height(30)))
            {
                OrganizeHierarchyNodes(config);
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("🔍 Select All Grouped Parts", GUILayout.Height(30)))
            {
                SelectAllGroupedParts(config);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // Draw default Inspector fields
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Per-Group Quick Actions", EditorStyles.boldLabel);

        if (config.partGroups == null || config.partGroups.Count == 0)
        {
            EditorGUILayout.HelpBox("No part groups defined yet. Use 'Tools > AI Engine Part Auto-Grouping Tool' to generate groups automatically.", MessageType.Info);
            return;
        }

        // Master Action Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 Select ALL Grouped Parts", GUILayout.Height(28)))
        {
            SelectAllGroupedParts(config);
        }
        if (GUILayout.Button("📁 Organize Hierarchy Nodes", GUILayout.Height(28)))
        {
            OrganizeHierarchyNodes(config);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Per-Group Action Buttons
        EditorGUILayout.LabelField("Per-Group Actions:", EditorStyles.miniBoldLabel);
        for (int i = 0; i < config.partGroups.Count; i++)
        {
            var group = config.partGroups[i];
            int count = group.parts != null ? group.parts.Count : 0;
            string gName = string.IsNullOrEmpty(group.groupName) ? $"Group {i + 1}" : group.groupName;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{gName} ({count} parts)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"🔍 Select in Hierarchy ({count})"))
            {
                SelectGroupParts(group);
            }
            if (GUILayout.Button("📁 Reparent to Folder"))
            {
                ReparentSingleGroup(config.gameObject, group);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    private void SelectAllGroupedParts(EngineAssemblyConfig config)
    {
        List<GameObject> allGOs = new List<GameObject>();
        foreach (var group in config.partGroups)
        {
            if (group.parts != null)
            {
                foreach (var p in group.parts)
                {
                    if (p != null && !allGOs.Contains(p.gameObject))
                        allGOs.Add(p.gameObject);
                }
            }
        }

        if (allGOs.Count > 0)
        {
            Selection.objects = allGOs.ToArray();
            Debug.Log($"[EngineAssemblyConfig] Selected {allGOs.Count} parts in Hierarchy.");
        }
    }

    private void SelectGroupParts(EnginePartGroupData group)
    {
        if (group.parts == null || group.parts.Count == 0) return;

        List<GameObject> goList = group.parts.Where(p => p != null).Select(p => p.gameObject).ToList();
        if (goList.Count > 0)
        {
            Selection.objects = goList.ToArray();
            Debug.Log($"[EngineAssemblyConfig] Selected {goList.Count} parts for group '{group.groupName}'.");
        }
    }

    private void ReparentSingleGroup(GameObject rootGO, EnginePartGroupData group)
    {
        if (group.parts == null || group.parts.Count == 0) return;

        Transform existingParent = rootGO.transform.Find(group.groupName);
        GameObject parentGO = existingParent != null ? existingParent.gameObject : new GameObject(group.groupName);

        Undo.RegisterCreatedObjectUndo(parentGO, "Create Group Parent");
        parentGO.transform.SetParent(rootGO.transform, worldPositionStays: false);

        Vector3 avgPos = Vector3.zero;
        int valid = 0;
        foreach (var p in group.parts)
        {
            if (p != null) { avgPos += p.transform.position; valid++; }
        }
        if (valid > 0) parentGO.transform.position = avgPos / valid;

        foreach (var p in group.parts)
        {
            if (p != null)
            {
                Undo.SetTransformParent(p.transform, parentGO.transform, "Reparent Part to Group Node");
            }
        }
        Debug.Log($"[EngineAssemblyConfig] Reparented {valid} parts into folder node '{group.groupName}'.");
    }

    private void OrganizeHierarchyNodes(EngineAssemblyConfig config)
    {
        foreach (var group in config.partGroups)
        {
            ReparentSingleGroup(config.gameObject, group);
        }
        EditorUtility.SetDirty(config.gameObject);
        Debug.Log($"[EngineAssemblyConfig] Organized all {config.partGroups.Count} groups into Hierarchy nodes.");
    }
}
